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
    public Transform plateTransform;

    [Header("Camera Reset")]
    [Tooltip("Camera to reset on pattern change. If null, Camera.main is used.")]
    public Camera gameCamera;
    [Tooltip("Reset camera position & FOV/OrthoSize when pattern changes.")]
    public bool resetCameraOnPatternChange = true;

    [Header("Patterns")]
    public List<PatternEntry> patterns = new();
    public int defaultPatternIndex = 0;

    [Header("Behaviour")]
    public bool autoApplyOnStart = true;
    public bool resetScoreOnPatternChange = true;
    public bool resetMaskOnPatternChange = true;
    public bool autoMatchBrushToLine = true;
    [Range(0.5f, 1.5f)] public float brushToLineScale = 0.9f;
    public float minWorldBrushRadius = 0.005f;
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
    PlateManipulator _plateManipulator;

    // Plate start pose
    Vector3 _plateStartPosition;
    Quaternion _plateStartRotation;
    bool _plateStartCaptured;

    // Camera start cache
    bool _camStartCaptured;
    Vector3 _camStartPosition;
    Quaternion _camStartRotation;
    float _camStartOrthoSize;

    void Start()
    {
        if (plateTransform)
        {
            _plateStartPosition = plateTransform.position;
            _plateStartRotation = plateTransform.rotation;
            _plateStartCaptured = true;
            _plateManipulator = plateTransform.GetComponent<PlateManipulator>();
        }

        CaptureCameraStartIfNeeded();

        if (autoApplyOnStart && patterns.Count > 0)
            ApplyPattern(defaultPatternIndex);
    }

    void CaptureCameraStartIfNeeded()
    {
        if (_camStartCaptured) return;
        if (!gameCamera) gameCamera = Camera.main;
        if (!gameCamera) return;

        _camStartPosition = gameCamera.transform.position;
        _camStartRotation = gameCamera.transform.rotation;
        _camStartOrthoSize = gameCamera.orthographicSize;
        _camStartCaptured = true;
    }

    void ResetCameraToStart()
    {
        if (!resetCameraOnPatternChange || !_camStartCaptured || !gameCamera) return;

        // Restore transform
        gameCamera.transform.position = _camStartPosition;
        gameCamera.transform.rotation = _camStartRotation;

        // Restore projection size
        if (gameCamera.orthographic)
            gameCamera.orthographicSize = _camStartOrthoSize;
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

        // Reset plate pose (syncs PlateManipulator internal targets)
        if (_plateManipulator)
        {
            _plateManipulator.ResetToStart();
        }
        else if (_plateStartCaptured && plateTransform)
        {
            plateTransform.position = _plateStartPosition;
            plateTransform.rotation = _plateStartRotation;
        }

        // Reset camera view if requested
        if (resetCameraOnPatternChange)
            ResetCameraToStart();

        // Overlay
        if (patternOverlayRenderer && entry.texture)
        {
            var mat = patternOverlayRenderer.material;
            mat.SetTexture("_PatternTex", entry.texture);
            mat.SetColor("_LineTint", entry.lineTint);
            mat.SetFloat("_Alpha", entry.overlayAlpha);
            if (sawCutter) sawCutter.PushPlateMappingTo(mat);
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
                    float desiredUVRadius = halfThicknessUV * Mathf.Clamp(brushToLineScale, 0.5f, 1.5f);
                    float worldR = sawCutter.UVToWorldRadius(desiredUVRadius);
                    sawCutter.brushWorldRadius = Mathf.Clamp(worldR, minWorldBrushRadius, maxWorldBrushRadius);
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