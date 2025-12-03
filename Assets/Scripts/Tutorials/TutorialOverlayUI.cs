using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialOverlayUI : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("Overlay")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image dimmer; // acts as the blocker
    [SerializeField, Tooltip("Optional: image stretched to fill the canvas for slideshow/fullscreen steps.")]
    private Image fullscreenImage;
    [SerializeField, Tooltip("Optional: image to use behind text as backdrop.")]
    private Image textBackdropImage;

    [Header("Hint UI")]
    [SerializeField] private FingerHint finger;
    [SerializeField] private RectTransform bubble;
    [SerializeField] private Image bubbleSpeaker;
    [SerializeField] private TextMeshProUGUI bubbleText;

    [Header("Behavior")]
    [SerializeField] private bool deactivateRootOnHide = false;
    [SerializeField] private bool bringTargetAboveDimmer = true;

    [Header("Layout")]
    [SerializeField] private Vector2 bubbleEdgePadding = new Vector2(24f, 24f);

    [Header("Typewriter")]
    [SerializeField] private float defaultTypewriterCharsPerSecond = 40f;

    [Header("Block-All UI")]
    [SerializeField, Tooltip("Main UI canvases to disable when a step sets Block All UI Interaction. Do NOT include the tutorial overlay canvas.")]
    private Canvas[] uiCanvasesToBlock;

    [Header("Skip")]
    [SerializeField, Tooltip("Optional skip button shown on the overlay.")]
    private Button skipButton;
    [SerializeField, Tooltip("Show the skip button when the overlay is visible.")]
    private bool showSkipButton = true;

    private RectTransform _highlightTarget;
    private bool _gateOutside;
    private bool _blockAll;

    private TMP_FontAsset _defaultBubbleFont;
    private Color _defaultBubbleColor;

    // Lifter state
    private RectTransform _liftedTarget;
    private Canvas _liftedCanvas;
    private bool _liftedCanvasWasAdded;
    private bool _prevOverrideSorting;
    private int _prevSortingOrder;
    private GraphicRaycaster _addedRaycaster;

    // Finger canvas state
    private Canvas _fingerCanvas;
    private bool _fingerCanvasWasAdded;
    private bool _fingerPrevOverrideSorting;
    private int _fingerPrevSortingOrder;

    // Typewriter state
    private Coroutine _typewriterRoutine;

    public event System.Action SkipRequested;

    // Block-all UI state tracking
    private readonly System.Collections.Generic.Dictionary<Graphic, bool> _prevGraphicRaycast = new System.Collections.Generic.Dictionary<Graphic, bool>();
    private readonly System.Collections.Generic.Dictionary<Selectable, bool> _prevSelectableInteractable = new System.Collections.Generic.Dictionary<Selectable, bool>();

    void Awake()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (bubbleText != null)
        {
            _defaultBubbleFont = bubbleText.font;
            _defaultBubbleColor = bubbleText.color;
        }

        // Overlay visuals should not consume input
        if (bubbleSpeaker) bubbleSpeaker.raycastTarget = false;
        if (bubbleText) bubbleText.raycastTarget = false;
        if (fullscreenImage) fullscreenImage.raycastTarget = false;
        if (textBackdropImage) textBackdropImage.raycastTarget = false;

        // Dimmer starts disabled and non-raycastable
        if (dimmer)
        {
            dimmer.gameObject.SetActive(false);
            dimmer.raycastTarget = false;
        }

        // Wire skip
        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => SkipRequested?.Invoke());
            skipButton.gameObject.SetActive(false);
        }

        Show(false);
    }

    public bool IsTyping => _typewriterRoutine != null;

    private void StartTypewriter(float charsPerSecond)
    {
        if (bubbleText == null) return;

        // Stop any previous routine
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        // Immediate reveal if cps <= 0 or component is not active/enabled
        if (charsPerSecond <= 0f || !isActiveAndEnabled)
        {
            bubbleText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        bubbleText.ForceMeshUpdate();
        bubbleText.maxVisibleCharacters = 0;
        _typewriterRoutine = StartCoroutine(TypewriterRoutine(charsPerSecond));
    }

    public void SkipTypewriter()
    {
        if (bubbleText == null) return;

        if (_typewriterRoutine != null)
        {
            if (isActiveAndEnabled) StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        bubbleText.maxVisibleCharacters = int.MaxValue;
    }

    public void Show(bool visible)
    {
        if (deactivateRootOnHide && !visible)
        {
            // Stop typewriter before deactivating owner or children
            SkipTypewriter();

            RestoreLiftedTarget();
            RestoreFingerCanvas();
            RestoreUIRaycasts();

            if (fullscreenImage) fullscreenImage.gameObject.SetActive(false);
            if (textBackdropImage) textBackdropImage.gameObject.SetActive(false);
            if (dimmer) { dimmer.gameObject.SetActive(false); dimmer.raycastTarget = false; }
            if (skipButton) skipButton.gameObject.SetActive(false);

            gameObject.SetActive(false);
            return;
        }

        if (!visible)
        {
            // Stop typewriter before hiding children that might host this component
            SkipTypewriter();

            RestoreLiftedTarget();
            RestoreFingerCanvas();
            RestoreUIRaycasts();

            if (fullscreenImage) fullscreenImage.gameObject.SetActive(false);
            if (textBackdropImage) textBackdropImage.gameObject.SetActive(false);
            if (dimmer) { dimmer.gameObject.SetActive(false); dimmer.raycastTarget = false; }
            if (skipButton) skipButton.gameObject.SetActive(false);
        }

        if (dimmer) dimmer.gameObject.SetActive(visible);
        if (bubble) bubble.gameObject.SetActive(visible);
        if (bubbleSpeaker) bubbleSpeaker.gameObject.SetActive(visible);
        if (bubbleText) bubbleText.gameObject.SetActive(visible);
        if (finger) finger.gameObject.SetActive(visible);
        if (fullscreenImage) fullscreenImage.gameObject.SetActive(false);
        if (textBackdropImage) textBackdropImage.gameObject.SetActive(false);
        if (skipButton) skipButton.gameObject.SetActive(visible && showSkipButton);

        _highlightTarget = null;
        _gateOutside = false;
        _blockAll = false;
        enabled = true;
    }

    public void ConfigureStep(TutorialStep step, RectTransform highlightTarget)
    {
        if (_liftedTarget != null && _liftedTarget != highlightTarget)
            RestoreLiftedTarget();

        _highlightTarget = highlightTarget;
        _gateOutside = step.gateInputOutsideHighlight;
        _blockAll = step.blockAllUIInteraction;

        // Dimmer
        if (dimmer != null)
        {
            dimmer.enabled = step.showDimmerImage;
            dimmer.gameObject.SetActive(step.showDimmerImage);
            dimmer.raycastTarget = step.showDimmerImage;
        }

        // Block / restore UI with whitelist of highlight target
        if (_blockAll) DisableUIRaycasts(_highlightTarget);
        else RestoreUIRaycasts();

        // Fullscreen image (optional)
        if (fullscreenImage)
        {
            if (step.fullscreenSprite != null)
            {
                fullscreenImage.sprite = step.fullscreenSprite;
                fullscreenImage.enabled = true;
                fullscreenImage.gameObject.SetActive(true);
            }
            else
            {
                fullscreenImage.sprite = null;
                fullscreenImage.enabled = false;
                fullscreenImage.gameObject.SetActive(false);
            }
        }

        // Bubble text + typewriter
        if (bubbleText)
        {
            // Apply text color override or restore default
            bubbleText.color = (step.overrideTextColor ? step.textColor : _defaultBubbleColor);

            if (!string.IsNullOrEmpty(step.text))
            {
                bubbleText.font = step.textFont != null ? step.textFont : _defaultBubbleFont;
                bubbleText.text = step.text;
                bubbleText.enabled = true;
                bubbleText.gameObject.SetActive(true);

                float cps = step.typewriterCharsPerSecond == 0f
                    ? defaultTypewriterCharsPerSecond
                    : step.typewriterCharsPerSecond;
                StartTypewriter(cps);

                if (textBackdropImage)
                {
                    if (step.textBackdropSprite != null)
                    {
                        textBackdropImage.sprite = step.textBackdropSprite;
                        textBackdropImage.enabled = true;
                        textBackdropImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        textBackdropImage.sprite = null;
                        textBackdropImage.enabled = false;
                        textBackdropImage.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                bubbleText.font = _defaultBubbleFont;
                bubbleText.text = "";
                bubbleText.enabled = false;
                bubbleText.gameObject.SetActive(false);
                SkipTypewriter();

                if (textBackdropImage)
                {
                    textBackdropImage.sprite = null;
                    textBackdropImage.enabled = false;
                    textBackdropImage.gameObject.SetActive(false);
                }
            }
        }

        // Avatar
        if (bubbleSpeaker)
        {
            if (step.speaker != null)
            {
                bubbleSpeaker.sprite = step.speaker;
                bubbleSpeaker.enabled = true;
                bubbleSpeaker.gameObject.SetActive(true);
            }
            else
            {
                bubbleSpeaker.sprite = null;
                bubbleSpeaker.enabled = false;
                bubbleSpeaker.gameObject.SetActive(false);
            }
        }

        PlaceBubble(step.anchor);

        // Finger hint
        finger?.Hide();
        if (step.showFinger && highlightTarget != null && finger != null)
        {
            if (step.fingerMode == FingerMode.Tap)
            {
                finger.ShowTapAt(highlightTarget, step.fingerOffset);
            }
            else if (step.fingerMode == FingerMode.Drag)
            {
                RectTransform to = ResolveRect(step.dragTargetPath);
                if (to != null)
                    finger.ShowDrag(highlightTarget, to, step.fingerOffset, Mathf.Max(0.1f, step.dragDuration), step.dragLoop);
            }
        }

        // Lift target above the dimmer so it remains clickable
        if (bringTargetAboveDimmer && highlightTarget != null)
            LiftTargetCanvas(highlightTarget);

        // Ensure skip button visibility each step (in case you toggle showSkipButton at runtime)
        if (skipButton) skipButton.gameObject.SetActive(showSkipButton);

        EnsureFingerOnTop();
    }

    private void PlaceBubble(TutorialBubbleAnchor anchor)
    {
        if (bubble == null || rootCanvas == null) return;

        bubble.pivot = anchor switch
        {
            TutorialBubbleAnchor.TopLeft => new Vector2(0f, 1f),
            TutorialBubbleAnchor.TopMiddle => new Vector2(0.5f, 1f),
            TutorialBubbleAnchor.TopRight => new Vector2(1f, 1f),
            TutorialBubbleAnchor.MiddleLeft => new Vector2(0f, 0.5f),
            TutorialBubbleAnchor.Centre => new Vector2(0.5f, 0.5f),
            TutorialBubbleAnchor.MiddleRight => new Vector2(1f, 0.5f),
            TutorialBubbleAnchor.BottomLeft => new Vector2(0f, 0f),
            TutorialBubbleAnchor.BottomMiddle => new Vector2(0.5f, 0f),
            TutorialBubbleAnchor.BottomRight => new Vector2(1f, 0f),
            _ => bubble.pivot
        };

        bubble.anchorMin = bubble.pivot;
        bubble.anchorMax = bubble.pivot;

        Vector2 offset = anchor switch
        {
            TutorialBubbleAnchor.TopLeft => new Vector2(bubbleEdgePadding.x, -bubbleEdgePadding.y),
            TutorialBubbleAnchor.TopMiddle => new Vector2(0, -bubbleEdgePadding.y),
            TutorialBubbleAnchor.TopRight => new Vector2(-bubbleEdgePadding.x, -bubbleEdgePadding.y),
            TutorialBubbleAnchor.MiddleLeft => new Vector2(bubbleEdgePadding.x, 0),
            TutorialBubbleAnchor.Centre => Vector2.zero,
            TutorialBubbleAnchor.MiddleRight => new Vector2(-bubbleEdgePadding.x, 0),
            TutorialBubbleAnchor.BottomLeft => new Vector2(bubbleEdgePadding.x, bubbleEdgePadding.y),
            TutorialBubbleAnchor.BottomMiddle => new Vector2(0, bubbleEdgePadding.y),
            TutorialBubbleAnchor.BottomRight => new Vector2(-bubbleEdgePadding.x, bubbleEdgePadding.y),
            _ => Vector2.zero
        };

        bubble.anchoredPosition = offset;
    }

    private IEnumerator TypewriterRoutine(float charsPerSecond)
    {
        // Ensure bubbleText exists
        if (bubbleText == null)
        {
            yield break;
        }

        bubbleText.ForceMeshUpdate();
        int total = bubbleText.textInfo.characterCount;
        float shown = 0f;

        while (shown < total)
        {
            shown += charsPerSecond * Time.unscaledDeltaTime;
            int count = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
            bubbleText.maxVisibleCharacters = count;
            yield return null;
        }

        bubbleText.maxVisibleCharacters = int.MaxValue;
        _typewriterRoutine = null;
        // end of coroutine
    }

    private void LiftTargetCanvas(RectTransform target)
    {
        if (rootCanvas == null) return;

        _liftedTarget = target;
        _liftedCanvas = target.GetComponent<Canvas>();
        _liftedCanvasWasAdded = false;
        _addedRaycaster = null;

        if (_liftedCanvas == null)
        {
            _liftedCanvas = target.gameObject.AddComponent<Canvas>();
            _liftedCanvasWasAdded = true;
        }
        else
        {
            _prevOverrideSorting = _liftedCanvas.overrideSorting;
            _prevSortingOrder = _liftedCanvas.sortingOrder;
        }

        _liftedCanvas.overrideSorting = true;
        int overlayOrder = rootCanvas.sortingOrder;
        _liftedCanvas.sortingOrder = overlayOrder + 1;
        _liftedCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        _liftedCanvas.renderMode = rootCanvas.renderMode;
        _liftedCanvas.worldCamera = rootCanvas.worldCamera;

        var existingRaycaster = target.GetComponent<GraphicRaycaster>();
        if (existingRaycaster == null)
        {
            _addedRaycaster = target.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void RestoreLiftedTarget()
    {
        if (_liftedTarget == null || _liftedCanvas == null)
        {
            _liftedTarget = null;
            return;
        }

        if (_addedRaycaster != null)
        {
            Destroy(_addedRaycaster);
            _addedRaycaster = null;
        }

        if (_liftedCanvasWasAdded)
        {
            Destroy(_liftedCanvas);
        }
        else
        {
            _liftedCanvas.overrideSorting = _prevOverrideSorting;
            _liftedCanvas.sortingOrder = _prevSortingOrder;
        }

        _liftedTarget = null;
        _liftedCanvas = null;
        _liftedCanvasWasAdded = false;
    }

    private void EnsureFingerOnTop()
    {
        if (finger == null || rootCanvas == null) return;

        var go = finger.gameObject;
        _fingerCanvas = go.GetComponent<Canvas>();
        _fingerCanvasWasAdded = false;

        if (_fingerCanvas == null)
        {
            _fingerCanvas = go.AddComponent<Canvas>();
            _fingerCanvasWasAdded = true;
        }

        _fingerCanvas.renderMode = rootCanvas.renderMode;
        _fingerCanvas.worldCamera = rootCanvas.worldCamera;
        _fingerCanvas.overrideSorting = true;

        int overlayOrder = rootCanvas.sortingOrder;
        _fingerPrevOverrideSorting = _fingerCanvas.overrideSorting;
        _fingerPrevSortingOrder = _fingerCanvas.sortingOrder;

        _fingerCanvas.sortingOrder = overlayOrder + 2;

        foreach (var img in go.GetComponentsInChildren<Image>(true))
            img.raycastTarget = false;
        foreach (var txt in go.GetComponentsInChildren<TMP_Text>(true))
            txt.raycastTarget = false;
    }

    private void RestoreFingerCanvas()
    {
        if (_fingerCanvas == null) return;

        if (_fingerCanvasWasAdded)
        {
            Destroy(_fingerCanvas);
        }
        else
        {
            _fingerCanvas.overrideSorting = _fingerPrevOverrideSorting;
            _fingerCanvas.sortingOrder = _fingerPrevSortingOrder;
        }

        _fingerCanvas = null;
        _fingerCanvasWasAdded = false;
    }

    void LateUpdate()
    {
        if (_liftedTarget != null && _liftedCanvas != null && rootCanvas != null)
        {
            if (!_liftedCanvas.overrideSorting) _liftedCanvas.overrideSorting = true;
            int overlayOrder = rootCanvas.sortingOrder;
            if (_liftedCanvas.sortingOrder <= overlayOrder)
                _liftedCanvas.sortingOrder = overlayOrder + 1;
            _liftedCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            _liftedCanvas.renderMode = rootCanvas.renderMode;
            _liftedCanvas.worldCamera = rootCanvas.worldCamera;
        }
    }

    // Use the dimmer's Image + ICanvasRaycastFilter to block outside highlight.
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // Only apply gating when dimmer is visible and gating is requested.
        if (dimmer == null || !dimmer.enabled || !_gateOutside || _highlightTarget == null)
            return true; // when dimmer is shown without gating, block everywhere

        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rootCanvas.transform, sp, eventCamera, out var local);
        RectTransform canvasRect = (RectTransform)rootCanvas.transform;

        Rect highlight = GetRectInCanvasSpace(_highlightTarget, canvasRect);
        Vector2 canvasSpacePoint = local + canvasRect.rect.size * 0.5f;

        // Return true to block outside; false to let events pass inside highlight
        return !highlight.Contains(canvasSpacePoint);
    }

    private void DisableUIRaycasts(RectTransform whitelistTarget)
    {
        if (uiCanvasesToBlock == null) return;

        foreach (var c in uiCanvasesToBlock)
        {
            if (c == null) continue;

            foreach (var g in c.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;
                // Skip tutorial overlay itself
                if (transform != null && g.transform.IsChildOf(transform)) continue;

                // Whitelist highlight target (and its children)
                if (whitelistTarget != null &&
                    (g.transform == whitelistTarget ||
                     g.transform.IsChildOf(whitelistTarget)))
                {
                    // Do not modify; leave interactable
                    continue;
                }

                if (!_prevGraphicRaycast.ContainsKey(g))
                    _prevGraphicRaycast[g] = g.raycastTarget;
                g.raycastTarget = false;

                var sel = g.GetComponent<Selectable>();
                if (sel != null && !_prevSelectableInteractable.ContainsKey(sel))
                {
                    _prevSelectableInteractable[sel] = sel.interactable;
                    sel.interactable = false;
                }
            }
        }
    }

    private void RestoreUIRaycasts()
    {
        if (_prevGraphicRaycast.Count > 0)
        {
            foreach (var kv in _prevGraphicRaycast)
                if (kv.Key != null) kv.Key.raycastTarget = kv.Value;
            _prevGraphicRaycast.Clear();
        }

        if (_prevSelectableInteractable.Count > 0)
        {
            foreach (var kv in _prevSelectableInteractable)
                if (kv.Key != null) kv.Key.interactable = kv.Value;
            _prevSelectableInteractable.Clear();
        }
    }

    private Rect GetRectInCanvasSpace(RectTransform target, RectTransform canvasRect)
    {
        // Fallback if input is invalid
        if (target == null || canvasRect == null)
        {
            return new Rect(Vector2.zero, Vector2.zero);
        }

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3[] canvasCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(rootCanvas != null ? rootCanvas.worldCamera : null, corners[i]),
                rootCanvas != null ? rootCanvas.worldCamera : null,
                out var lp);
            canvasCorners[i] = lp;
        }

        // Compute min/max
        Vector2 min = canvasCorners[0];
        Vector2 max = canvasCorners[0];
        for (int i = 1; i < 4; i++)
        {
            min = Vector2.Min(min, canvasCorners[i]);
            max = Vector2.Max(max, canvasCorners[i]);
        }

        Vector2 size = max - min;
        // Convert from local space (center at (0,0)) to Rect with position relative to canvas center
        Vector2 origin = min + canvasRect.rect.size * 0.5f;
        return new Rect(origin, size);
    }

    private RectTransform ResolveRect(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName)) return null;
        var go = GameObject.Find(pathOrName);
        return go != null ? go.GetComponent<RectTransform>() : null;
    }
}