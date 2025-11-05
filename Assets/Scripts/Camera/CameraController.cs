using UnityEngine;

public class CameraController : MonoBehaviour
{
    private enum PrimaryGesture { None, Undetermined, Pan, Zoom, Rotate }

    public enum CameraTestMode
    {
        A_SimultaneousZoomRotate = 0,
        B_ExclusiveZoomOrRotate = 1,
        C_TwoFingerSameDirectionRotate = 2,
        D_OneFingerRotate_TwoFingerPan = 3,
        E_DesktopMouseControls = 4
    }

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.01f;
    [SerializeField] private float panSmoothTime = 0.15f;
    [Tooltip("Pan speed multiplier when at minimum zoom (closer in). 1 = no change, lower slows panning as you zoom in.")]
    [SerializeField][Range(0.05f, 1f)] private float panSpeedScaleAtMinZoom = 0.3f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.02f;
    [SerializeField] private float minZoomDistance = 5f;
    [SerializeField] private float maxZoomDistance = 40f;
    [SerializeField] private float focusDistance = 10f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 0.15f;
    [SerializeField] private float rotationSmoothTime = 0.15f;

    [Header("Gesture Detection")]
    [SerializeField] private float deadzone = 1.5f;
    [SerializeField] private float pinchThreshold = 3f;
    [SerializeField] private float rotateThreshold = 3f;

    [Header("Bounds")]
    [Tooltip("Optional: If set, camera will be clamped to this BoxCollider's world bounds.")]
    [SerializeField] private BoxCollider boundsCollider;
    [Tooltip("Used if no BoxCollider is provided.")]
    [SerializeField] private Vector2 xLimits = new Vector2(-50, 50);
    [SerializeField] private Vector2 zLimits = new Vector2(-50, 50);

    [Header("Mouse Settings (Editor/Desktop)")]
    [Tooltip("Horizontal mouse delta while right-button held is multiplied by this before rotating.")]
    [SerializeField] private float mouseRotateSensitivity = 0.2f;
    [Tooltip("How much pinch 'distanceDelta' a single scroll notch simulates. Higher = stronger zoom.")]
    [SerializeField] private float mouseWheelZoomDelta = 120f;
    [Tooltip("When zooming with the wheel, pivot toward the mouse cursor on the ground plane.")]
    [SerializeField] private bool zoomTowardsMouse = true;

    [Header("Debug/Test")]
    [SerializeField] private bool debug = false;
    [Tooltip("If true, rotation and zoom can happen simultaneously. If false, only one (the dominant) is allowed at a time.")]
    [SerializeField] private bool allowRotationWhenZooming = true;

    [Header("Test Mode")]
    [SerializeField] private CameraTestMode testMode = CameraTestMode.A_SimultaneousZoomRotate;

    private Camera _mainCamera;
    private PrimaryGesture primaryState = PrimaryGesture.None;

    // two-finger tracking
    private Vector2 lastPos0;
    private Vector2 lastPos1;
    private float lastDistance;
    private Vector2 lastMid;

    // single-finger panning
    private Vector2 lastSinglePos;

    // Smoothing fields
    private Vector3 targetPosition;
    private Vector3 positionVelocity;
    private Quaternion targetRotation;

    // Two-finger lock and pivot
    private bool isTwoFingerGestureActive = false;
    private bool hasPivot = false;
    private Vector3 pivotWorld;

    // Start pose for reset
    private Vector3 startPosition;
    private Quaternion startRotation;

    // Mouse controls
    private bool isMouseRotating = false;
    private Vector2 lastMousePos;
    private bool isMousePanning = false;
    private Vector2 lastMousePanPos;

    // External input lock (e.g., while dragging a machine)
    public bool InputLocked { get; private set; }

    // Ignore any gestures until all touches are lifted (prevents drift on unlock)
    private bool blockUntilNoTouchUp = false;

    void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("TouchCameraController: No main camera found in the scene.");
            enabled = false;
        }

        // Record start pose and initialize targets
        startPosition = _mainCamera.transform.position;
        startRotation = _mainCamera.transform.rotation;

        targetPosition = startPosition;
        targetRotation = startRotation;
        ClampTargetToBounds();
    }

    public void SetInputLocked(bool locked)
    {
        InputLocked = locked;
        if (locked)
        {
            // Kill smoothing and clear any in-progress gesture state
            positionVelocity = Vector3.zero;
            primaryState = PrimaryGesture.None;
            isTwoFingerGestureActive = false;
            hasPivot = false;
            // Also block until touches are released; we'll clear on unlock explicitly if needed
            blockUntilNoTouchUp = false;
        }
    }

    // Hard stop any camera smoothing and prevent post-drag drift.
    // If snapToTarget is false, freeze where the camera currently is.
    // If true, snap transform to the pending target immediately.
    public void StopMotion(bool snapToTarget = false)
    {
        positionVelocity = Vector3.zero;
        if (_mainCamera == null) return;

        if (snapToTarget)
        {
            _mainCamera.transform.position = targetPosition;
            _mainCamera.transform.rotation = targetRotation;
        }
        else
        {
            targetPosition = _mainCamera.transform.position;
            targetRotation = _mainCamera.transform.rotation;
        }
    }

    // After an external interaction ends, call this to ignore residual touches
    // until all fingers are lifted, preventing unintended camera motion.
    public void BlockInputUntilNoTouchRelease()
    {
        positionVelocity = Vector3.zero;
        primaryState = PrimaryGesture.None;
        isTwoFingerGestureActive = false;
        hasPivot = false;
        blockUntilNoTouchUp = true;
    }

    //expose mode setter for UI
    public void SetTestMode(CameraTestMode mode)
    {
        testMode = mode;
        if (mode == CameraTestMode.A_SimultaneousZoomRotate || mode == CameraTestMode.C_TwoFingerSameDirectionRotate)
        {
            allowRotationWhenZooming = true;
        }
        else if (mode == CameraTestMode.B_ExclusiveZoomOrRotate)
        {
            allowRotationWhenZooming = false;
        }
        if (debug) Debug.Log($"Camera Test Mode set to: {testMode}");
    }

    // Reset camera to scene start pose
    public void ResetToStart(bool immediate = true)
    {
        // clear gesture state
        isTwoFingerGestureActive = false;
        hasPivot = false;
        primaryState = PrimaryGesture.None;
        positionVelocity = Vector3.zero;
        lastPos0 = lastPos1 = lastMid = Vector2.zero;
        lastDistance = 0f;
        blockUntilNoTouchUp = false;

        // reset targets
        targetPosition = startPosition;
        targetRotation = startRotation;
        ClampTargetToBounds();

        if (immediate && _mainCamera != null)
        {
            _mainCamera.transform.position = targetPosition;
            _mainCamera.transform.rotation = targetRotation;
        }
    }

    // Used by external systems (e.g., edge scroll during drag)
    public void NudgeWorld(Vector3 worldDelta)
    {
        targetPosition += worldDelta;
        ClampTargetToBounds();
    }

    void Update()
    {
        // If we must wait for all touches to be lifted, do so before processing any input
        if (blockUntilNoTouchUp)
        {
            if (Input.touchCount == 0)
            {
                blockUntilNoTouchUp = false;
                // ensure no residual smoothing
                positionVelocity = Vector3.zero;
            }
        }

        if (!InputLocked && !blockUntilNoTouchUp)
        {
            // Desktop mode: only process mouse, skip touch logic entirely
            if (testMode == CameraTestMode.E_DesktopMouseControls)
            {
                ProcessMouseControls();
            }
            else
            {
                if (isTwoFingerGestureActive)
                {
                    if (Input.touchCount >= 2)
                    {
                        ProcessTwoFinger();
                    }
                    else if (Input.touchCount == 1)
                    {
                        lastSinglePos = Input.GetTouch(0).position;
                    }
                    else
                    {
                        // No touch: stop gesture, but DO NOT reset targets
                        isTwoFingerGestureActive = false;
                        hasPivot = false;
                        primaryState = PrimaryGesture.None;
                        positionVelocity = Vector3.zero;
                    }
                }
                else
                {
                    if (Input.touchCount >= 2)
                    {
                        isTwoFingerGestureActive = true;
                        ProcessTwoFinger();
                    }
                    else if (Input.touchCount == 1)
                    {
                        ProcessOneFinger();
                    }
                    else
                    {
                        //stop smoothing velocity
                        if (primaryState != PrimaryGesture.None)
                        {
                            if (debug) Debug.Log("Gesture ended -> reset");
                            primaryState = PrimaryGesture.None;
                        }
                        positionVelocity = Vector3.zero;
                    }
                }
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

    private void ProcessMouseControls()
    {
        // Left mouse button: pan
        if (Input.GetMouseButtonDown(0) && !Input.GetMouseButton(1)) 
        {
            isMousePanning = true;
            lastMousePanPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0) && isMousePanning && !Input.GetMouseButton(1))
        {
            Vector2 cur = (Vector2)Input.mousePosition;
            Vector2 delta = cur - lastMousePanPos;
            if (delta.sqrMagnitude > 0.0001f)
            {
                HandlePan(delta);
            }
            lastMousePanPos = cur;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isMousePanning = false;
        }

        // Right mouse button: rotate (yaw) around a ground-plane pivot under the cursor
        if (Input.GetMouseButtonDown(1))
        {
            isMouseRotating = true;
            lastMousePos = Input.mousePosition;

            // Set pivot under mouse so rotation feels anchored to the scene
            if (TryGetMousePivotWorld((Vector2)lastMousePos, out var pivot))
            {
                pivotWorld = pivot;
                hasPivot = true;
            }
            else
            {
                // fallback pivot in front of camera
                hasPivot = true;
                pivotWorld = targetPosition + (targetRotation * Vector3.forward) * Mathf.Max(0.01f, focusDistance);
            }
        }
        else if (Input.GetMouseButton(1) && isMouseRotating)
        {
            Vector2 cur = Input.mousePosition;
            Vector2 delta = cur - lastMousePos;

            // Use horizontal delta for yaw; scale by sensitivity
            float rotationDelta = delta.x * mouseRotateSensitivity;
            if (Mathf.Abs(rotationDelta) > 0.0001f)
            {
                HandleRotation(rotationDelta);
            }

            lastMousePos = cur;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isMouseRotating = false;
        }

        // Mouse wheel: zoom in/out. Positive scroll zooms in.
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.0001f)
        {
            if (zoomTowardsMouse && TryGetMousePivotWorld((Vector2)Input.mousePosition, out var mousePivot))
            {
                pivotWorld = mousePivot;
                hasPivot = true;
            }
            else if (!hasPivot)
            {
                hasPivot = true;
                pivotWorld = targetPosition + (targetRotation * Vector3.forward) * Mathf.Max(0.01f, focusDistance);
            }

            // Simulate a pinch distanceDelta; HandleZoom applies zoomSpeed scaling and clamping
            float distanceDelta = wheel * mouseWheelZoomDelta;
            HandleZoom(distanceDelta);
        }
    }

    private bool TryGetMousePivotWorld(Vector2 screenPos, out Vector3 world)
    {
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }
        world = default;
        return false;
    }

    private void ProcessOneFinger()
    {
        Touch t = Input.GetTouch(0);
        Vector2 cur = t.position;

        if (t.phase == TouchPhase.Began)
        {
            lastSinglePos = cur;
            return;
        }

        Vector2 delta = cur - lastSinglePos;

        if (delta.sqrMagnitude > deadzone * deadzone)
        {
            if (testMode == CameraTestMode.D_OneFingerRotate_TwoFingerPan)
            {
                // One-finger rotate (use horizontal delta for yaw)
                HandleRotation(delta.x);
            }
            else
            {
                // Default: one-finger pan
                HandlePan(delta);
            }
        }

        lastSinglePos = cur;
    }

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
            primaryState = PrimaryGesture.Undetermined;
            lastPos0 = cur0;
            lastPos1 = cur1;
            lastDistance = curDist;
            lastMid = mid;

            hasPivot = TryGetMidpointPivotWorld(mid, out pivotWorld);
            return;
        }

        Vector2 d0 = cur0 - lastPos0;
        Vector2 d1 = cur1 - lastPos1;

        float distanceDelta = curDist - lastDistance;
        float pinchScore = Mathf.Abs(distanceDelta);

        // Compute rotation score and delta depending on mode
        float rotationAmount;
        float rotateScore = ComputeRotateSignal(d0, d1, out rotationAmount);


        if (testMode == CameraTestMode.D_OneFingerRotate_TwoFingerPan)
        {
            // Two-finger same-direction pan only; ignore pinch/rotate
            float dot = Vector2.Dot(d0.normalized, d1.normalized);
            bool sameDir = dot > 0.3f; // tolerance
            if (sameDir)
            {
                Vector2 avg = (d0 + d1) * 0.5f;
                HandlePan(avg);
            }
            // else do nothing for conflicting two-finger movement
        }
        else if (allowRotationWhenZooming)
        {
            if (pinchScore > pinchThreshold)
            {
                HandleZoom(distanceDelta);
            }
            if (rotateScore > rotateThreshold)
            {
                HandleRotation(-rotationAmount);
            }
        }
        else
        {
            // Exclusive: choose primary
            if (primaryState == PrimaryGesture.Undetermined)
            {
                bool pinchDetected = pinchScore > pinchThreshold;
                bool rotateDetected = rotateScore > rotateThreshold;

                if (pinchDetected && !rotateDetected) primaryState = PrimaryGesture.Zoom;
                else if (!pinchDetected && rotateDetected) primaryState = PrimaryGesture.Rotate;
                else if (pinchDetected && rotateDetected)
                    primaryState = (pinchScore >= rotateScore) ? PrimaryGesture.Zoom : PrimaryGesture.Rotate;
            }

            if (primaryState == PrimaryGesture.Zoom && pinchScore > pinchThreshold)
            {
                HandleZoom(distanceDelta);
            }
            else if (primaryState == PrimaryGesture.Rotate && rotateScore > rotateThreshold)
            {
                HandleRotation(-rotationAmount);
            }
        }

        lastPos0 = cur0;
        lastPos1 = cur1;
        lastDistance = curDist;
        lastMid = mid;
    }

    // Mode-aware rotation detection for two-finger gestures
    private float ComputeRotateSignal(Vector2 d0, Vector2 d1, out float rotationAmount)
    {
        // Opposite vertical (default): current behavior
        if (testMode == CameraTestMode.C_TwoFingerSameDirectionRotate)
        {
            // Same vertical direction => rotate
            bool sameVertical = Mathf.Sign(d0.y) == Mathf.Sign(d1.y) && (Mathf.Abs(d0.y) > 0f || Mathf.Abs(d1.y) > 0f);
            if (sameVertical)
            {
                rotationAmount = (d0.y + d1.y) * 0.5f;
                return (Mathf.Abs(d0.y) + Mathf.Abs(d1.y)) * 0.5f;
            }
            rotationAmount = 0f;
            return 0f;
        }
        else
        {
            // Opposite vertical => rotate (original)
            bool verticalOpposite = (Mathf.Sign(d0.y) != Mathf.Sign(d1.y)) &&
                                    (Mathf.Abs(d0.y) > 0f && Mathf.Abs(d1.y) > 0f);
            if (verticalOpposite)
            {
                rotationAmount = (d0.y - d1.y) * 0.5f;
                return (Mathf.Abs(d0.y) + Mathf.Abs(d1.y)) * 0.5f;
            }
            rotationAmount = 0f;
            return 0f;
        }
    }

    private bool TryGetMidpointPivotWorld(Vector2 midScreen, out Vector3 world)
    {
        Ray ray = _mainCamera.ScreenPointToRay(midScreen);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }
        world = targetPosition + (targetRotation * Vector3.forward) * Mathf.Max(0.01f, focusDistance);
        return false;
    }

    private void HandlePan(Vector2 avgDelta)
    {
        Vector3 right = _mainCamera.transform.right;
        Vector3 forward = _mainCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        float speed = EffectivePanSpeed();
        Vector3 move = (-right * avgDelta.x - forward * avgDelta.y) * speed;
        targetPosition += move;
        ClampTargetToBounds();
    }

    private void HandleZoom(float distanceDelta)
    {
        if (!hasPivot)
        {
            hasPivot = true;
            pivotWorld = targetPosition + (targetRotation * Vector3.forward) * Mathf.Max(0.01f, focusDistance);
        }

        Vector3 toCam = targetPosition - pivotWorld;
        float currentDist = toCam.magnitude;

        float desiredChange = distanceDelta * zoomSpeed;
        float targetDist = Mathf.Clamp(currentDist - desiredChange, minZoomDistance, maxZoomDistance);

        if (Mathf.Abs(targetDist - currentDist) < 0.0001f)
            return;

        Vector3 dir = toCam.sqrMagnitude > 1e-8f ? toCam.normalized : (targetRotation * Vector3.back);
        targetPosition = pivotWorld + dir * targetDist;
        ClampTargetToBounds();
    }

    private void HandleRotation(float rotationDelta)
    {
        if (!hasPivot)
        {
            hasPivot = true;
            pivotWorld = targetPosition + (targetRotation * Vector3.forward) * Mathf.Max(0.01f, focusDistance);
        }

        Quaternion rot = Quaternion.AngleAxis(rotationDelta * rotationSpeed, Vector3.up);

        Vector3 dir = targetPosition - pivotWorld;
        dir = rot * dir;
        targetPosition = pivotWorld + dir;
        targetRotation = rot * targetRotation;
        ClampTargetToBounds();
    }

    private float EffectivePanSpeed()
    {
        float dist = GetCurrentZoomDistance();
        float t = Mathf.InverseLerp(minZoomDistance, maxZoomDistance, dist); // 0 at min zoom, 1 at max zoom
        float scale = Mathf.Lerp(panSpeedScaleAtMinZoom, 1f, t);
        return panSpeed * scale;
    }

    private float GetCurrentZoomDistance()
    {
        if (hasPivot)
        {
            return Mathf.Clamp((targetPosition - pivotWorld).magnitude, minZoomDistance, maxZoomDistance);
        }

        // Approximate by intersecting camera forward with ground plane (y=0)
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = new Ray(targetPosition, targetRotation * Vector3.forward);
        if (plane.Raycast(ray, out float enter) && enter > 0f)
        {
            Vector3 hit = ray.GetPoint(enter);
            return Mathf.Clamp(Vector3.Distance(targetPosition, hit), minZoomDistance, maxZoomDistance);
        }

        return Mathf.Clamp(focusDistance, minZoomDistance, maxZoomDistance);
    }

    private void ClampTargetToBounds()
    {
        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            float x = Mathf.Clamp(targetPosition.x, b.min.x, b.max.x);
            float z = Mathf.Clamp(targetPosition.z, b.min.z, b.max.z);
            targetPosition = new Vector3(x, targetPosition.y, z);
        }
        else
        {
            float x = Mathf.Clamp(targetPosition.x, xLimits.x, xLimits.y);
            float z = Mathf.Clamp(targetPosition.z, zLimits.x, zLimits.y);
            targetPosition = new Vector3(x, targetPosition.y, z);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (boundsCollider != null)
        {
            Gizmos.DrawWireCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
        }
        else
        {
            Vector3 c1 = new Vector3(xLimits.x, 0, zLimits.x);
            Vector3 c2 = new Vector3(xLimits.y, 0, zLimits.x);
            Vector3 c3 = new Vector3(xLimits.y, 0, zLimits.y);
            Vector3 c4 = new Vector3(xLimits.x, 0, zLimits.y);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c4);
            Gizmos.DrawLine(c4, c1);
        }
    }
#endif
}
