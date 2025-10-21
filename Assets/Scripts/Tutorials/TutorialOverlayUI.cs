using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialOverlayUI : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("Overlay")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image dimmer;

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

    private RectTransform _highlightTarget;
    private bool _gateOutside;

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

    void Awake()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        Show(false);
    }

    public void Show(bool visible)
    {
        if (deactivateRootOnHide && !visible)
        {
            // Restore any lifted target/finger before hiding
            RestoreLiftedTarget();
            RestoreFingerCanvas();
            gameObject.SetActive(false);
            return;
        }

        if (!visible)
        {
            // Restore any lifted target/finger before hiding
            RestoreLiftedTarget();
            RestoreFingerCanvas();
        }

        if (dimmer) dimmer.gameObject.SetActive(visible);
        if (bubble) bubble.gameObject.SetActive(visible);
        if (bubbleSpeaker) bubbleSpeaker.gameObject.SetActive(visible);
        if (bubbleText) bubbleText.gameObject.SetActive(visible);

        if (finger)
        {
            if (!visible) finger.Hide();
            finger.gameObject.SetActive(visible);
        }

        _highlightTarget = null;
        _gateOutside = false;
        enabled = true;
    }

    public void ConfigureStep(TutorialStep step, RectTransform highlightTarget)
    {
        // Switching target: restore previous lifted state
        if (_liftedTarget != null && _liftedTarget != highlightTarget)
            RestoreLiftedTarget();

        _highlightTarget = highlightTarget;
        _gateOutside = step.gateInputOutsideHighlight;

        if (bubbleText) bubbleText.text = step.text ?? "";

        if (bubbleSpeaker)
        {
            bubbleSpeaker.sprite = step.speaker;
            bubbleSpeaker.enabled = step.speaker != null;
            bubbleSpeaker.gameObject.SetActive(step.speaker != null);
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

        // Lift the target above dimmer so it renders on top
        if (bringTargetAboveDimmer && highlightTarget != null)
            LiftTargetCanvas(highlightTarget);

        // Ensure finger renders above the lifted target and overlay
        EnsureFingerOnTop();
    }

    private void PlaceBubble(TutorialBubbleAnchor anchor)
    {
        if (bubble == null || rootCanvas == null) return;

        // Set the pivot based on the anchor
        bubble.pivot = anchor switch
        {
            TutorialBubbleAnchor.TopLeft => new Vector2(0f, 1f),
            TutorialBubbleAnchor.TopRight => new Vector2(1f, 1f),
            TutorialBubbleAnchor.BottomLeft => new Vector2(0f, 0f),
            TutorialBubbleAnchor.BottomRight => new Vector2(1f, 0f),
            TutorialBubbleAnchor.Middle => new Vector2(0.5f, 0.5f), // Center pivot
            _ => bubble.pivot
        };

        // Set the anchors to the same position as the pivot
        bubble.anchorMin = bubble.pivot;
        bubble.anchorMax = bubble.pivot;

        // Apply padding to keep the bubble within the screen bounds
        Vector2 offset = anchor switch
        {
            TutorialBubbleAnchor.TopLeft => new Vector2(bubbleEdgePadding.x, -bubbleEdgePadding.y),
            TutorialBubbleAnchor.TopRight => new Vector2(-bubbleEdgePadding.x, -bubbleEdgePadding.y),
            TutorialBubbleAnchor.BottomLeft => new Vector2(bubbleEdgePadding.x, bubbleEdgePadding.y),
            TutorialBubbleAnchor.BottomRight => new Vector2(-bubbleEdgePadding.x, bubbleEdgePadding.y),
            TutorialBubbleAnchor.Middle => Vector2.zero, // No offset for the middle
            _ => Vector2.zero
        };

        // Set the anchored position with the offset
        bubble.anchoredPosition = offset;
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
            // Ensure it can receive clicks
            _addedRaycaster = target.gameObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            _prevOverrideSorting = _liftedCanvas.overrideSorting;
            _prevSortingOrder = _liftedCanvas.sortingOrder;
        }

        _liftedCanvas.overrideSorting = true;
        // Ensure it's above the overlay's sorting order
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

    // Put the finger on its own top-most sub-canvas (above lifted target)
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

        // Keep same render pipeline/camera
        _fingerCanvas.renderMode = rootCanvas.renderMode;
        _fingerCanvas.worldCamera = rootCanvas.worldCamera;
        _fingerCanvas.overrideSorting = true;

        // Overlay order (X), target lifted at X+1, finger at X+2
        int overlayOrder = rootCanvas.sortingOrder;
        _fingerPrevOverrideSorting = _fingerCanvas.overrideSorting;
        _fingerPrevSortingOrder = _fingerCanvas.sortingOrder;

        _fingerCanvas.sortingOrder = overlayOrder + 2;

        // Make sure finger never blocks input
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

    // ICanvasRaycastFilter — NOTE the inversion to pass clicks inside highlight
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        if (!_gateOutside || _highlightTarget == null) return false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rootCanvas.transform, sp, eventCamera, out var local);
        RectTransform canvasRect = (RectTransform)rootCanvas.transform;

        Rect highlight = GetRectInCanvasSpace(_highlightTarget, canvasRect);
        Vector2 canvasSpacePoint = local + canvasRect.rect.size * 0.5f;

        // false inside highlight (let click through), true outside (block)
        return !highlight.Contains(canvasSpacePoint);
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