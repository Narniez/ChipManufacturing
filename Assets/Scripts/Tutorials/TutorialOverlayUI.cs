using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialOverlayUI : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("Overlay")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image dimmer;
    [SerializeField] private RectTransform holeRect; // optional visual

    [Header("Hint UI")]
    [SerializeField] private FingerHint finger;       
    [SerializeField] private RectTransform bubble;
    [SerializeField] private Image bubbleSpeaker;
    [SerializeField] private TextMeshProUGUI bubbleText;

    private RectTransform _highlightTarget;
    private bool _gateOutside;

    void Awake()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        Show(false);
    }

    public void Show(bool visible)
    {
        gameObject.SetActive(visible);
        if (dimmer) dimmer.enabled = visible;
        if (!visible)
        {
            _highlightTarget = null;
            finger?.Hide();
        }
    }

    public void ConfigureStep(TutorialStep step, RectTransform highlightTarget)
    {
        _highlightTarget = highlightTarget;
        _gateOutside = step.gateInputOutsideHighlight;

        // Bubble
        if (bubbleText) bubbleText.text = step.text ?? "";
        if (bubbleSpeaker)
        {
            bubbleSpeaker.enabled = step.speaker != null;
            bubbleSpeaker.sprite = step.speaker;
        }
        PositionBubble(step.anchor);

        // Finger
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
                if (to != null) finger.ShowDrag(highlightTarget, to, step.fingerOffset, Mathf.Max(0.1f, step.dragDuration), step.dragLoop);
            }
        }
    }

    private void PositionBubble(TutorialBubbleAnchor anchor)
    {
        var canvasRect = (RectTransform)rootCanvas.transform;
        Vector2 anchorPos = anchor switch
        {
            TutorialBubbleAnchor.TopLeft => new Vector2(0.1f, 0.9f),
            TutorialBubbleAnchor.TopRight => new Vector2(0.9f, 0.9f),
            TutorialBubbleAnchor.BottomLeft => new Vector2(0.1f, 0.1f),
            _ => new Vector2(0.9f, 0.1f),
        };
        if (bubble && canvasRect)
        {
            Vector2 world = Vector2.Scale(anchorPos, canvasRect.rect.size) - (canvasRect.rect.size * 0.5f);
            bubble.anchoredPosition = world;
        }
    }

    // Highlight gating
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        if (!_gateOutside || _highlightTarget == null) return true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rootCanvas.transform, sp, eventCamera, out var local);
        RectTransform canvasRect = (RectTransform)rootCanvas.transform;
        Rect highlight = GetRectInCanvasSpace(_highlightTarget, canvasRect);
        Vector2 canvasSpacePoint = local + canvasRect.rect.size * 0.5f;
        return highlight.Contains(canvasSpacePoint);
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