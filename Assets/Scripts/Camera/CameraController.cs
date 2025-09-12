using UnityEngine;

public class TouchCameraController : MonoBehaviour
{
    private enum PrimaryGesture { None, Undetermined, Zoom, Rotate }

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.01f;
    [SerializeField] private float decelerationRate = 0.9f; 
    [SerializeField] private float stopThreshold = 0.05f;   

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.02f;
    [SerializeField] private float minZoomDistance = 5f;
    [SerializeField] private float maxZoomDistance = 40f;
    [SerializeField] private float focusDistance = 10f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 0.15f;

    [Header("Gesture Detection")]
    [SerializeField] private float deadzone = 1.5f;
    [SerializeField] private float pinchThreshold = 3f;
    [SerializeField] private float rotateThreshold = 3f;
    [SerializeField] private float dominanceFactor = 1.3f;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private Camera _mainCamera;
    private PrimaryGesture primaryState = PrimaryGesture.None;

    private Vector2 lastPos0;
    private Vector2 lastPos1;
    private float lastDistance;
    private Vector2 lastMid;

    private Vector3 panVelocity;   
    private bool isPanning;        

    void Start()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount == 2)
        {
            ProcessTwoFinger();
        }
        else
        {
            // Touch released -> stop detecting gesture but keep inertia
            if (primaryState != PrimaryGesture.None)
            {
                if (debug) Debug.Log("Gesture ended -> reset");
                primaryState = PrimaryGesture.None;
                isPanning = false; // will switch to inertia movement
            }

            // Apply inertia when not actively panning
            if (!isPanning && panVelocity.sqrMagnitude > stopThreshold * stopThreshold)
            {
                ApplyInertia();
            }
        }
    }

    private void ProcessTwoFinger()
    {
        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);

        Vector2 cur0 = t0.position;
        Vector2 cur1 = t1.position;
        Vector2 mid = (cur0 + cur1) * 0.5f;
        float curDist = Vector2.Distance(cur0, cur1);

        if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
        {
            primaryState = PrimaryGesture.Undetermined;
            lastPos0 = cur0;
            lastPos1 = cur1;
            lastDistance = curDist;
            lastMid = mid;
            panVelocity = Vector3.zero; 
            return;
        }

        Vector2 d0 = cur0 - lastPos0;
        Vector2 d1 = cur1 - lastPos1;
        Vector2 midDelta = mid - lastMid;
        float distanceDelta = curDist - lastDistance;

        // Gesture scores
        float pinchScore = Mathf.Abs(distanceDelta);
        float rotateScore = 0f;
        bool verticalOpposite = (Mathf.Sign(d0.y) != Mathf.Sign(d1.y)) && (Mathf.Abs(d0.y) > 0 && Mathf.Abs(d1.y) > 0);
        if (verticalOpposite)
            rotateScore = (Mathf.Abs(d0.y) + Mathf.Abs(d1.y)) * 0.5f;

        // Lock primary state (only between Zoom/Rotate)
        if (primaryState == PrimaryGesture.Undetermined)
        {
            if (pinchScore > pinchThreshold && pinchScore > rotateScore * dominanceFactor)
            {
                primaryState = PrimaryGesture.Zoom;
                if (debug) Debug.Log("Locked primary gesture: Zoom");
            }
            else if (rotateScore > rotateThreshold && rotateScore > pinchScore * dominanceFactor)
            {
                primaryState = PrimaryGesture.Rotate;
                if (debug) Debug.Log("Locked primary gesture: Rotate");
            }
        }

        Vector2 avgDelta = (d0 + d1) * 0.5f;
        if (avgDelta.magnitude > deadzone)
        {
            HandlePan(avgDelta);
            isPanning = true;
        }

        if (primaryState == PrimaryGesture.Zoom)
        {
            HandleZoom(distanceDelta);
        }
        else if (primaryState == PrimaryGesture.Rotate)
        {
            float rotationAmount = (d0.y - d1.y) * 0.5f;
            HandleRotation(rotationAmount);
        }

        lastPos0 = cur0;
        lastPos1 = cur1;
        lastDistance = curDist;
        lastMid = mid;
    }

    private void HandlePan(Vector2 avgDelta)
    {
        Vector3 right = _mainCamera.transform.right;
        Vector3 forward = _mainCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 move = (-right * avgDelta.x - forward * avgDelta.y) * panSpeed;
        _mainCamera.transform.position += move;

        // Store velocity for inertia
        panVelocity = move / Time.deltaTime;
    }

    private void ApplyInertia()
    {
        _mainCamera.transform.position += panVelocity * Time.deltaTime;

        // Exponential decay for smooth slowdown
        float decay = Mathf.Pow(decelerationRate, Time.deltaTime * 60f);
        panVelocity *= decay;

        if (panVelocity.sqrMagnitude < stopThreshold * stopThreshold)
        {
            panVelocity = Vector3.zero;
        }
    }

    private void HandleZoom(float distanceDelta)
    {

        Vector3 move = _mainCamera.transform.forward * (distanceDelta * zoomSpeed);
        Vector3 newPos = new Vector3(_mainCamera.transform.position.x, Mathf.Clamp(_mainCamera.transform.position.y,minZoomDistance, maxZoomDistance), _mainCamera.transform.position.z) + move;

        if(newPos.y < minZoomDistance || newPos.y > maxZoomDistance)
            return;

        Vector3 focusPoint = _mainCamera.transform.position + _mainCamera.transform.forward * focusDistance;
            _mainCamera.transform.position = newPos;
    }

    private void HandleRotation(float rotationDelta)
    {
        Vector3 focusPoint = _mainCamera.transform.position + _mainCamera.transform.forward * focusDistance;
        _mainCamera.transform.RotateAround(focusPoint, Vector3.up, rotationDelta * rotationSpeed);
    }
}
