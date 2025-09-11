using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _dragSpeed = 2f;
    [SerializeField] private float minZ = -100f;
    [SerializeField] private float maxZ = 100f;
    [SerializeField] private float _touchSensitivity = 0.01f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float focusDistance = 10f;

    private Camera _mainCamera;
    private Vector2 lastSingleTouchPos;

    void Start()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount == 1)
        {
            HandleSingleTouchRotation();
        }
        else if (Input.touchCount == 2)
        {
            HandleTwoFingerDrag();
        }
    }

    private void HandleSingleTouchRotation()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            lastSingleTouchPos = touch.position;
        }
        else if (touch.phase == TouchPhase.Moved)
        {
            Vector2 delta = touch.position - lastSingleTouchPos;
            lastSingleTouchPos = touch.position;

            // Get a point in front of the camera (center of screen)
            Vector3 lookAtPoint = _mainCamera.transform.position + _mainCamera.transform.forward * focusDistance;

            // Rotate around that point on the Y-axis only
            _mainCamera.transform.RotateAround(lookAtPoint, Vector3.up, delta.x * rotationSpeed);
        }
    }

    private void HandleTwoFingerDrag()
    {
        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        if (touch0.phase == TouchPhase.Moved && touch1.phase == TouchPhase.Moved)
        {
            Vector2 delta0 = touch0.deltaPosition;
            Vector2 delta1 = touch1.deltaPosition;

            float dot = Vector2.Dot(delta0.normalized, delta1.normalized);

            if (dot > 0.7f) // both fingers moving roughly the same direction
            {
                Vector2 avgDelta = (delta0 + delta1) * 0.5f;

                float moveAmount = -avgDelta.y * _dragSpeed * _touchSensitivity * Time.deltaTime;

                Vector3 newPos = _mainCamera.transform.position + new Vector3(0, 0, _mainCamera.transform.position.z) * moveAmount;
                newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

                _mainCamera.transform.position = newPos;
            }
        }
    }
}
