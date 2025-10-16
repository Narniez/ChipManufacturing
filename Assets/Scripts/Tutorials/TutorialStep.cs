using UnityEngine;

public enum TutorialBubbleAnchor { TopLeft, TopRight, BottomLeft, BottomRight }
public enum FingerMode { None, Tap, Drag }

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Scriptable Objects/Tutorial/Step")]
public class TutorialStep : ScriptableObject
{
    [Header("Dialogue")]
    [TextArea] public string text;
    public Sprite speaker;
    public TutorialBubbleAnchor anchor = TutorialBubbleAnchor.BottomLeft;

    [Header("Progress condition")]
    public TutorialSignal waitForSignal = TutorialSignal.None;
    [Tooltip("Optional MachineData filter for Shop events; leave null to accept any.")]
    public MachineData machineFilter;

    [Header("Highlight (optional)")]
    [Tooltip("Unity scene path or GameObject name to find RectTransform to highlight (e.g., \"Canvas/ShopButton\").")]
    public string highlightTargetPath;
    [Tooltip("Blocks input outside highlight area until step completes.")]
    public bool gateInputOutsideHighlight = true;

    [Header("Finger hint")]
    public FingerMode fingerMode = FingerMode.Tap;
    public bool showFinger = true;
    public Vector2 fingerOffset = new Vector2(0, -30);

    [Tooltip("For Drag mode, target path to drag towards. Leave empty for Tap mode.")]
    public string dragTargetPath;
    [Tooltip("For Drag mode, seconds to move from start to end.")]
    public float dragDuration = 1.25f;
    [Tooltip("For Drag mode, repeat drag loop.")]
    public bool dragLoop = true;
}