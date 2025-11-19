using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(MeshRenderer))]
public class PlateManipulator : MonoBehaviour
{
    [Header("Movement")]
    public float translateSpeed = 0.01f;
    public float rotationSpeed = 0.3f; // unused now but kept for UI consistency
    public bool useClampArea = false;
    public Vector2 clampMinXZ = new Vector2(-5, -5);
    public Vector2 clampMaxXZ = new Vector2(5, 5);

    [Header("Smoothing")]
    [Range(0, 1)] public float moveSmooth = 0.15f;
    [Range(0, 1)] public float rotSmooth = 0.15f;

    [Header("Interaction")]
    public bool ignoreUI = true;
    public bool singleTouchMoves = true;

    [Header("Y Lock")]
    public bool lockY = true;

    [Header("Zoom & Camera Pan (two-finger)")]
    public bool enableZoom = true;
    [Tooltip("Mouse scroll wheel zoom speed (FOV or ortho size).")]
    public float zoomSpeedScroll = 5f;
    [Tooltip("Pinch zoom speed multiplier.")]
    public float zoomSpeedPinch = 0.3f;
    [Tooltip("Min/Max orthographic size (if camera is orthographic).")]
    public float minOrthoSize = 2f;
    public float maxOrthoSize = 20f;

    [Header("Two-finger latching thresholds")]
    [Tooltip("Minimum pinch distance change (in pixels) to latch pinch mode.")]
    public float pinchStartThresholdPx = 10f;
    [Tooltip("Minimum average 2-finger movement (in pixels) to latch camera vertical pan.")]
    public float panStartThresholdPx = 8f;
    [Tooltip("Small pinch delta required to continue pinch updates (prevents jitter).")]
    public float pinchContinueEpsilonPx = 1.5f;
    [Tooltip("Small movement required to continue pan (prevents jitter).")]
    public float panContinueEpsilonPx = 1.0f;

    [Header("Camera vertical pan")]
    [Tooltip("World units moved vertically per screen pixel of two-finger vertical movement.")]
    public float cameraPanSpeed = 0.01f;

    [Header("UI (Optional)")]
    public TextMeshProUGUI translateSpeedText;
    public TextMeshProUGUI rotationSpeedText;
    public TextMeshProUGUI zoomText;
    public string translateFormat = "Translate: {0:0.000}";
    public string rotationFormat = "Rotate: {0:0.00}";
    public string zoomFormatPerspective = "FOV: {0:0.0}";
    public string zoomFormatOrtho = "Size: {0:0.0}";
    public bool showDebugHUD = false;

    Camera _cam;
    float _yLock;
    Vector3 _targetPos;
    Quaternion _targetRot;

    // Start pose cache
    Vector3 _startPos;
    Quaternion _startRot;
    bool _startCached;

    // Two-finger state
    Vector2 _prevTouchA;
    Vector2 _prevTouchB;
    bool _havePrevTwo;

    // Pinch zoom
    float _prevPinchDist;
    bool _havePrevPinch;

    // Single touch move tracking
    Vector2 _prevSingleTouch;

    // Mouse state
    bool _mouseTranslating;

    // Block single-touch translation until all touches released after a multi-touch gesture
    bool _blockSingleAfterMulti;

    enum InteractionMode { None, Translate, TwoFinger }
    InteractionMode _mode = InteractionMode.None;

    enum TwoFingerMode { None, Pinch, Pan }
    TwoFingerMode _twoFingerMode = TwoFingerMode.None;

    void Awake()
    {
        _cam = Camera.main;
        CacheStartPoseIfNeeded();
        _targetPos = _startPos;
        _targetRot = _startRot;
        if (lockY) _yLock = _startPos.y;
        UpdateUI();
    }

    void CacheStartPoseIfNeeded()
    {
        if (_startCached) return;
        _startPos = transform.position;
        _startRot = transform.rotation;
        _startCached = true;
    }

    public void ResetToStart()
    {
        CacheStartPoseIfNeeded();
        _targetPos = _startPos;
        _targetRot = _startRot;
        transform.position = _startPos;
        transform.rotation = _startRot;
        if (lockY) _yLock = _startPos.y;

        _havePrevTwo = false;
        _havePrevPinch = false;
        _mouseTranslating = false;
        _blockSingleAfterMulti = false;
        _twoFingerMode = TwoFingerMode.None;
        _mode = InteractionMode.None;
    }

    public void SetTranslateSpeed(float value)
    {
        translateSpeed = Mathf.Max(0f, value);
        UpdateUI();
    }

    public void SetRotationSpeed(float value) // kept for UI compatibility
    {
        rotationSpeed = Mathf.Max(0f, value);
        UpdateUI();
    }

    public void SetScrollZoomSpeed(float value)
    {
        zoomSpeedScroll = Mathf.Max(0f, value);
        UpdateUI();
    }

    public void SetPinchZoomSpeed(float value)
    {
        zoomSpeedPinch = Mathf.Max(0f, value);
        UpdateUI();
    }

    void Update()
    {
        if (_cam == null) return;
#if UNITY_EDITOR
        HandleMouse();
#else
        HandleTouch();
#endif
        HandleScrollZoom();
        ApplySmooth();
        UpdateUI();
    }

    void HandleTouch()
    {
        int count = Input.touchCount;

        // If all fingers lifted, clear multi-touch block and state
        if (count == 0)
        {
            _havePrevTwo = false;
            _havePrevPinch = false;
            _blockSingleAfterMulti = false;
            _twoFingerMode = TwoFingerMode.None;
            _mode = InteractionMode.None;
            return;
        }

        if (ignoreUI)
            for (int i = 0; i < count; i++)
                if (IsPointerOverUI(Input.touches[i].fingerId)) return;

        if (count == 1 && singleTouchMoves)
        {
            if (_blockSingleAfterMulti) return;
            _mode = InteractionMode.Translate;
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                _prevSingleTouch = t.position;
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                Vector2 delta = t.position - _prevSingleTouch;
                _prevSingleTouch = t.position;
                TranslateFromScreenDelta(delta);
            }
        }
        else if (count >= 2)
        {
            _blockSingleAfterMulti = true;
            _mode = InteractionMode.TwoFinger;
            var tA = Input.GetTouch(0);
            var tB = Input.GetTouch(1);
            Vector2 a = tA.position;
            Vector2 b = tB.position;
            float currentPinchDist = Vector2.Distance(a, b);

            if (!_havePrevTwo)
            {
                _prevTouchA = a;
                _prevTouchB = b;
                _prevPinchDist = currentPinchDist;
                _havePrevTwo = _havePrevPinch = true;
                _twoFingerMode = TwoFingerMode.None;
                return;
            }

            Vector2 da = a - _prevTouchA;
            Vector2 db = b - _prevTouchB;
            Vector2 avgDelta = 0.5f * (da + db);
            float verticalPixels = avgDelta.y;

            float pinchDelta = currentPinchDist - _prevPinchDist;
            float absPinch = Mathf.Abs(pinchDelta);
            float absPan = Mathf.Abs(verticalPixels);

            if (_twoFingerMode == TwoFingerMode.None)
            {
                if (enableZoom && absPinch > pinchStartThresholdPx)
                    _twoFingerMode = TwoFingerMode.Pinch;
                else if (absPan > panStartThresholdPx)
                    _twoFingerMode = TwoFingerMode.Pan;
            }

            if (_twoFingerMode == TwoFingerMode.Pinch && enableZoom)
            {
                if (absPinch > pinchContinueEpsilonPx)
                    ApplyPinchZoom(pinchDelta);
                _prevPinchDist = currentPinchDist;
            }
            else if (_twoFingerMode == TwoFingerMode.Pan)
            {
                if (absPan > panContinueEpsilonPx)
                {
                    // Two fingers down (verticalPixels < 0) => camera up (+Y)
                    float dy = -verticalPixels * cameraPanSpeed;
                    var pos = _cam.transform.position;
                    pos.y += dy;
                    _cam.transform.position = pos;
                }
            }

            // Update refs
            _prevTouchA = a;
            _prevTouchB = b;
            if (_twoFingerMode == TwoFingerMode.None)
                _prevPinchDist = currentPinchDist;
        }
    }

    void HandleMouse()
    {
        // Left mouse translates (exclusive)
        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreUI && IsPointerOverUI(-1)) return;
            _mouseTranslating = true;
            _mode = InteractionMode.Translate;
        }
        if (Input.GetMouseButtonUp(0))
        {
            _mouseTranslating = false;
            _mode = InteractionMode.None;
        }

        if (_mouseTranslating && _mode == InteractionMode.Translate)
        {
            Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 100f;
            TranslateFromScreenDelta(delta);
        }

        // Scroll zoom
        if (enableZoom)
        {
            HandleScrollZoom();
        }
    }

    void HandleScrollZoom()
    {
        if (!enableZoom) return;
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            ApplyScrollZoom(scroll);
        }
    }

    void ApplyScrollZoom(float scrollDelta)
    {
        if (_cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Clamp(
                _cam.orthographicSize - scrollDelta * zoomSpeedScroll,
                minOrthoSize,
                maxOrthoSize);
        }
    }

    void ApplyPinchZoom(float pinchDelta)
    {
        if (_cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Clamp(
                _cam.orthographicSize - (pinchDelta * zoomSpeedPinch * 0.01f),
                minOrthoSize,
                maxOrthoSize);
        }
    }

    void TranslateFromScreenDelta(Vector2 screenDelta)
    {
        Vector3 right = _cam.transform.right; right.y = 0f; right.Normalize();
        Vector3 fwd = _cam.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 move = (right * screenDelta.x + fwd * screenDelta.y) * translateSpeed;
        _targetPos += move;

        if (lockY) _targetPos.y = _yLock;

        if (useClampArea)
        {
            _targetPos.x = Mathf.Clamp(_targetPos.x, clampMinXZ.x, clampMaxXZ.x);
            _targetPos.z = Mathf.Clamp(_targetPos.z, clampMinXZ.y, clampMaxXZ.y);
        }
    }

    void ApplySmooth()
    {
        transform.position = Vector3.Lerp(transform.position, _targetPos, 1f - moveSmooth);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRot, 1f - rotSmooth);
    }

    bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
#if UNITY_EDITOR
        return EventSystem.current.IsPointerOverGameObject();
#else
        if (fingerId >= 0)
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        return EventSystem.current.IsPointerOverGameObject();
#endif
    }

    void OnGUI()
    {
        if (!showDebugHUD) return;
        const float pad = 8f;
        const float line = 20f;
        Rect r1 = new Rect(pad, pad, 240f, line);
        Rect r2 = new Rect(pad, pad + line, 240f, line);
        Rect r3 = new Rect(pad, pad + line * 2, 240f, line);

        GUI.Label(r1, string.Format(translateFormat, translateSpeed));
        GUI.Label(r2, string.Format(rotationFormat, rotationSpeed));
        GUI.Label(r3, GetZoomString());
    }

    void UpdateUI()
    {
        if (translateSpeedText)
            translateSpeedText.text = string.Format(translateFormat, translateSpeed);
        if (rotationSpeedText)
            rotationSpeedText.text = string.Format(rotationFormat, rotationSpeed);
        if (zoomText)
            zoomText.text = GetZoomString();
    }

    string GetZoomString()
    {
        if (_cam == null) return "";
        return _cam.orthographic
            ? string.Format(zoomFormatOrtho, _cam.orthographicSize)
            : string.Format(zoomFormatPerspective, _cam.fieldOfView);
    }

    void OnDrawGizmosSelected()
    {
        if (!useClampArea) return;
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        Vector3 a = new Vector3(clampMinXZ.x, transform.position.y, clampMinXZ.y);
        Vector3 b = new Vector3(clampMaxXZ.x, transform.position.y, clampMinXZ.y);
        Vector3 c = new Vector3(clampMaxXZ.x, transform.position.y, clampMaxXZ.y);
        Vector3 d = new Vector3(clampMinXZ.x, transform.position.y, clampMaxXZ.y);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
    }
}