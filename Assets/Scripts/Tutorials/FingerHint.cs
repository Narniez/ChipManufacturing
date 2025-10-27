using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class FingerHint : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image image;

    [Header("Tap Animation")]
    [SerializeField] private Sprite fingerUp;
    [SerializeField] private Sprite fingerDown;
    [SerializeField, Tooltip("Seconds for one tap cycle (up->down->up).")]
    private float tapCycle = 0.8f;
    [SerializeField, Tooltip("Scale pulse during tap.")]
    private Vector2 tapScale = new Vector2(0.95f, 1.05f);

    [Header("Drag Animation")]
    [SerializeField, Tooltip("Seconds to move from start to end.")]
    private float dragDuration = 1.25f;
    [SerializeField, Tooltip("Pause between drags.")]
    private float dragPause = 0.3f;

    private RectTransform _rt;
    private RectTransform _from;
    private RectTransform _to;
    private Vector2 _offset;
    private Coroutine _routine;

    void Awake()
    {
        _rt = (RectTransform)transform;
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (image == null) image = GetComponentInChildren<Image>(true);
        Hide();
    }

    public void Hide()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        gameObject.SetActive(false);
        _from = _to = null;
    }

    // Point/tap at a single target
    public void ShowTapAt(RectTransform target, Vector2 offset)
    {
        if (target == null || rootCanvas == null) { Hide(); return; }
        _from = target; _to = null; _offset = offset;
        if (_routine != null) StopCoroutine(_routine);
        gameObject.SetActive(true);
        _routine = StartCoroutine(TapLoop());
    }

    // Drag from 'from' to 'to' and loop
    public void ShowDrag(RectTransform from, RectTransform to, Vector2 offset, float duration, bool loop)
    {
        if (from == null || to == null || rootCanvas == null) { Hide(); return; }
        _from = from; _to = to; _offset = offset;
        dragDuration = Mathf.Max(0.1f, duration);
        if (_routine != null) StopCoroutine(_routine);
        gameObject.SetActive(true);
        _routine = StartCoroutine(DragLoop(loop));
    }

    private IEnumerator TapLoop()
    {
        while (true)
        {
            // Follow target
            SetAnchoredToTarget(_from, _offset);

            // Up -> Down -> Up within tapCycle
            float half = tapCycle * 0.5f;
            // Down phase
            if (image) image.sprite = fingerDown != null ? fingerDown : image.sprite;
            yield return ScaleOver(half, tapScale.x);
            // Up phase
            if (image) image.sprite = fingerUp != null ? fingerUp : image.sprite;
            yield return ScaleOver(half, tapScale.y);
        }
    }

    private IEnumerator DragLoop(bool loop)
    {
        do
        {
            // Place at start
            SetAnchoredToTarget(_from, _offset);
            if (image) image.sprite = fingerDown != null ? fingerDown : image.sprite;

            // Lerp to end
            Vector2 start = _rt.anchoredPosition;
            Vector2 end = ToAnchored(_to) + _offset;

            float t = 0f;
            while (t < dragDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dragDuration);
                _rt.anchoredPosition = Vector2.Lerp(start, end, EaseOutQuad(k));
                yield return null;
            }

            if (dragPause > 0f) yield return new WaitForSeconds(dragPause);
        }
        while (loop);
    }

    private IEnumerator ScaleOver(float duration, float targetScale)
    {
        float t = 0f;
        Vector3 start = Vector3.one;
        Vector3 end = new Vector3(targetScale, targetScale, 1f);
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            _rt.localScale = Vector3.Lerp(start, end, EaseOutQuad(k));
            yield return null;
        }
        // Return to 1
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            _rt.localScale = Vector3.Lerp(end, Vector3.one, t / 0.1f);
            yield return null;
        }
    }

    private void SetAnchoredToTarget(RectTransform target, Vector2 offset)
    {
        _rt.anchoredPosition = ToAnchored(target) + offset;
    }

    private Vector2 ToAnchored(RectTransform target)
    {
        var canvasRect = (RectTransform)rootCanvas.transform;
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, center),
            rootCanvas.worldCamera,
            out var local);
        return local;
    }

    private static float EaseOutQuad(float x) => 1 - (1 - x) * (1 - x);
}