using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Lens : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] public KeyCode rotateLeft = KeyCode.Q;
    [SerializeField] public KeyCode rotateRight = KeyCode.E;
    [SerializeField] float rotateSpeed = 60f;                // degrees per second
    [SerializeField] Vector3 rotationAxis = Vector3.forward; // rotate around global Z by default
    [SerializeField] bool allowRotation = true;

    [Header("Rotation Clamp")]
    [SerializeField] private bool clampRotation = true;
    [SerializeField] private float minAngle = -60f;
    [SerializeField] private float maxAngle = 60f;

    [Header("Movement")]
    [SerializeField] public KeyCode moveLeft = KeyCode.A;
    [SerializeField] public KeyCode moveRight = KeyCode.D;
    [SerializeField] float movementSpeed = 5f;
    [SerializeField] bool allowMovement = true;

    [Header("Movement Clamp")]
    [SerializeField] private bool clampMovement = false;
    [SerializeField] private Vector2 xClampRange = new Vector2(-10f, 10f);

    [Header("Lens Data")]
    [SerializeField] private Sprite lensSprite;
    [SerializeField] public bool isReflective = true;

    [Header("Collision Settings")]
    [Tooltip("Layers that block lens movement/rotation.")]
    [SerializeField] private LayerMask blockingLayers = ~0;
    [Tooltip("Shrinks the collision box slightly to avoid tiny overlaps.")]
    [SerializeField] private float skinWidth = 0.01f;

    [Header("Input (New Input System)")]
    [Tooltip("Input Actions asset that contains a 'Touch' map with 'Point' (Vector2) and 'Click' (Button).")]
    [SerializeField] private InputActionAsset inputActions;

    [Tooltip("Camera used for screen-to-world raycasts.")]
    [SerializeField] private Camera mainCamera;

    [HideInInspector] public bool isSelected;

    private Renderer cachedRenderer;
    private Color originalColor;

    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private float currentAngle;

    private LensesController minigameController;
    private Rigidbody rb;
    private Collider col;
    private Vector3 colliderCenterOffset;

    // Input buffers (read in Update, used in FixedUpdate)
    private float movementInput;
    private float rotationInput;

    // New Input System actions
    private InputAction pointAction; // Vector2 pointer position
    private InputAction clickAction; // Button pointer press
    private InputAction deltaAction; // Button pointer press

    private void Awake()
    {
        minigameController = FindAnyObjectByType<LensesController>();

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Script-driven kinematic body: no gravity, no physics pushing it around
        rb.isKinematic = true;
        rb.useGravity = false;

        // Lock Z-position, and only allow rotation around your chosen axis via script
        rb.constraints = RigidbodyConstraints.FreezePositionZ
                         | RigidbodyConstraints.FreezeRotationX
                         | RigidbodyConstraints.FreezeRotationY;

        initialRotation = rb.rotation;
        initialPosition = rb.position;
        currentAngle = 0f;

        // Offset from transform.position to collider center, to reuse when we sample future positions
        colliderCenterOffset = col.bounds.center - transform.position;

        if (gameObject.activeSelf && minigameController != null)
            minigameController.RegisterLens(gameObject);

        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
            originalColor = cachedRenderer.material.color;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // --- New Input System setup ---
        if (inputActions != null)
        {
            var touchMap = inputActions.FindActionMap("Touch", true);
            pointAction = touchMap.FindAction("Point", true); // Vector2
            clickAction = touchMap.FindAction("Click", true); // Button
            deltaAction = touchMap.FindAction("Delta", true); // Vector2
        }
        else
        {
            Debug.LogWarning($"Lens '{name}': InputActionAsset not assigned.");
        }
    }

    private void OnEnable()
    {
        inputActions.Enable();

        if (pointAction != null)
            pointAction.Enable();

        if (clickAction != null)
        {
            clickAction.Enable();
            clickAction.performed += OnPointerClick;
        }
        if (deltaAction != null)
        {
            deltaAction.Enable();
            deltaAction.performed += OnPointerClick;
        }
    }

    private void OnDisable()
    {
        if (pointAction != null)
            pointAction.Disable();

        if (clickAction != null)
        {
            clickAction.performed -= OnPointerClick;
            clickAction.Disable();
        }
    }

    private void Update()
    {
        if (isSelected)
        {
            ReadMovementInput();
            ReadRotationInput();
        }
        else
        {
            movementInput = 0f;
            rotationInput = 0f;
        }
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyRotation();
    }


    #region Selection via new Input System

    private void OnPointerClick(InputAction.CallbackContext ctx)
    {
        if (mainCamera == null || pointAction == null || minigameController == null)
            return;

        // This fires on mouse click AND touch tap (because Click is bound to <Pointer>/press)
        Vector2 screenPos = pointAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Check if THIS lens was hit
            var hitLens = hit.collider.GetComponentInParent<Lens>();
            if (hitLens == this)
            {
                minigameController.SelectLens(this);
            }
        }
    }


    public void ToggleSelected()
    {
        isSelected = !isSelected;
        if (cachedRenderer == null) return;
        cachedRenderer.material.color = isSelected ? Color.yellow : originalColor;
    }

    #endregion

    #region Movement / Rotation (keyboard for now)

    private void ReadMovementInput()
    {
        float input = 0f;
        if (Input.GetKey(moveRight)) input += 1f;
        if (Input.GetKey(moveLeft)) input -= 1f;

        movementInput = input;
    }

    private void ApplyMovement()
    {
        if (!allowMovement || Mathf.Approximately(movementInput, 0f))
            return;

        float dt = Time.fixedDeltaTime;

        Vector3 moveDir = Vector3.right;
        Vector3 targetPos = rb.position + moveDir * movementInput * movementSpeed * dt;

        if (clampMovement)
        {
            Vector3 offset = targetPos - initialPosition;
            offset.x = Mathf.Clamp(offset.x, xClampRange.x, xClampRange.y);
            targetPos = initialPosition + offset;
        }

        if (CanMoveTo(targetPos, rb.rotation))
        {
            rb.MovePosition(targetPos);
        }
        else
        {
            movementInput = 0f;
        }
    }

    private void ReadRotationInput()
    {
        float input = 0f;

        if (Input.GetKey(rotateRight)) input -= 0.25f;
        if (Input.GetKey(rotateLeft)) input += 0.25f;

        rotationInput = input;
    }

    private void ApplyRotation()
    {
        if (!allowRotation || Mathf.Approximately(rotationInput, 0f))
            return;

        float dt = Time.fixedDeltaTime;

        float delta = rotationInput * rotateSpeed * dt;
        currentAngle += delta;

        if (clampRotation)
            currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);

        Quaternion targetRot =
            initialRotation * Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);

        if (CanMoveTo(rb.position, targetRot))
        {
            rb.MoveRotation(targetRot);
        }
        else
        {
            currentAngle -= delta;
            rotationInput = 0f;
        }
    }

    public void ResetRotation()
    {
        currentAngle = 0f;
        rb.MoveRotation(initialRotation);
    }

    #endregion

    #region Collision helper

    private bool CanMoveTo(Vector3 targetPos, Quaternion targetRot)
    {
        if (col == null)
            return true;

        Vector3 halfExtents = col.bounds.extents;
        halfExtents -= Vector3.one * skinWidth;
        if (halfExtents.x < 0f) halfExtents.x = 0.001f;
        if (halfExtents.y < 0f) halfExtents.y = 0.001f;
        if (halfExtents.z < 0f) halfExtents.z = 0.001f;

        Vector3 boxCenter = targetPos + colliderCenterOffset;

        Collider[] hits = Physics.OverlapBox(
            boxCenter,
            halfExtents,
            targetRot,
            blockingLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit == col) continue; // ignore self
            return false;
        }

        return true;
    }

    #endregion
}
