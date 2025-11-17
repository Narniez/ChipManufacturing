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
    public float maxAllowedDistanceUV = 0.01f;                // strict radius (UV units)
    public float extraToleranceUV = 0.005f;                   // soft band extra
    public float outsidePenaltyMultiplier = 2f;               // outside penalty weight
    public bool distanceWeightedOvercut = true;               // scale penalty by distance
    public bool useDiceInsteadOfCoverage = false;             // optional alt metric

    [Header("Coverage Mode")]
    public bool coverageByDistance = true;                    // NEW: make coverage thickness-agnostic

    [Header("Weights")]
    [Range(0,1)] public float wCoverage = 0.5f;
    [Range(0,1)] public float wPrecision = 0.3f;
    [Range(0,1)] public float wOvercut = 0.2f;

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

        int linePixelCount   = 0;
        int coveredCount     = 0;       // for coverage-by-pixel or after distance threshold
        int totalCutPixels   = 0;       // for Dice / overcut
        double precisionAccum = 0.0;
        double precisionSamples = 0.0;
        double overcutPenaltyAccum = 0.0;

        float tol1Px = maxAllowedDistanceUV * W;
        float tol2Px = (maxAllowedDistanceUV + extraToleranceUV) * W;

        // Pass for precision and overcut
        for (int i = 0; i < W * H; i++)
        {
            bool isCut = mask[i] < cutMaskThreshold;
            if (isCut) totalCutPixels++;

            bool isLine = line[i] > 0.5f;
            float dPx = distFromLine[i];

            if (isLine && isCut)
            {
                // Precision only within strict tolerance
                if (dPx <= tol1Px)
                {
                    float norm = 1f - (dPx / tol1Px); // 1 center, 0 at tol edge
                    precisionAccum += norm;
                    precisionSamples++;
                }
            }
            else if (!isLine && isCut)
            {
                // Overcut: penalize cuts away from the line
                if (dPx > tol1Px)
                {
                    float outsideFactor = dPx <= tol2Px ? 0.5f : 1f;
                    if (distanceWeightedOvercut && dPx > tol2Px)
                    {
                        float beyond = dPx - tol2Px;
                        outsideFactor += beyond / (W * 0.5f);
                    }
                    overcutPenaltyAccum += outsideFactor;
                }
            }
        }

        // Coverage computation
        if (coverageByDistance)
        {
            // Thickness-agnostic: a line pixel is "covered" if any cut is within tol1
            for (int i = 0; i < W * H; i++)
            {
                if (line[i] > 0.5f)
                {
                    linePixelCount++;
                    if (distFromCut[i] <= tol1Px)
                        coveredCount++;
                }
            }
        }
        else
        {
            // Pixel-wise (old): requires line pixel itself to be cut
            for (int i = 0; i < W * H; i++)
            {
                if (line[i] > 0.5f)
                {
                    linePixelCount++;
                    if (mask[i] < cutMaskThreshold)
                        coveredCount++;
                }
            }
        }

        // Metrics
        float coverage = linePixelCount > 0 ? (float)coveredCount / linePixelCount : 0f;
        float precision = precisionSamples > 0 ? (float)(precisionAccum / precisionSamples) : 0f;

        int nonLinePixels = W * H - linePixelCount;
        float rawOvercut = nonLinePixels > 0 ? (float)(overcutPenaltyAccum / nonLinePixels) : 0f;
        float overcut = rawOvercut * outsidePenaltyMultiplier;

        // Optional Dice
        float dice = 0f;
        if (useDiceInsteadOfCoverage && (totalCutPixels + linePixelCount) > 0)
            dice = 2f * coveredCount / (float)(totalCutPixels + linePixelCount);

        float primaryCoverageMetric = useDiceInsteadOfCoverage ? dice : coverage;

        // Combine score
        float score = wCoverage * primaryCoverageMetric
                    + wPrecision * precision
                    - wOvercut * overcut;

        score = Mathf.Clamp01(score);

        if (logDetails)
        {
            string covName = useDiceInsteadOfCoverage ? "Dice" : (coverageByDistance ? "CoverageDist" : "CoveragePix");
            Debug.Log(
                $"Cut Evaluation -> {covName}:{primaryCoverageMetric:F3} Precision:{precision:F3} Overcut:{overcut:F3} Score:{score:F3} " +
                $"(covered {coveredCount}/{linePixelCount}, cutPixels:{totalCutPixels}, tol1Px:{tol1Px:F1})");
        }

        OnScoreComputed?.Invoke(score);
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
