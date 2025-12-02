using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialOverlayUI : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("Overlay")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image dimmer;
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
    [SerializeField, Tooltip("If true, Show(false) deactivates this GameObject. Leave OFF to avoid disabling the manager object.")]
    private bool deactivateRootOnHide = false;
    [SerializeField, Tooltip("Temporarily lift the highlighted UI above the dimmer so it renders on top.")]
    private bool bringTargetAboveDimmer = true;

    [Header("Layout")]
    [SerializeField, Tooltip("Padding from the screen edges for the speech bubble (in canvas units).")]
    private Vector2 bubbleEdgePadding = new Vector2(24f, 24f);

    [Header("Typewriter")]
    [SerializeField, Tooltip("Default characters-per-second when a step doesn't override. 0 = instant.")]
    private float defaultTypewriterCharsPerSecond = 40f;

    [Header("Which UI canvases to disable when a step sets Block All UI Interaction")]
    [SerializeField, Tooltip("All Graphics & Selectables under these canvases will be made non-raycast and non-interactable while the step is active.")]
    private Canvas[] uiCanvasesToBlock;

    private RectTransform _highlightTarget;
    private bool _gateOutside;
    private bool _blockAll;

    private TMP_FontAsset _defaultBubbleFont;

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

    // State of temporarily modified UI
    private readonly Dictionary<Graphic, bool> _prevGraphicRaycast = new Dictionary<Graphic, bool>();
    private readonly Dictionary<Selectable, bool> _prevSelectableInteractable = new Dictionary<Selectable, bool>();

    public bool BlocksAllUI => _blockAll; // still exposed, but now used to disable UI raycasts (not a fullscreen catch)

    void Awake()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (bubbleText != null) _defaultBubbleFont = bubbleText.font;

        // Ensure overlay visuals do NOT block camera gestures
        if (dimmer) dimmer.raycastTarget = false;
        if (bubbleSpeaker) bubbleSpeaker.raycastTarget = false;
        if (bubbleText) bubbleText.raycastTarget = false;
        if (fullscreenImage) fullscreenImage.raycastTarget = false;
        if (textBackdropImage) textBackdropImage.raycastTarget = false;

        Show(false);
    }

    public bool IsTyping => _typewriterRoutine != null;

    public void SkipTypewriter()
    {
        if (bubbleText == null) return;
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        bubbleText.maxVisibleCharacters = int.MaxValue;
    }

    public void Show(bool visible)
    {
        if (deactivateRootOnHide && !visible)
        {
            RestoreLiftedTarget();
            RestoreFingerCanvas();
            RestoreUIRaycasts(); // restore anything disabled
            SkipTypewriter();
            if (fullscreenImage) fullscreenImage.gameObject.SetActive(false);
            if (textBackdropImage) textBackdropImage.gameObject.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        if (!visible)
        {
            RestoreLiftedTarget();
            RestoreFingerCanvas();
            RestoreUIRaycasts();
            SkipTypewriter();
            if (fullscreenImage) fullscreenImage.gameObject.SetActive(false);
            if (textBackdropImage) textBackdropImage.gameObject.SetActive(false);
        }

        if (dimmer) dimmer.gameObject.SetActive(visible);
        if (bubble) bubble.gameObject.SetActive(visible);
        if (bubbleSpeaker) bubbleSpeaker.gameObject.SetActive(visible);
        if (bubbleText) bubbleText.gameObject.SetActive(visible);
        if (finger) finger.gameObject.SetActive(visible);
        if (fullscreenImage) fullscreenImage.gameObject.SetActive(false);
        if (textBackdropImage) textBackdropImage.gameObject.SetActive(false);

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

        if (dimmer != null)
            dimmer.enabled = step.showDimmerImage;

        // If we are globally blocking UI, disable raycasts/interactables on configured UI canvases.
        if (_blockAll)
        {
            DisableUIRaycasts();

        }
        else
        {
            RestoreUIRaycasts();
            bool useGating = _gateOutside && highlightTarget != null;
        }

        // Fullscreen image (optional visual)
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
                if (_defaultBubbleFont != null) bubbleText.font = _defaultBubbleFont;
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

        if (bringTargetAboveDimmer && highlightTarget != null)
            LiftTargetCanvas(highlightTarget);

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

    private void StartTypewriter(float charsPerSecond)
    {
        if (bubbleText == null) return;

        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        if (charsPerSecond <= 0f)
        {
            bubbleText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        bubbleText.ForceMeshUpdate();
        bubbleText.maxVisibleCharacters = 0;
        _typewriterRoutine = StartCoroutine(TypewriterRoutine(charsPerSecond));
    }

    private IEnumerator TypewriterRoutine(float charsPerSecond)
    {
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
    }

    // Lift target: add/adjust a Canvas on the target to render above the overlay
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
            _addedRaycaster = target.gameObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            _prevOverrideSorting = _liftedCanvas.overrideSorting;
            _prevSortingOrder = _liftedCanvas.sortingOrder;
        }

        _liftedCanvas.overrideSorting = true;
        _liftedCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        int overlayOrder = rootCanvas.sortingOrder;
        _liftedCanvas.sortingOrder = overlayOrder + 1;
    }

    private void RestoreLiftedTarget()
    {
        if (_liftedTarget == null || _liftedCanvas == null)
        {
            _liftedTarget = null;
            return;
        }

        if (_liftedCanvasWasAdded)
        {
            if (_addedRaycaster != null) Destroy(_addedRaycaster);
            Destroy(_liftedCanvas);
        }
        else
        {
            _liftedCanvas.overrideSorting = _prevOverrideSorting;
            _liftedCanvas.sortingOrder = _prevSortingOrder;
        }

        _liftedTarget = null;
        _liftedCanvas = null;
        _addedRaycaster = null;
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
        // Keep lifted target above the dimmer even if other scripts change Canvas settings
        if (_liftedTarget != null && _liftedCanvas != null && rootCanvas != null)
        {
            if (!_liftedCanvas.overrideSorting) _liftedCanvas.overrideSorting = true;

            int overlayOrder = rootCanvas.sortingOrder;
            if (_liftedCanvas.sortingOrder <= overlayOrder)
                _liftedCanvas.sortingOrder = overlayOrder + 1;

            _liftedCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        }
    }

    // ICanvasRaycastFilter: only used by interactionCatcher during highlight gating
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // If not gating, never block via catcher
        if (!_gateOutside || _highlightTarget == null) return false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rootCanvas.transform, sp, eventCamera, out var local);
        RectTransform canvasRect = (RectTransform)rootCanvas.transform;

        Rect highlight = GetRectInCanvasSpace(_highlightTarget, canvasRect);
        Vector2 canvasSpacePoint = local + canvasRect.rect.size * 0.5f;

        // Inside highlight: false (let through). Outside: true (block)
        return !highlight.Contains(canvasSpacePoint);
    }

    // Disable raycasts and button interactability on selected canvases (but not the overlay)
    private void DisableUIRaycasts()
    {
        if (uiCanvasesToBlock == null) return;

        foreach (var c in uiCanvasesToBlock)
        {
            if (c == null) continue;
            foreach (var g in c.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;
                // Skip anything under the overlay hierarchy if canvases overlap
                if (transform != null && g.transform.IsChildOf(transform)) continue;

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
            {
                if (kv.Key != null) kv.Key.raycastTarget = kv.Value;
            }
            _prevGraphicRaycast.Clear();
        }

        if (_prevSelectableInteractable.Count > 0)
        {
            foreach (var kv in _prevSelectableInteractable)
            {
                if (kv.Key != null) kv.Key.interactable = kv.Value;
            }
            _prevSelectableInteractable.Clear();
        }
    }

    private Rect GetRectInCanvasSpace(RectTransform target, RectTransform canvasRect)
    {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3[] canvasCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, corners[i]),
                rootCanvas.worldCamera,
                out var lp);
            canvasCorners[i] = lp;
        }
        Vector2 min = canvasCorners[0];
        Vector2 max = canvasCorners[2];
        Vector2 size = max - min;
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