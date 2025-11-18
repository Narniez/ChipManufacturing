using UnityEngine;
using UnityEngine.Rendering;
using System;

public class PatternCutEvaluator : MonoBehaviour
{
    [Header("References")]
    public SawCutter sawCutter;
    public Texture2D patternTexture; // black lines, transparent elsewhere (Read/Write Enabled)

    [Header("Sampling & Thresholds")]
    [Range(0f,1f)] public float cutMaskThreshold = 0.5f;      // mask < threshold => considered cut
    [Range(0f,1f)] public float lineLumaThreshold = 0.3f;     // pattern pixel luma < threshold => line
    [Range(0f,1f)] public float lineAlphaThreshold = 0.1f;    // pattern alpha > threshold => line
    public int evaluationResolution = 512;                    // downsample for faster scoring

    [Header("Tolerance Around Lines")]
    public float maxAllowedDistanceUV = 0.01f;                // strict radius (UV units) used for coverage
    public float extraToleranceUV = 0.008f;                   // soft band extra (make this larger for curves)
    public bool distanceWeightedOvercut = true;               // scale penalty by distance beyond soft band

    [Header("Coverage Mode")]
    public bool coverageByDistance = true;                    // thickness-agnostic coverage (recommended)

    [Header("Score Weights")]
    [Tooltip("Coverage drives the score almost 1:1. Set to 1 for direct mapping.")]
    [Range(0f, 1.5f)] public float wCoverage = 1f;
    [Tooltip("Penalty strength for cutting outside the soft band around the line. Typical 0.4..0.9")]
    [Range(0f, 2f)] public float wOvercut = 0.6f;

    [Header("Line Thickness Estimation")]
    [Range(0.6f, 0.99f)] public float thicknessPercentile = 0.9f;
    [Range(0.5f, 2f)] public float thicknessScale = 1.0f;
    public float minHalfThicknessUV = 0.004f;
    [HideInInspector] public float lastEstimatedHalfThicknessUV = 0f;

    [Header("Debug / Output")]
    public bool logDetails = true;

    public Action<float> OnScoreComputed;

    bool _pending;

    public void EvaluateCut()
    {
        if (_pending || sawCutter == null) return;
        var rt = GetMaskRT();
        if (rt == null) return;
        _pending = true;
        AsyncGPUReadback.Request(rt, 0, TextureFormat.R8, OnMaskReadback);
    }

    RenderTexture GetMaskRT()
        => sawCutter?.plateMaterial?.GetTexture("_CutMask") as RenderTexture;

    void OnMaskReadback(AsyncGPUReadbackRequest req)
    {
        _pending = false;
        if (req.hasError)
        {
            Debug.LogWarning("Mask readback error");
            return;
        }
        if (patternTexture == null)
        {
            Debug.LogWarning("Pattern texture missing");
            return;
        }

        var data = req.GetData<byte>();
        int maskW = sawCutter.maskResolution;
        int maskH = sawCutter.maskResolution;

        int W = evaluationResolution;
        int H = evaluationResolution;

        // Resample mask -> cutBinary
        float[] mask = new float[W * H];
        float[] cutBinary = new float[W * H]; // 1 if cut, else 0
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            int srcY = Mathf.Clamp(Mathf.RoundToInt(v * (maskH - 1)), 0, maskH - 1);
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                int srcX = Mathf.Clamp(Mathf.RoundToInt(u * (maskW - 1)), 0, maskW - 1);
                int srcIdx = srcY * maskW + srcX;
                float m = data[srcIdx] / 255f;
                mask[y * W + x] = m;
                cutBinary[y * W + x] = (m < cutMaskThreshold) ? 1f : 0f;
            }
        }

        // Build binary pattern line map
        float[] line = new float[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            int srcY = Mathf.Clamp(Mathf.RoundToInt(v * (patternTexture.height - 1)), 0, patternTexture.height - 1);
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                int srcX = Mathf.Clamp(Mathf.RoundToInt(u * (patternTexture.width - 1)), 0, patternTexture.width - 1);
                Color c = patternTexture.GetPixel(srcX, srcY);
                float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                bool isLine = c.a > lineAlphaThreshold && luma < lineLumaThreshold;
                line[y * W + x] = isLine ? 1f : 0f;
            }
        }

        // Distance fields
        float[] distFromLine = BuildDistanceField(line, W, H);       // distance (px) from nearest line
        float[] distFromCut  = BuildDistanceField(cutBinary, W, H);  // distance (px) from nearest cut

        // Metrics accumulators
        int linePixelCount   = 0;
        int coveredCount     = 0;       // line pixels that are covered (within tol1)
        int totalCutPixels   = 0;       // all pixels cut
        double overcutPenaltyAccum = 0.0;

        // Strict coverage tolerance and wider soft band for penalty
        float tol1Px = maxAllowedDistanceUV * W;                                  // coverage
        float tol2Px = (maxAllowedDistanceUV + Mathf.Max(extraToleranceUV, 0.003f)) * W; // soft band (>= tol1)

        // First pass: count total cut pixels and accumulate "bad cuts" only beyond tol2
        for (int i = 0; i < W * H; i++)
        {
            bool isCut = mask[i] < cutMaskThreshold;
            if (!isCut) continue;

            totalCutPixels++;

            float dPx = distFromLine[i];
            if (dPx > tol2Px)
            {
                if (distanceWeightedOvercut)
                {
                    // Weight grows with distance beyond soft band; normalized by screen size
                    float beyond = dPx - tol2Px;
                    float weight = 1f + (beyond / (W * 0.25f)); // gentle growth
                    overcutPenaltyAccum += weight;
                }
                else
                {
                    overcutPenaltyAccum += 1.0;
                }
            }
            // Note: cuts between tol1 and tol2 do not penalize (near-miss region along thick/curvy lines)
        }

        // Coverage (thickness-agnostic): a line pixel is covered if any cut is within tol1
        for (int i = 0; i < W * H; i++)
        {
            if (line[i] > 0.5f)
            {
                linePixelCount++;
                if (distFromCut[i] <= tol1Px)
                    coveredCount++;
            }
        }

        // Final metrics
        float coverage = linePixelCount > 0 ? (float)coveredCount / linePixelCount : 0f;
        float overcutRatio = totalCutPixels > 0 ? (float)(overcutPenaltyAccum / totalCutPixels) : 0f;

        // Score: coverage almost 1:1, minus penalty only for far-off cuts
        float score = wCoverage * coverage - wOvercut * overcutRatio;
        score = Mathf.Clamp01(score);

        if (logDetails)
        {
            Debug.Log(
                $"Cut Evaluation -> Coverage:{coverage:F3} Overcut:{overcutRatio:F3} Score:{score:F3} " +
                $"(covered {coveredCount}/{linePixelCount}, cutPixels:{totalCutPixels}, tol1Px:{tol1Px:F1}, tol2Px:{tol2Px:F1})");
        }

        OnScoreComputed?.Invoke(score);
    }

    // Call this after assigning patternTexture (e.g., when switching patterns)
    public void AutoTuneForCurrentPattern()
    {
        if (patternTexture == null || sawCutter == null) return;

        int W = Mathf.Clamp(evaluationResolution, 128, 2048);
        int H = W;

        // Build binary line map at evaluation resolution using current thresholds
        float[] line = new float[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            int srcY = Mathf.Clamp(Mathf.RoundToInt(v * (patternTexture.height - 1)), 0, patternTexture.height - 1);
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                int srcX = Mathf.Clamp(Mathf.RoundToInt(u * (patternTexture.width - 1)), 0, patternTexture.width - 1);
                Color c = patternTexture.GetPixel(srcX, srcY);
                float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                bool isLine = c.a > lineAlphaThreshold && luma < lineLumaThreshold;
                line[y * W + x] = isLine ? 1f : 0f;
            }
        }

        // Estimate median half-thickness of the pattern lines in pixels
        float halfThicknessPx = EstimateLineHalfThicknessPx(line, W, H);
        float halfThicknessUV = Mathf.Max(minHalfThicknessUV, (halfThicknessPx / W) * thicknessScale);
        lastEstimatedHalfThicknessUV = halfThicknessUV;

        // Strict tolerance: tie to brush & thickness (favor thickness first for lining)
        float brushUV = Mathf.Max(1e-5f, sawCutter.ApproxBrushUVRadius());
        float tol1UV = Mathf.Clamp(Mathf.Lerp(brushUV, halfThicknessUV, 0.75f), 0.5f * brushUV, halfThicknessUV * 1.15f);
        maxAllowedDistanceUV = tol1UV;
        extraToleranceUV = Mathf.Clamp(maxAllowedDistanceUV * 0.5f, 0.0005f, 0.05f);

        // Coverage-first scoring (already set in your code)
        wCoverage = Mathf.Clamp(wCoverage, 0.75f, 1.2f);
        wOvercut = Mathf.Clamp(wOvercut, 0.5f, 1.25f);

            if (logDetails)
            Debug.Log($"[AutoTune] halfThicknessPx:{halfThicknessPx:F2} UV:{halfThicknessUV:F4} brushUV:{brushUV:F4} tol1UV:{tol1UV:F4} extra:{extraToleranceUV:F4}");
    }

    // Estimate average half-thickness by measuring distance from line pixels to nearest background
    float EstimateLineHalfThicknessPx(float[] line, int W, int H)
    {
        // Build background (seeds = background)
        float[] background = new float[W * H];
        int lineCount = 0;
        for (int i = 0; i < W * H; i++)
        {
            bool isLine = line[i] > 0.5f;
            if (isLine) lineCount++;
            background[i] = isLine ? 0f : 1f;
        }
        if (lineCount == 0) return 2f;

        float[] distToBackground = BuildDistanceField(background, W, H);

        // Collect distances only for line pixels (cap for performance)
        int sampleCap = Mathf.Min(lineCount, 30000);
        float[] samples = new float[sampleCap];
        int stride = Mathf.Max(1, (W * H) / sampleCap);
        int si = 0;
        for (int i = 0; i < W * H && si < sampleCap; i += stride)
        {
            if (line[i] > 0.5f)
                samples[si++] = distToBackground[i];
        }
        Array.Sort(samples, 0, si);
        if (si == 0) return 2f;

        // Use chosen percentile of interior distances (values are in pixels)
        int idx = Mathf.Clamp(Mathf.RoundToInt((thicknessPercentile) * (si - 1)), 0, si - 1);
        float pct = samples[idx];
        // Guard against zero (very thin AA lines)
        return Mathf.Max(1f, pct);
    }

    // Distance transform (two pass, approx Euclidean)
    float[] BuildDistanceField(float[] seeds, int W, int H)
    {
        const float INF = 1e6f;
        float[] d = new float[W * H];

        for (int i = 0; i < W * H; i++)
            d[i] = seeds[i] > 0.5f ? 0f : INF;

        // Pass 1
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                float best = d[i];
                if (x > 0) best = Mathf.Min(best, d[i - 1] + 1f);
                if (y > 0) best = Mathf.Min(best, d[i - W] + 1f);
                if (x > 0 && y > 0) best = Mathf.Min(best, d[i - W - 1] + 1.4142f);
                d[i] = best;
            }
        }

        // Pass 2
        for (int y = H - 1; y >= 0; y--)
        {
            for (int x = W - 1; x >= 0; x--)
            {
                int i = y * W + x;
                float best = d[i];
                if (x < W - 1) best = Mathf.Min(best, d[i + 1] + 1f);
                if (y < H - 1) best = Mathf.Min(best, d[i + W] + 1f);
                if (x < W - 1 && y < H - 1) best = Mathf.Min(best, d[i + W + 1] + 1.4142f);
                d[i] = best;
            }
        }
        return d;
    }
}
