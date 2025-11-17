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

    // UI button can call this with an index
    public void ApplyPattern(int index)
    {
        if (index < 0 || index >= patterns.Count)
        {
            Debug.LogWarning($"Pattern index {index} out of range.");
            return;
        }
        var entry = patterns[index];
        _current = entry;

        // Update overlay
        if (patternOverlayRenderer && entry.texture)
        {
            var mat = patternOverlayRenderer.material; 
            mat.SetTexture("_PatternTex", entry.texture);
            mat.SetColor("_LineTint", entry.lineTint);
            mat.SetFloat("_Alpha", entry.overlayAlpha);
        }

        // Update evaluator
        if (patternEvaluator)
            patternEvaluator.patternTexture = entry.texture;

        // Reset mask (clear holes)
        if (resetMaskOnPatternChange && sawCutter)
            sawCutter.ResetCutMask();

        // Reset score display
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

    // Example helper to cycle through patterns (assign to a “Next Pattern” button)
    public void NextPattern()
    {
        if (patterns.Count == 0) return;
        int currentIndex = _current.HasValue ? patterns.FindIndex(p => p.name == _current.Value.name) : -1;
        int next = (currentIndex + 1 + patterns.Count) % patterns.Count;
        ApplyPattern(next);
    }
}