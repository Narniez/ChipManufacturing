using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NewCameraControls : MonoBehaviour
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

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    // Touch actions (map must contain these names)
    private InputAction primaryTouchContact;
    private InputAction primaryTouchPosition;
    private InputAction secondaryTouchContact;
    private InputAction secondaryTouchPosition;

    // Mouse actions
    private InputAction pointerPosition;
    private InputAction pointerDelta;
    private InputAction pointerPress;
    private InputAction pointerRightPress;
    private InputAction scroll;

    [Header("Camera Settings")]
    [SerializeField] private float panSpeed = 0.1f;
    [SerializeField] private float zoomSpeed = 3f;              // height change factor
    [SerializeField] private float rotateSpeed = 0.3f;          // yaw * delta
    [Tooltip("Min/Max height of the camera when zooming (Y axis).")]
    [SerializeField] private float minHeight = 5f;
    [SerializeField] private float maxHeight = 60f;

    [Header("Rotation Behavior")]
    [Tooltip("If true, rotate (yaw) without moving the camera. If false, orbit around a ground pivot like CameraController.")]
    [SerializeField] private bool rotateInPlace = true;

    [Header("Gesture / Mode Settings")]
    [Tooltip("Ignore tiny finger movement (pixels).")]
    [SerializeField] private float deadzone = 1.5f;
    [Tooltip("Minimum absolute pinch distance delta (pixels) to treat as zoom.")]
    [SerializeField] private float pinchThreshold = 3f;
    [Tooltip("Minimum rotation signal (aggregated vertical delta) to treat as rotate.")]
    [SerializeField] private float rotateThreshold = 3f;
    [Tooltip("If true (Mode A/C), rotation and zoom can occur simultaneously; otherwise (Mode B) only dominant gesture applies.")]
    [SerializeField] private bool allowRotationWhenZooming = true;
    [SerializeField] private CameraTestMode testMode = CameraTestMode.A_SimultaneousZoomRotate;

    [Header("Bounds")]
    [Tooltip("Optional: If set, camera will be clamped to this BoxCollider's world bounds.")]
    [SerializeField] private BoxCollider boundsCollider;
    [Tooltip("Used if no BoxCollider is provided.")]
    [SerializeField] private Vector2 xLimits = new Vector2(-50, 50);
    [Tooltip("Used if no BoxCollider is provided.")]
    [SerializeField] private Vector2 zLimits = new Vector2(-50, 50);

    [Header("Edge Scroll While Dragging")]
    [SerializeField] private float edgeZonePixels = 48f;
    [SerializeField] private float edgeScrollSpeed = 12f;

    [Header("Smoothing")]
    [Tooltip("Seconds to smooth XZ panning.")]
    [SerializeField] private float panSmoothTime = 0.08f;
    [Tooltip("Seconds to smooth yaw rotation.")]
    [SerializeField] private float rotateSmoothTime = 0.06f;
    [Tooltip("Seconds to smooth height (zoom).")]
    [SerializeField] private float heightSmoothTime = 0.08f;
    [SerializeField] private bool debugLog;

    [Header("Gesture Safety")]
    [Tooltip("If true, once a two-finger rotation starts, suppress single-finger panning until all fingers are lifted.")]
    [SerializeField] private bool lockPanUntilReleaseAfterTwoFingerRotate = true;

    [Header("Testing Settings")]
    [SerializeField] private Slider panSpeedSlider;
    [SerializeField] private float maxPanValue = 0.5f;
    [SerializeField] private float minPanValue = 0.01f;
    [SerializeField] private TextMeshProUGUI panSpeedValueText;

    [Header("Hold-to-pan (no smoothing)")]
    [Tooltip("Hold duration (seconds) before moving that disables pan smoothing for this drag.")]
    [SerializeField] private float noSmoothHoldSeconds = 0.4f;

    [Header("Pan scaling by zoom")]
    [Tooltip("Multiplier applied to panSpeed when zoomed in (at minHeight). Higher = faster pan when zoomed in.")]
    [SerializeField] private float panMultAtMinHeight = 1.5f;
    [Tooltip("Multiplier applied to panSpeed when zoomed out (at maxHeight). Lower = slower pan when zoomed out.")]
    [SerializeField] private float panMultAtMaxHeight = 0.6f;

    private Camera cam;

    // Mouse states
    private bool isPanningWithMouse;
    private bool isRotatingWithMouse;

    // Touch states
    private bool isTwoFingerGestureActive;
    private PrimaryGesture primaryState = PrimaryGesture.None;
    private bool hasPivot;
    private Vector3 pivotWorld;

    private Vector2 lastPrimaryPos;

    private float lastTouchDistance;

    // Two-finger tracking
    private Vector2 lastPos0;
    private Vector2 lastPos1;
    private float lastDistance;
    private Vector2 lastMid;

    // Targets
    private Vector3 panTarget;  
    private float targetYaw;
    private float targetHeight;

    // Velocities for smoothing
    private Vector2 panVelocityXZ;
    private float yawVelocity;
    private float heightVelocity;

    // Fallback for mouse position-based delta
    private Vector2 lastPointerPos;

    // External input locks
    private bool inputLocked;
    private bool blockUntilNoPointerUp;

    // Suppress single-finger pan after two-finger rotate until all fingers are released
    private bool suppressSingleFingerUntilAllReleased;

    // Hold-to-pan smoothing control
    private bool prevPrimaryDown;
    private float primaryDownStartTime = -1f;
    private bool panNoSmoothingActive; // true => pan smoothing disabled until all fingers lifted

    // Public API for mode changes
    public void SetTestMode(CameraTestMode mode)
    {
        testMode = mode;
        allowRotationWhenZooming = (mode == CameraTestMode.A_SimultaneousZoomRotate || mode == CameraTestMode.C_TwoFingerSameDirectionRotate);
    }

    void Awake()
    {
        if(panSpeedSlider != null)
        {
            panSpeedSlider.minValue = minPanValue;
            panSpeedSlider.maxValue = maxPanValue;
            panSpeedSlider.value = panSpeed;
            panSpeedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }
        cam = GetComponent<Camera>();

        primaryTouchContact    = inputActionsAsset.FindAction("PrimaryTouchContact",  true);
        primaryTouchPosition   = inputActionsAsset.FindAction("PrimaryTouchPosition", true);
        secondaryTouchContact  = inputActionsAsset.FindAction("SecondaryTouchContact", true);
        secondaryTouchPosition = inputActionsAsset.FindAction("SecondaryTouchPosition", true);

        pointerPosition   = inputActionsAsset.FindAction("MousePosition",   true);
        pointerDelta      = inputActionsAsset.FindAction("MouseDelta",      true);
        pointerPress      = inputActionsAsset.FindAction("MousePressLeft",  true);
        pointerRightPress = inputActionsAsset.FindAction("MousePressRight", true);
        scroll            = inputActionsAsset.FindAction("Scroll",          true);

        panTarget = new Vector3(transform.position.x, 0f, transform.position.z);
        targetYaw = transform.eulerAngles.y;
        targetHeight = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
        ClampTargets();

        lastPointerPos = pointerPosition.ReadValue<Vector2>();
    }

    void OnEnable()
    {
        inputActionsAsset.Enable();
        panVelocityXZ = Vector2.zero;
        yawVelocity = 0f;
        heightVelocity = 0f;
    }

    void OnDisable()
    {
        inputActionsAsset.Disable();
    }

    void FixedUpdate()
    {   
        if (blockUntilNoPointerUp)
        {
            bool anyMouseDown = Mouse.current != null &&
                                (Mouse.current.leftButton.isPressed ||
                                 Mouse.current.rightButton.isPressed ||
                                 Mouse.current.middleButton.isPressed);

            bool anyTouchDown = Touchscreen.current != null &&
                                Touchscreen.current.primaryTouch.press.isPressed;

            if (!anyMouseDown && !anyTouchDown)
                blockUntilNoPointerUp = false;
        }

        bool allowUserInput = !inputLocked && !blockUntilNoPointerUp;

        if (allowUserInput)
        {
            if (testMode == CameraTestMode.E_DesktopMouseControls)
            {
                HandleMouseInput();
            }
            else
            {
                ProcessTouchLogic();
                //HandleMouseInput(); // Allow mouse fallback even in touch modes (editor)
            }
        }

        ApplySmoothing();
    }

    public void OnSpeedChanged(float value)
    {
        panSpeed = value;
        if (panSpeedValueText != null)
            panSpeedValueText.text = panSpeed.ToString("F2");
       
    }

    // ---------- Public API (PlacementManager) ----------
    public void StopMotion(bool snapToTarget = false)
    {
        if (snapToTarget)
        {
            Vector3 snapPos = ClampToBounds(new Vector3(panTarget.x, Mathf.Clamp(targetHeight, minHeight, maxHeight), panTarget.z));
            transform.position = snapPos;
            var e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(e.x, targetYaw, e.z);
        }
        else
        {
            panTarget = new Vector3(transform.position.x, 0f, transform.position.z);
            targetYaw = transform.eulerAngles.y;
            targetHeight = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
            ClampTargets();
        }

        panVelocityXZ = Vector2.zero;
        yawVelocity = 0f;
        heightVelocity = 0f;

        // Reset gesture state
        primaryState = PrimaryGesture.None;
        isTwoFingerGestureActive = false;
        hasPivot = false;
        suppressSingleFingerUntilAllReleased = false;

        // Reset no-smoothing state
        panNoSmoothingActive = false;
        primaryDownStartTime = -1f;
        prevPrimaryDown = false;
    }

    public void SetInputLocked(bool locked) => inputLocked = locked;
    public void BlockInputUntilNoTouchRelease() => blockUntilNoPointerUp = true;

    public void NudgeWorld(Vector3 worldDelta)
    {
        panTarget += new Vector3(worldDelta.x, 0f, worldDelta.z);
        ClampTargets();
    }

    public void EdgeScrollFromScreen(Vector2 screenPos)
    {
        float w = Screen.width;
        float h = Screen.height;

        float xDir = 0f;
        if (screenPos.x < edgeZonePixels)
            xDir = -(1f - Mathf.Clamp01(screenPos.x / edgeZonePixels));
        else if (screenPos.x > w - edgeZonePixels)
            xDir = (1f - Mathf.Clamp01((w - screenPos.x) / edgeZonePixels));

        float yDir = 0f;
        if (screenPos.y < edgeZonePixels)
            yDir = -(1f - Mathf.Clamp01(screenPos.y / edgeZonePixels));
        else if (screenPos.y > h - edgeZonePixels)
            yDir = (1f - Mathf.Clamp01((h - screenPos.y) / edgeZonePixels));

        if (Mathf.Approximately(xDir, 0f) && Mathf.Approximately(yDir, 0f))
            return;

        Vector3 right = transform.right;
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
        // scale edge scrolling by zoom too (faster when zoomed in, slower when out)
        float panScale = PanScaleFactor();
        Vector3 move = (right * xDir + forward * yDir) * (edgeScrollSpeed * panScale) * Time.unscaledDeltaTime;
        NudgeWorld(move);
    }

    // ---------- Touch Logic (Modes) ----------
    private void ProcessTouchLogic()
    {
        bool primaryDown = primaryTouchContact.ReadValue<float>() > 0.1f;
        bool secondaryDown = secondaryTouchContact.ReadValue<float>() > 0.1f;

        // Track primary down edges to measure hold duration
        if (primaryDown && !prevPrimaryDown)
        {
            primaryDownStartTime = Time.unscaledTime;
            panNoSmoothingActive = false; // will decide on first move
        }
        prevPrimaryDown = primaryDown;

        // Clear suppression only when ALL fingers are up
        if (!primaryDown && !secondaryDown)
        {
            isTwoFingerGestureActive = false;
            primaryState = PrimaryGesture.None;
            suppressSingleFingerUntilAllReleased = false;

            // Reset no-smoothing state on release
            panNoSmoothingActive = false;
            primaryDownStartTime = -1f;
            return;
        }

        if (secondaryDown && primaryDown)
        {
            if (!isTwoFingerGestureActive)
            {
                isTwoFingerGestureActive = true;
                BeginTwoFinger();
            }
            else
            {
                UpdateTwoFinger();
            }
        }
        else if (primaryDown && !secondaryDown)
        {
            // If we recently rotated with two fingers, suppress single-finger pan until all released
            if (suppressSingleFingerUntilAllReleased)
            {
                // keep lastPrimaryPos in sync to avoid jump on unlock
                lastPrimaryPos = primaryTouchPosition.ReadValue<Vector2>();
                return;
            }

            isTwoFingerGestureActive = false;
            UpdateSingleFinger();
        }
        else
        {
            // secondaryDown only (unlikely in typical setups) -> treat as no input
            isTwoFingerGestureActive = false;
            primaryState = PrimaryGesture.None;
        }
    }

    // Single finger processing (pan or rotate depending on mode D)
    private void UpdateSingleFinger()
    {
        Vector2 pos = primaryTouchPosition.ReadValue<Vector2>();

        if (primaryState == PrimaryGesture.None)
        {
            primaryState = PrimaryGesture.Pan;
            lastPrimaryPos = pos;
            return;
        }

        Vector2 delta = pos - lastPrimaryPos;
        lastPrimaryPos = pos;

        if (delta.sqrMagnitude < deadzone * deadzone)
            return;

        // Decide pan smoothing mode on first meaningful movement of this drag
        if (!panNoSmoothingActive && primaryDownStartTime >= 0f)
        {
            float heldFor = Time.unscaledTime - primaryDownStartTime;
            if (heldFor >= noSmoothHoldSeconds)
            {
                // Held long enough before moving -> disable pan smoothing for this drag
                panNoSmoothingActive = true;
            }
            else
            {
                // Quick swipe -> keep smoothing
                panNoSmoothingActive = false;
            }
            // After the first decision, keep it until release
            primaryDownStartTime = -1f;
        }

        if (testMode == CameraTestMode.D_OneFingerRotate_TwoFingerPan)
        {
            HandleYawRotation(delta.x);
        }
        else
        {
            HandleTouchPanDelta(delta);
        }
    }

    // Two finger initial
    private void BeginTwoFinger()
    {
        Vector2 p0 = primaryTouchPosition.ReadValue<Vector2>();
        Vector2 p1 = secondaryTouchPosition.ReadValue<Vector2>();

        lastPos0 = p0;
        lastPos1 = p1;
        lastMid = (p0 + p1) * 0.5f;
        lastDistance = Vector2.Distance(p0, p1);

        hasPivot = !rotateInPlace && TryGetMidpointPivotWorld(lastMid, out pivotWorld);

        primaryState = PrimaryGesture.Undetermined;
    }

    // Two finger update
    private void UpdateTwoFinger()
    {
        Vector2 cur0 = primaryTouchPosition.ReadValue<Vector2>();
        Vector2 cur1 = secondaryTouchPosition.ReadValue<Vector2>();

        Vector2 mid = (cur0 + cur1) * 0.5f;
        float curDist = Vector2.Distance(cur0, cur1);

        Vector2 d0 = cur0 - lastPos0;
        Vector2 d1 = cur1 - lastPos1;

        float distanceDelta = curDist - lastDistance;
        float pinchScore = Mathf.Abs(distanceDelta);

        float rotationAmount;
        float rotateScore = ComputeRotateSignal(d0, d1, out rotationAmount);

        if (testMode == CameraTestMode.D_OneFingerRotate_TwoFingerPan)
        {
            float dot = Vector2.Dot(d0.normalized, d1.normalized);
            bool sameDir = dot > 0.3f;
            if (sameDir)
            {
                Vector2 avg = (d0 + d1) * 0.5f;
                HandleTouchPanDelta(avg);
            }
        }
        else if (allowRotationWhenZooming)
        {
            if (pinchScore > pinchThreshold)
            {
                HandleHeightZoom(distanceDelta);
                // NEW: suppress subsequent single-finger pan after a zoom gesture until both fingers are released
                if (lockPanUntilReleaseAfterTwoFingerRotate)
                    suppressSingleFingerUntilAllReleased = true;
            }
            if (rotateScore > rotateThreshold)
            {
                HandleYawRotation(-rotationAmount);
                if (lockPanUntilReleaseAfterTwoFingerRotate)
                    suppressSingleFingerUntilAllReleased = true;
            }
        }
        else
        {
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
                HandleHeightZoom(distanceDelta);
                // NEW: suppress single-finger pan after zoom until both fingers lifted
                if (lockPanUntilReleaseAfterTwoFingerRotate)
                    suppressSingleFingerUntilAllReleased = true;
            }
            else if (primaryState == PrimaryGesture.Rotate && rotateScore > rotateThreshold)
            {
                HandleYawRotation(-rotationAmount);
                if (lockPanUntilReleaseAfterTwoFingerRotate)
                    suppressSingleFingerUntilAllReleased = true;
            }
        }

        lastPos0 = cur0;
        lastPos1 = cur1;
        lastDistance = curDist;
        lastMid = mid;
    }

    private float ComputeRotateSignal(Vector2 d0, Vector2 d1, out float rotationAmount)
    {
        if (testMode == CameraTestMode.C_TwoFingerSameDirectionRotate)
        {
            bool sameVertical = Mathf.Sign(d0.y) == Mathf.Sign(d1.y) &&
                                (Mathf.Abs(d0.y) > 0f || Mathf.Abs(d1.y) > 0f);
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
        Ray ray = cam.ScreenPointToRay(midScreen);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }
        world = new Vector3(panTarget.x, 0f, panTarget.z) + transform.forward * 10f;
        return false;
    }

    // ---------- Gesture handlers ----------
    private void HandleTouchPanDelta(Vector2 delta)
    {
        Vector3 rightPlanar = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forwardPlanar = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float effPanSpeed = EffectivePanSpeed(); // faster when zoomed in, slower when out
        Vector3 move = (-delta.x * effPanSpeed) * rightPlanar + (-delta.y * effPanSpeed) * forwardPlanar;
        panTarget += new Vector3(move.x, 0f, move.z);
        ClampTargets();
    }

    private void HandleHeightZoom(float distanceDelta)
    {
        targetHeight = Mathf.Clamp(targetHeight - distanceDelta * zoomSpeed * 0.02f, minHeight, maxHeight);
        ClampTargets();
    }

    private void HandleYawRotation(float rawDelta)
    {
        float appliedDelta = rawDelta * rotateSpeed;
        targetYaw += appliedDelta;

        // Only orbit when rotateInPlace is disabled
        if (!rotateInPlace)
        {
            // Ensure pivot (use lastMid if available, else screen center)
            if (!hasPivot)
            {
                Vector2 pivotScreen = (lastMid != Vector2.zero) ? lastMid : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                hasPivot = TryGetMidpointPivotWorld(pivotScreen, out pivotWorld);
            }

            if (hasPivot)
            {
                Vector3 groundCam = new Vector3(panTarget.x, 0f, panTarget.z);
                Vector3 pivotGround = new Vector3(pivotWorld.x, 0f, pivotWorld.z);
                Vector3 offset = groundCam - pivotGround;

                Quaternion rot = Quaternion.AngleAxis(appliedDelta, Vector3.up);
                offset = rot * offset;

                groundCam = pivotGround + offset;
                panTarget = new Vector3(groundCam.x, 0f, groundCam.z);
            }
        }

        ClampTargets();
    }

    // ---------- Mouse (desktop) ----------
    private void HandleMouseInput()
    {
        bool leftDown  = pointerPress.ReadValue<float>() > 0.5f;
        bool rightDown = pointerRightPress.ReadValue<float>() > 0.5f;

        Vector2 deltaValue  = pointerDelta.ReadValue<Vector2>();
        Vector2 scrollDelta = scroll.ReadValue<Vector2>();
        Vector2 pointerPos  = pointerPosition.ReadValue<Vector2>();

        if (leftDown && !isPanningWithMouse)
        {
            isPanningWithMouse = true;
            lastPointerPos = pointerPos;
        }
        else if (!leftDown && isPanningWithMouse)
        {
            isPanningWithMouse = false;
        }

        if (rightDown && !isRotatingWithMouse)
        {
            isRotatingWithMouse = true;
            lastPointerPos = pointerPos;
            if (!rotateInPlace) hasPivot = false; // new orbit session
        }
        else if (!rightDown && isRotatingWithMouse)
        {
            isRotatingWithMouse = false;
        }

        Vector2 effectiveDelta = deltaValue;
        if (effectiveDelta == Vector2.zero)
            effectiveDelta = pointerPos - lastPointerPos;

        lastPointerPos = pointerPos;

        if (isPanningWithMouse)
        {
            Vector3 rightPlanar   = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 forwardPlanar = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float effPanSpeed = EffectivePanSpeed(); // faster when zoomed in, slower when out
            Vector3 move = (-effectiveDelta.x * effPanSpeed) * rightPlanar + (-effectiveDelta.y * effPanSpeed) * forwardPlanar;
            panTarget += new Vector3(move.x, 0f, move.z);
            ClampTargets();
        }

        if (isRotatingWithMouse)
        {
            if (!rotateInPlace)
            {
                if (!hasPivot && TryGetMidpointPivotWorld(pointerPos, out var pivot))
                {
                    pivotWorld = pivot;
                    hasPivot = true;
                }
            }
            HandleYawRotation(effectiveDelta.x);
        }

        if (scrollDelta.y != 0)
        {
            targetHeight = Mathf.Clamp(targetHeight - scrollDelta.y * zoomSpeed, minHeight, maxHeight);
            ClampTargets();
        }

        if (debugLog && (isPanningWithMouse || isRotatingWithMouse) && deltaValue == Vector2.zero)
            Debug.LogWarning("MouseDelta is zero; using position-based fallback. Ensure action type = Pass Through.");
    }

    // ---------- Smoothing & Clamp ----------
    private void ApplySmoothing()
    {
        // If we decided this drag is a "held then drag" -> disable pan smoothing
        float effectivePanSmooth = panNoSmoothingActive ? 0f : panSmoothTime;

        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetXZ  = new Vector2(panTarget.x, panTarget.z);
        Vector2 smoothedXZ = effectivePanSmooth > 0f
            ? Vector2.SmoothDamp(currentXZ, targetXZ, ref panVelocityXZ, effectivePanSmooth)
            : targetXZ;

        float currentY = transform.position.y;
        float smoothedY = heightSmoothTime > 0f
            ? Mathf.SmoothDamp(currentY, targetHeight, ref heightVelocity, heightSmoothTime)
            : targetHeight;

        Vector3 next = ClampToBounds(new Vector3(smoothedXZ.x, smoothedY, smoothedXZ.y));
        transform.position = next;

        Vector3 e = transform.eulerAngles;
        float desiredYaw = Mathf.Repeat(targetYaw, 360f);
        float smoothedYaw = rotateSmoothTime > 0f
            ? Mathf.SmoothDampAngle(e.y, desiredYaw, ref yawVelocity, rotateSmoothTime)
            : desiredYaw;
        transform.rotation = Quaternion.Euler(e.x, smoothedYaw, e.z);
    }

    private void ClampTargets()
    {
        GetXZLimits(out float minX, out float maxX, out float minZ, out float maxZ);
        panTarget.x = Mathf.Clamp(panTarget.x, minX, maxX);
        panTarget.z = Mathf.Clamp(panTarget.z, minZ, maxZ);
        targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);
    }

    private Vector3 ClampToBounds(Vector3 pos)
    {
        GetXZLimits(out float minX, out float maxX, out float minZ, out float maxZ);
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        return pos;
    }

    private void GetXZLimits(out float minX, out float maxX, out float minZ, out float maxZ)
    {
        if (boundsCollider != null)
        {
            Bounds b = boundsCollider.bounds;
            minX = b.min.x; maxX = b.max.x;
            minZ = b.min.z; maxZ = b.max.z;
        }
        else
        {
            minX = xLimits.x; maxX = xLimits.y;
            minZ = zLimits.x; maxZ = zLimits.y;
        }
    }

    // ---------- Helpers ----------
    private float PanScaleFactor()
    {
        // t = 0 when zoomed in (minHeight), t = 1 when zoomed out (maxHeight)
        float t = Mathf.InverseLerp(minHeight, maxHeight, targetHeight);
        // faster at min height, slower at max height
        return Mathf.Lerp(panMultAtMinHeight, panMultAtMaxHeight, t);
    }

    private float EffectivePanSpeed()
    {
        return panSpeed * PanScaleFactor();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
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
