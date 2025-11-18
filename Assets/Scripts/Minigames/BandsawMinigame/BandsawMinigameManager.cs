using System;
using System.Collections.Generic;
using UnityEngine;

public class BandsawMinigameManager : MonoBehaviour
{
    [Header("Core References")]
    public SawCutter sawCutter;
    public PatternCutEvaluator patternEvaluator;
    public CutScoreUI scoreUI;
    public Renderer patternOverlayRenderer;

    [Header("Patterns")]
    public List<PatternEntry> patterns = new();
    public int defaultPatternIndex = 0;

    [Header("Behaviour")]
    public bool autoApplyOnStart = true;
    public bool resetScoreOnPatternChange = true;
    public bool resetMaskOnPatternChange = true;

    [Header("Brush Auto-Match")]
    [Tooltip("Automatically adjust brush radius to match line thickness of the selected pattern.")]
    public bool autoMatchBrushToLine = true;
    [Tooltip("Scale factor applied to half line thickness (UV) when setting brush radius. 1 = full half-thickness.")]
    [Range(0.5f, 1.5f)] public float brushToLineScale = 0.9f;
    [Tooltip("Minimum world radius clamp when auto-matching.")]
    public float minWorldBrushRadius = 0.005f;
    [Tooltip("Maximum world radius clamp when auto-matching.")]
    public float maxWorldBrushRadius = 0.2f;

    [Header("Events")]
    public Action<PatternEntry> OnPatternChanged;

    [Serializable]
    public struct PatternEntry
    {
        public string name;
        public Texture2D texture;
        public Color lineTint;
        [Range(0f, 1f)] public float overlayAlpha;
    }

    PatternEntry? _current;

    void Start()
    {
        if (autoApplyOnStart && patterns.Count > 0)
            ApplyPattern(defaultPatternIndex);
    }

    public void ApplyPattern(int index)
    {
        if (index < 0 || index >= patterns.Count)
        {
            Debug.LogWarning($"Pattern index {index} out of range.");
            return;
        }
        var entry = patterns[index];
        _current = entry;

        // Overlay
        if (patternOverlayRenderer && entry.texture)
        {
            var mat = patternOverlayRenderer.material;
            mat.SetTexture("_PatternTex", entry.texture);
            mat.SetColor("_LineTint", entry.lineTint);
            mat.SetFloat("_Alpha", entry.overlayAlpha);
        }

        // Evaluator + auto-tune
        if (patternEvaluator)
        {
            patternEvaluator.patternTexture = entry.texture;
            patternEvaluator.AutoTuneForCurrentPattern();

            if (autoMatchBrushToLine && sawCutter)
            {
                float halfThicknessUV = patternEvaluator.lastEstimatedHalfThicknessUV;
                if (halfThicknessUV > 0f)
                {
                    // Use half-thickness directly (already scaled/tuned), optionally enlarge slightly
                    float desiredUVRadius = halfThicknessUV;
                    float worldR = sawCutter.UVToWorldRadius(desiredUVRadius);
                    worldR = Mathf.Clamp(worldR, minWorldBrushRadius, maxWorldBrushRadius);
                    sawCutter.brushWorldRadius = worldR;
                }
            }
        }

        if (resetMaskOnPatternChange && sawCutter)
            sawCutter.ResetCutMask();

        if (resetScoreOnPatternChange && scoreUI)
            scoreUI.ResetDisplay();

        OnPatternChanged?.Invoke(entry);
    }

    public void ApplyPatternByName(string patternName)
    {
        int idx = patterns.FindIndex(p => string.Equals(p.name, patternName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) ApplyPattern(idx);
        else Debug.LogWarning($"Pattern '{patternName}' not found.");
    }

    public void NextPattern()
    {
        if (patterns.Count == 0) return;
        int currentIndex = _current.HasValue ? patterns.FindIndex(p => p.name == _current.Value.name) : -1;
        int next = (currentIndex + 1 + patterns.Count) % patterns.Count;
        ApplyPattern(next);
    }
}