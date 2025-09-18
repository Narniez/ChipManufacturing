using UnityEngine;

public class TouchCameraController : MonoBehaviour
{
    private enum PrimaryGesture { None, Undetermined, Pan, Zoom, Rotate }

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.01f;
    [SerializeField] private float panSmoothTime = 0.15f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.02f;
    [SerializeField] private float minZoomDistance = 5f;
    [SerializeField] private float maxZoomDistance = 40f;
    [SerializeField] private float focusDistance = 10f;
    [SerializeField] private float zoomSmoothTime = 0.15f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 0.15f;
    [SerializeField] private float rotationSmoothTime = 0.15f;

    [Header("Gesture Detection")]
    [SerializeField] private float deadzone = 1.5f;
    [SerializeField] private float pinchThreshold = 3f;
    [SerializeField] private float rotateThreshold = 3f;
    

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private Camera _mainCamera;
    private PrimaryGesture primaryState = PrimaryGesture.None;

    // two-finger tracking
    private Vector2 lastPos0;
    private Vector2 lastPos1;
    private float lastDistance;
    private Vector2 lastMid;

    // single-finger panning
    private Vector2 lastSinglePos;
    private bool singlePanActive = false;

    // Smoothing fields
    private Vector3 targetPosition;
    private Vector3 positionVelocity;
    private Quaternion targetRotation;
    private float targetZoomDistance;

    // Lock while a two-finger gesture is active (prevents pan until both fingers are lifted)
    private bool isTwoFingerGestureActive = false;

    void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("TouchCameraController: No main camera found in the scene.");
            enabled = false;
        }
        targetPosition = _mainCamera.transform.position;
        targetRotation = _mainCamera.transform.rotation;
        targetZoomDistance = focusDistance;
    }

    void Update()
    {
        // If a two-finger gesture is active, ignore single-finger inputs until both fingers are released
        if (isTwoFingerGestureActive)
        {
            if (Input.touchCount >= 2)
            {
                ProcessTwoFinger(); // allow simultaneous zoom + rotate, no pan
            }
            else if (Input.touchCount == 1)
            {
                // Ignore one-finger input while two-finger gesture is locked
                // Sync lastSinglePos so pan won't jump when we resume later
                lastSinglePos = Input.GetTouch(0).position;
            }
            else
            {
                // Both fingers lifted -> end the two-finger gesture
                isTwoFingerGestureActive = false;
                primaryState = PrimaryGesture.None;
                positionVelocity = Vector3.zero;
                targetPosition = _mainCamera.transform.position;
                targetRotation = _mainCamera.transform.rotation;
            }
        }
        else
        {
            // Not locked: route input
            if (Input.touchCount >= 2)
            {
                isTwoFingerGestureActive = true; // lock until both lifted
                ProcessTwoFinger();
            }
            else if (Input.touchCount == 1)
            {
                ProcessOneFinger();
            }
            else
            {
                if (primaryState != PrimaryGesture.None)
                {
                    if (debug) Debug.Log("Gesture ended -> reset");
                    primaryState = PrimaryGesture.None;
                }
                singlePanActive = false;

                // No touch: freeze smoothing to current
                positionVelocity = Vector3.zero;
                targetPosition = _mainCamera.transform.position;
                targetRotation = _mainCamera.transform.rotation;
            }
        }

        // Smooth position
        _mainCamera.transform.position = Vector3.SmoothDamp(
            _mainCamera.transform.position,
            targetPosition,
            ref positionVelocity,
            panSmoothTime);

        // Smooth rotation
        _mainCamera.transform.rotation = Quaternion.Slerp(
            _mainCamera.transform.rotation,
            targetRotation,
            Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, rotationSmoothTime)));
    }

    /// <summary>
    /// Single-finger panning. Moves targetPosition based on the finger delta.
    /// </summary>
    private void ProcessOneFinger()
    {
        Touch t = Input.GetTouch(0);
        Vector2 cur = t.position;

        if (t.phase == TouchPhase.Began)
        {
            lastSinglePos = cur;
            singlePanActive = false;
            return;
        }

        Vector2 delta = cur - lastSinglePos;

        if (delta.sqrMagnitude > deadzone * deadzone)
        {
            HandlePan(delta);
            singlePanActive = true;

            if (debug) Debug.Log($"Single-finger pan delta: {delta}");
        }
        else
        {
            singlePanActive = false;
        }

        lastSinglePos = cur;
    }

    /// <summary>
    /// Two-finger gestures: allow simultaneous zoom and rotate; never pan.
    /// </summary>
    private void ProcessTwoFinger()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        Vector2 cur0 = t0.position;
        Vector2 cur1 = t1.position;
        Vector2 mid = (cur0 + cur1) * 0.5f;
        float curDist = Vector2.Distance(cur0, cur1);

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began || primaryState == PrimaryGesture.None)
        {
            primaryState = PrimaryGesture.Undetermined; // used only for debug/flow
            lastPos0 = cur0;
            lastPos1 = cur1;
            lastDistance = curDist;
            lastMid = mid;
            return;
        }

        Vector2 d0 = cur0 - lastPos0;
        Vector2 d1 = cur1 - lastPos1;

        float distanceDelta = curDist - lastDistance; 
        float pinchScore = Mathf.Abs(distanceDelta);

        float rotateScore = 0f;
        bool verticalOpposite = (Mathf.Sign(d0.y) != Mathf.Sign(d1.y)) &&
                                (Mathf.Abs(d0.y) > 0 && Mathf.Abs(d1.y) > 0);
        if (verticalOpposite)
            rotateScore = (Mathf.Abs(d0.y) + Mathf.Abs(d1.y)) * 0.5f;

        // Lock state for debugging visibility only
        if (primaryState == PrimaryGesture.Undetermined)
        {
            if (pinchScore > pinchThreshold && rotateScore > rotateThreshold)
            {
                if (debug) Debug.Log("Locked primary gesture: Zoom + Rotate");
            }
            else if (pinchScore > pinchThreshold)
            {
                if (debug) Debug.Log("Locked primary gesture: Zoom");
            }
            else if (rotateScore > rotateThreshold)
            {
                if (debug) Debug.Log("Locked primary gesture: Rotate");
            }
            primaryState = PrimaryGesture.Zoom; // mark as two-finger active; we still apply both below
        }

        // Apply both simultaneously if above thresholds; never pan on two-finger
        if (pinchScore > pinchThreshold)
        {
            HandleZoom(distanceDelta);
        }
        if (rotateScore > rotateThreshold)
        {
            float rotationAmount = (d0.y - d1.y) * 0.5f;
            HandleRotation(rotationAmount);
        }

        // Update history
        lastPos0 = cur0;
        lastPos1 = cur1;
        lastDistance = curDist;
        lastMid = mid;
    }

    /// <summary>
    /// Handles camera panning. avgDelta is a screen-space pixel delta.
    /// </summary>
    private void HandlePan(Vector2 avgDelta)
    {
        // Convert screen-space delta into world movement relative to camera orientation.
        Vector3 right = _mainCamera.transform.right;
        Vector3 forward = _mainCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // Pixel-to-world scaling: panSpeed determines how responsive the camera is to screen movement.
        Vector3 move = (-right * avgDelta.x - forward * avgDelta.y) * panSpeed;
        targetPosition += move;
    }

    /// <summary>
    /// Handles camera zooming (two-finger pinch).
    /// </summary>
    private void HandleZoom(float distanceDelta)
    {
        Vector3 move = _mainCamera.transform.forward * (distanceDelta * zoomSpeed);
        Vector3 newPos = new Vector3(targetPosition.x, Mathf.Clamp(targetPosition.y, minZoomDistance, maxZoomDistance), targetPosition.z) + move;

        Vector3 focusPoint = targetPosition + _mainCamera.transform.forward * focusDistance;
        float newDistance = Vector3.Distance(newPos, focusPoint);

        if (newDistance >= minZoomDistance && newDistance <= maxZoomDistance)
            targetPosition = newPos;
    }

    /// <summary>
    /// Handles camera rotation (two-finger opposite vertical movement).
    /// </summary>
    private void HandleRotation(float rotationDelta)
    {
        Vector3 focusPoint = targetPosition + _mainCamera.transform.forward * focusDistance;
        Quaternion rot = Quaternion.AngleAxis(rotationDelta * rotationSpeed, Vector3.up);

        // Rotate around focus point (changes position) and apply orientation
        Vector3 dir = targetPosition - focusPoint;
        dir = rot * dir;
        targetPosition = focusPoint + dir;
        targetRotation = rot * targetRotation;
    }
}
