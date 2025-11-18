using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class Lens : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] public KeyCode rotateLeft = KeyCode.Q;
    [SerializeField] public KeyCode rotateRight = KeyCode.E;
    [SerializeField] float rotateSpeed = 60f;            // degrees per second
    [SerializeField] Vector3 rotationAxis = Vector3.forward;  // rotate around global Z by default
    [SerializeField] bool allowRotation = true;

    [Header("Rotation Clamp")]
    [SerializeField] private bool clampRotation = true;
    [SerializeField] private float minAngle = -60f;
    [SerializeField] private float maxAngle = 60f;


    [Header("Movement")]
    [SerializeField] public KeyCode moveLeft = KeyCode.A;
    [SerializeField] public KeyCode moveRight = KeyCode.D;
    [SerializeField] float movementSpeed = 5f;            // degrees per second
    [SerializeField] bool allowMovement = true;

    [Header("Movement Clamp")]
    [SerializeField] private bool clampMovement = false;
    [SerializeField] private Vector2 xClampRange = new Vector2(-10f, 10f);

    [Header("Lens Data")]
    [SerializeField] private Sprite lensSprite;
    [SerializeField] public bool isReflective = true;

    public bool isSelected;
    private Renderer cachedRenderer;
    private Color originalColor;

    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private float currentAngle;

    private LensesController minigameController;

    private void Awake()
    {
        minigameController = FindAnyObjectByType<LensesController>();
        initialRotation = transform.rotation;
        initialPosition = transform.position;
        currentAngle = 0f;

        if (gameObject.activeSelf && minigameController != null)
            minigameController.RegisterLens(gameObject);

        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
            originalColor = cachedRenderer.material.color;
    }

    private void Update()
    {
        if (isSelected)
        {
            HandleLensMovement();
            HandleLensRotation();
        }
    }

    public void DriveMovement(float input, float dt, Space space = Space.World)
    {
        if (!allowMovement || Mathf.Approximately(input, 0f)) return;
        Vector3 moveDir = (space == Space.World) ? Vector3.right : transform.right;

        transform.position += moveDir * input * movementSpeed * dt;
    }

    /*  public void DriveRotation(float input, float dt, Space space = Space.World)
      {
          if (!allowRotation || Mathf.Approximately(input, 0f)) return;
          transform.Rotate(rotationAxis, input * rotateSpeed * dt, space);
      }*/


    public void ResetRotation()
    {
        currentAngle = 0f;
        transform.rotation = initialRotation;

    }

    private void OnMouseDown()
    {
        if (minigameController != null)
            minigameController.SelectLens(this);
    }


    public void ToggleSelected()
    {
        isSelected = !isSelected;
        if (cachedRenderer == null) return;
        cachedRenderer.material.color = isSelected ? Color.yellow : originalColor;
    }

    private void HandleLensRotation()
    {
        float input = 0f;

        if (Input.GetKey(rotateRight)) input -= 0.25f;
        if (Input.GetKey(rotateLeft)) input += 0.25f;

        float delta = input * rotateSpeed * Time.deltaTime;
        currentAngle += delta;

        if (clampRotation)
            currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);

        transform.rotation = initialRotation * Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);
    }

    private void HandleLensMovement()
    {
        float dt = Time.deltaTime;
        float input = 0f;

        if (Input.GetKey(moveRight)) input += 1f;
        if (Input.GetKey(moveLeft)) input -= 1f;

        DriveMovement(input, dt, Space.World);

        if (clampMovement)
        {
            Vector3 p = transform.position;
            Vector3 offset = p - initialPosition;

            offset.x = Mathf.Clamp(offset.x, xClampRange.x, xClampRange.y);

            transform.position = initialPosition + offset;
        }

    }
}
