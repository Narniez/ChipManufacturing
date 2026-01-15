using NUnit.Framework;
using System.Net.NetworkInformation;
using UnityEngine;
using TMPro;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public enum TutorialBubbleAnchor { TopLeft, TopMiddle, TopRight, MiddleLeft, Centre, MiddleRight, BottomLeft, BottomMiddle, BottomRight}
public enum FingerMode { None, Tap, Drag }

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Scriptable Objects/Tutorial/Step")]
public class TutorialStep : ScriptableObject
{
    [Header("Dialogue")]
    [TextArea] public string text;
    
    [Tooltip("Sprite to display when fullscreen is enabled.")]
    public Sprite textBackdropSprite;

    public Sprite textForegroundSprite;

    [Tooltip("Optional TMP font to use for this step's text. Leave null to use the overlay default.")]
    public TMP_FontAsset textFont;



    [Tooltip("If true, override the bubble text color for this step.")]
    public bool overrideTextColor = false;

    [Tooltip("Bubble text color when Override Text Color is true.")]
    public Color textColor = Color.white;


    public Sprite speaker;
    public TutorialBubbleAnchor anchor = TutorialBubbleAnchor.BottomLeft;

    [Header("Progress condition")]
    public TutorialSignal waitForSignal = TutorialSignal.None;

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

    public bool showDimmerImage = true;

    [Tooltip("Characters per second for dialogue typing. < 0 shows all instantly. If 0, the overlay default is used.")]
    public float typewriterCharsPerSecond = 0f;

    [Header("Slideshow")]
    [Tooltip("Sprite to display when fullscreen is enabled.")]
    public Sprite fullscreenSprite;

    [Header("Interaction control")]
    [Tooltip("If true, ALL underlying UI interaction is disabled for this step (ignores highlight gating).")]
    public bool blockAllUIInteraction = false;


}