using UnityEngine;
using UnityEngine.Rendering;
using System;

public class PatternCutEvaluator : MonoBehaviour
{
    [Header("References")]
    public SawCutter sawCutter;
    public Texture2D patternTexture; // black lines, transparent elsewhere (Read/Write Enabled)

    [Header("Sampling & Thresholds")]
    [Range(0f,1f)] public float cutMaskThreshold = 0.5f;
    [Range(0f,1f)] public float lineLumaThreshold = 0.3f;
    [Range(0f,1f)] public float lineAlphaThreshold = 0.1f;
    public int evaluationResolution = 512; // base width

    [Header("Tolerance Around Lines")]
    public float maxAllowedDistanceUV = 0.01f;
    public float extraToleranceUV = 0.008f;
    public bool distanceWeightedOvercut = true;

    [Header("Score Weights")]
    [Range(0f, 1.5f)] public float wCoverage = 1f;
    [Range(0f, 2f)] public float wOvercut = 0.6f;

    [Header("Line Thickness Estimation")]
    [Range(0.6f, 0.99f)] public float thicknessPercentile = 0.9f;
    [Range(0.5f, 2f)] public float thicknessScale = 1.0f;
    public float minHalfThicknessUV = 0.004f;
    [HideInInspector] public float lastEstimatedHalfThicknessUV = 0f;

    [Header("Debug")]
    public bool logDetails = true;
    public bool dumpLineMapStats = true;

    [Header("Performance")]
    [Tooltip("Cache the pattern's line map and distance field once per pattern.")]
    public bool cachePatternAnalysis = true;
    [Tooltip("Pick a smaller evaluation grid for thick lines.")]
    public bool autoAdjustEvalResolution = true;
    [Range(128, 4096)] public int minEvalResolution = 256;
    [Range(128, 4096)] public int maxEvalResolution = 1024;

    public Action<float> OnScoreComputed;

    bool _pending;

    // Adaptive resolution override
    int _evalResOverride = 0;
    int EvalWidth => Mathf.Clamp(_evalResOverride > 0 ? _evalResOverride : evaluationResolution, 64, 4096);
    int EvalHeight
    {
        get
        {
            if (!patternTexture) return EvalWidth;
            float aspect = patternTexture.height / (float)patternTexture.width;
            int h = Mathf.RoundToInt(EvalWidth * aspect);
            return Mathf.Clamp(h, 64, 4096);
        }
    }

    // Cache
    float[] _lineCached;                  // 0/1 line map (letterboxed)
    float[] _distFromLineCached;          // distance from line (pixels)
    int _cacheW, _cacheH, _cacheLineCount;
    int _cachedTexId;
    float _cachedLumaTh, _cachedAlphaTh;
    float _scaleX = 1f, _scaleY = 1f;     // letterbox scale

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
        if (!patternTexture)
        {
            Debug.LogWarning("Pattern texture missing");
            return;
        }

        int W = EvalWidth;
        int H = EvalHeight;
        int maskW = sawCutter.maskResolution;
        int maskH = sawCutter.maskResolution;

        // Ensure cached pattern analysis (line map + distance) is available
        EnsurePatternCache(W, H);

        if (_cacheLineCount == 0)
        {
            if (logDetails) Debug.LogWarning("[PatternCutEvaluator] No line pixels detected.");
            OnScoreComputed?.Invoke(0f);
            return;
        }

        // Read mask into local arrays (cutBinary + raw values for thresholding)
        var data = req.GetData<byte>();
        float[] mask = new float[W * H];
        float[] cutBinary = new float[W * H];
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
                int i = y * W + x;
                mask[i] = m;
                cutBinary[i] = (m < cutMaskThreshold) ? 1f : 0f;
            }
        }

        // Distance field from cuts (for coverage test)
        float[] distFromCut = BuildDistanceField(cutBinary, W, H);

        int coveredCount = 0;
        int totalCutPixels = 0;
        double overcutPenaltyAccum = 0.0;

        float minDim = Mathf.Min(W, H);
        float tol1Px = maxAllowedDistanceUV * minDim;
        float tol2Px = (maxAllowedDistanceUV + Mathf.Max(extraToleranceUV, 0.003f)) * minDim;

        // Penalty + coverage
        for (int i = 0; i < W * H; i++)
        {
            if (mask[i] < cutMaskThreshold)
            {
                totalCutPixels++;
                float dLine = _distFromLineCached[i];
                if (dLine > tol2Px)
                {
                    if (distanceWeightedOvercut)
                    {   
                        float beyond = dLine - tol2Px;
                        float weight = 1f + (beyond / (minDim * 0.25f));
                        overcutPenaltyAccum += weight;
                    }
                    else overcutPenaltyAccum += 1.0;
                }
            }

            if (_lineCached[i] > 0.5f)
            {
                if (distFromCut[i] <= tol1Px)
                    coveredCount++;
            }
        }

        float coverage = _cacheLineCount > 0 ? (float)coveredCount / _cacheLineCount : 0f;
        float overcutRatio = totalCutPixels > 0 ? Mathf.Clamp01((float)(overcutPenaltyAccum / totalCutPixels)) : 0f;
        float score = Mathf.Clamp01(wCoverage * coverage - wOvercut * overcutRatio);

        if (logDetails)
        {
            Debug.Log(
                $"[Eval] Pattern:{patternTexture.width}x{patternTexture.height} Grid:{W}x{H} " +
                $"Lines:{_cacheLineCount} Covered:{coveredCount} Coverage:{coverage:P2} Overcut:{overcutRatio:P2} " +
                $"tol1Px:{tol1Px:F1} tol2Px:{tol2Px:F1} Score:{score:P2}");
        }
        if (dumpLineMapStats)
        {
            float pctLines = (float)_cacheLineCount / (W * H);
            Debug.Log($"[LineMapStats] linePixelFraction:{pctLines:P2}");
        }

        OnScoreComputed?.Invoke(score);
    }

    public void AutoTuneForCurrentPattern()
    {
        if (!patternTexture || !sawCutter) return;

        // Quick low-res pass to estimate thickness (uses current evaluationResolution but half to speed-up)
        int baseW = Mathf.Clamp(evaluationResolution / 2, 128, 1024);
        int baseH = Mathf.Clamp(Mathf.RoundToInt(baseW * (patternTexture.height / (float)patternTexture.width)), 128, 2048);

        // Letterbox scale
        float texAspect = (float)patternTexture.width / Mathf.Max(1, patternTexture.height);
        float scaleX = 1f, scaleY = 1f;
        if (texAspect > 1f) scaleY = 1f / texAspect; else scaleX = texAspect;

        // Build low-res line map
        float[] lineLow = new float[baseW * baseH];
        int lineCountLow = 0;
        for (int y = 0; y < baseH; y++)
        {
            float v = (y + 0.5f) / baseH;
            for (int x = 0; x < baseW; x++)
            {
                float u = (x + 0.5f) / baseW;
                float su = 0.5f + (u - 0.5f) * scaleX;
                float sv = 0.5f + (v - 0.5f) * scaleY;
                if (su < 0f || su > 1f || sv < 0f || sv > 1f) continue;
                Color c = patternTexture.GetPixelBilinear(su, sv);
                float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                bool isLine = c.a > lineAlphaThreshold && luma < lineLumaThreshold;
                if (isLine)
                {
                    lineLow[y * baseW + x] = 1f;
                    lineCountLow++;
                }
            }
        }

        float halfThicknessPx = EstimateLineHalfThicknessPx(lineLow, baseW, baseH);
        float minDimLow = Mathf.Min(baseW, baseH);
        float halfThicknessUV = Mathf.Max(minHalfThicknessUV, (halfThicknessPx / minDimLow) * thicknessScale);
        lastEstimatedHalfThicknessUV = halfThicknessUV;

        // Adaptive resolution: target ~6 px half-thickness
        if (autoAdjustEvalResolution)
        {
            const float targetHalfPx = 6f;
            float scale = Mathf.Clamp(targetHalfPx / Mathf.Max(1f, halfThicknessPx), 0.25f, 2f);
            _evalResOverride = Mathf.Clamp(Mathf.RoundToInt(evaluationResolution * scale), minEvalResolution, maxEvalResolution);
        }
        else
        {
            _evalResOverride = 0; // use evaluationResolution as-is
        }

        // Update coverage tolerances based on brush/line
        float brushUV = Mathf.Max(1e-5f, sawCutter.ApproxBrushUVRadius());
        float tol1UV = Mathf.Clamp(Mathf.Lerp(brushUV, halfThicknessUV, 0.75f), 0.5f * brushUV, halfThicknessUV * 1.15f);
        maxAllowedDistanceUV = tol1UV;
        extraToleranceUV = Mathf.Clamp(maxAllowedDistanceUV * 0.5f, 0.0005f, 0.05f);

        // Build (or rebuild) cached line map + distance at the final chosen resolution
        if (cachePatternAnalysis)
            EnsurePatternCache(EvalWidth, EvalHeight, force: true);

        if (logDetails)
        {
            Debug.Log($"[AutoTune] halfPxLow:{halfThicknessPx:F2} halfUV:{halfThicknessUV:F4} brushUV:{brushUV:F4} " +
                      $"eval:{EvalWidth}x{EvalHeight} tol1UV:{tol1UV:F4}");
        }
    }

    void EnsurePatternCache(int W, int H, bool force = false)
    {
        // Rebuild if:
        // - no cache
        // - resolution changed
        // - thresholds changed
        // - texture changed
        int texId = patternTexture ? patternTexture.GetInstanceID() : 0;
        bool needRebuild =
            force ||
            _lineCached == null || _distFromLineCached == null ||
            _cacheW != W || _cacheH != H ||
            _cachedTexId != texId ||
            !Mathf.Approximately(_cachedLumaTh, lineLumaThreshold) ||
            !Mathf.Approximately(_cachedAlphaTh, lineAlphaThreshold);

        if (!needRebuild) return;

        _cacheW = W;
        _cacheH = H;
        _cachedTexId = texId;
        _cachedLumaTh = lineLumaThreshold;
        _cachedAlphaTh = lineAlphaThreshold;

        // Compute letterbox scale
        float texAspect = (float)patternTexture.width / Mathf.Max(1, patternTexture.height);
        _scaleX = 1f; _scaleY = 1f;
        if (texAspect > 1f) _scaleY = 1f / texAspect; else _scaleX = texAspect;

        // Build line map
        _lineCached = new float[W * H];
        _cacheLineCount = 0;
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float su = 0.5f + (u - 0.5f) * _scaleX;
                float sv = 0.5f + (v - 0.5f) * _scaleY;
                if (su < 0f || su > 1f || sv < 0f || sv > 1f) continue;

                Color c = patternTexture.GetPixelBilinear(su, sv);
                float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                bool isLine = c.a > lineAlphaThreshold && luma < lineLumaThreshold;
                if (isLine)
                {
                    int i = y * W + x;
                    _lineCached[i] = 1f;
                    _cacheLineCount++;
                }
            }
        }

        // Build distance field from line (cached)
        _distFromLineCached = BuildDistanceField(_lineCached, W, H);

        if (logDetails)
        {
            float pctLines = (float)_cacheLineCount / (W * H);
            Debug.Log($"[Cache] Built line/dist cache {W}x{H} lines:{_cacheLineCount} ({pctLines:P1})");
        }
    }

    float EstimateLineHalfThicknessPx(float[] line, int W, int H)
    {
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

        int sampleCap = Mathf.Min(lineCount, 30000);
        float[] samples = new float[sampleCap];
        int stride = Mathf.Max(1, (W * H) / sampleCap);
        int si = 0;
        for (int i = 0; i < W * H && si < sampleCap; i += stride)
            if (line[i] > 0.5f) samples[si++] = distToBackground[i];
        Array.Sort(samples, 0, si);
        if (si == 0) return 2f;
        int idx = Mathf.Clamp(Mathf.RoundToInt(thicknessPercentile * (si - 1)), 0, si - 1);
        return Mathf.Max(1f, samples[idx]);
    }

    float[] BuildDistanceField(float[] seeds, int W, int H)
    {
        const float INF = 1e6f;
        float[] d = new float[W * H];
        for (int i = 0; i < W * H; i++)
            d[i] = seeds[i] > 0.5f ? 0f : INF;

        // Forward
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
        // Backward
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
