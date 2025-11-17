using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(MeshRenderer))]
public class PlateManipulator : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Units moved per screen pixel drag (single finger / mouse).")]
    public float translateSpeed = 0.01f;
    [Tooltip("Degrees rotated per screen pixel (two finger twist / mouse right drag).")]
    public float rotationSpeed = 0.3f;
    [Tooltip("Optional clamp area in world space (XZ). Leave disabled for free movement.")]
    public bool useClampArea = false;
    public Vector2 clampMinXZ = new Vector2(-5, -5);
    public Vector2 clampMaxXZ = new Vector2(5, 5);

    [Header("Smoothing")]
    [Tooltip("Interpolation for movement (0 = instant).")]
    [Range(0, 1)] public float moveSmooth = 0.15f;
    [Tooltip("Interpolation for rotation (0 = instant).")]
    [Range(0, 1)] public float rotSmooth = 0.15f;

    [Header("Interaction")]
    [Tooltip("Ignore touches that begin over UI.")]
    public bool ignoreUI = true;
    [Tooltip("Allow translation only with single touch / left mouse.")]
    public bool singleTouchMoves = true;
    [Tooltip("Allow rotation with two-finger twist or right mouse drag.")]
    public bool multiTouchRotates = true;

    [Header("Y Lock")]
    [Tooltip("Keep plate Y constant (recommended).")]
    public bool lockY = true;

    Camera _cam;
    float _yLock;
    Vector3 _targetPos;
    Quaternion _targetRot;

    // For two-finger rotation
    Vector2 _prevTouchA;
    Vector2 _prevTouchB;
    bool _havePrevTwo;

    // For single-finger movement (screen position tracking)
    Vector2 _prevSingleTouch;

    // Mouse helpers
    bool _mouseTranslating;
    bool _mouseRotating;

    void Awake()
    {
        _cam = Camera.main;
        _targetPos = transform.position;
        _targetRot = transform.rotation;
        if (lockY) _yLock = transform.position.y;
    }

    void Update()
    {
        if (_cam == null) return;
#if UNITY_EDITOR
        HandleMouse();
#else
        HandleTouch();
#endif
        ApplySmooth();
    }

    void HandleTouch()
    {
        int count = Input.touchCount;
        if (count == 0)
        {
            _havePrevTwo = false;
            return;
        }

        // Ignore touches that start over UI
        if (ignoreUI)
        {
            for (int i = 0; i < count; i++)
                if (IsPointerOverUI(Input.touches[i].fingerId)) return;
        }

        if (count == 1 && singleTouchMoves)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                _prevSingleTouch = t.position;
            }
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                Vector2 delta = t.position - _prevSingleTouch;
                _prevSingleTouch = t.position;
                TranslateFromScreenDelta(delta);
            }
        }
        else if (count >= 2 && multiTouchRotates)
        {
            var tA = Input.GetTouch(0);
            var tB = Input.GetTouch(1);

            Vector2 a = tA.position;
            Vector2 b = tB.position;

            if (!_havePrevTwo)
            {
                _prevTouchA = a;
                _prevTouchB = b;
                _havePrevTwo = true;
                return;
            }

            // Rotation angle (signed) between previous and current segment AB
            float anglePrev = Mathf.Atan2((_prevTouchB - _prevTouchA).y, (_prevTouchB - _prevTouchA).x);
            float angleNow = Mathf.Atan2((b - a).y, (b - a).x);
            float deltaAngleDeg = Mathf.DeltaAngle(anglePrev * Mathf.Rad2Deg, angleNow * Mathf.Rad2Deg);

            // Optional translation: average center movement
            Vector2 centerPrev = (_prevTouchA + _prevTouchB) * 0.5f;
            Vector2 centerNow = (a + b) * 0.5f;
            Vector2 centerDelta = centerNow - centerPrev;

            RotateBy(deltaAngleDeg);
            TranslateFromScreenDelta(centerDelta);

            _prevTouchA = a;
            _prevTouchB = b;
        }
    }

    void HandleMouse()
    {
        // Left button translates
        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreUI && IsPointerOverUI(-1)) return;
            _mouseTranslating = true;
        }
        if (Input.GetMouseButtonUp(0)) _mouseTranslating = false;

        // Right button rotates
        if (Input.GetMouseButtonDown(1))
        {
            if (ignoreUI && IsPointerOverUI(-1)) return;
            _mouseRotating = true;
        }
        if (Input.GetMouseButtonUp(1)) _mouseRotating = false;

        if (_mouseTranslating)
        {
            Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 100f;
            TranslateFromScreenDelta(delta);
        }

        if (_mouseRotating)
        {
            float deltaAngle = Input.GetAxis("Mouse X") * 10f; // horizontal drag rotates
            RotateBy(deltaAngle);
        }
    }

    void TranslateFromScreenDelta(Vector2 screenDelta)
    {
        // Convert screen delta to world XZ displacement based on camera orientation
        // Use camera right & forward projected onto XZ plane
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

    void RotateBy(float deltaAngleDeg)
    {
        _targetRot = Quaternion.Euler(0f, deltaAngleDeg * rotationSpeed, 0f) * _targetRot;
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

    // Optional gizmo to visualize clamp area
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