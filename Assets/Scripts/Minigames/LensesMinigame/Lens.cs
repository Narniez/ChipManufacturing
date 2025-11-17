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

    [Header("Movement")]
    [SerializeField] public KeyCode moveLeft = KeyCode.A;
    [SerializeField] public KeyCode moveRight = KeyCode.D;
    [SerializeField] float movementSpeed = 5f;            // degrees per second
    [SerializeField] bool allowMovement = true;

    [Header("Lens Data")]
    [SerializeField] private Sprite lensSprite;
    [SerializeField] public bool isReflective = true;

    public bool isSelected;
    private Renderer cachedRenderer;
    private Color originalColor;

    private LensesController minigameController;

    private void Awake()
    {
        minigameController = FindAnyObjectByType<LensesController>();
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
            HandleLensInput();
        }
    }

    public void DriveMovement(float input, float dt, Space space = Space.World)
    {
        if (!allowMovement || Mathf.Approximately(input, 0f)) return;
        Vector3 moveDir = (space == Space.World) ? Vector3.right : transform.right;

        transform.position += moveDir * input * movementSpeed * dt;
    }

    public void DriveRotation(float input, float dt, Space space = Space.World)
    {
        if (!allowRotation || Mathf.Approximately(input, 0f)) return;
        transform.Rotate(rotationAxis, input * rotateSpeed * dt, space);
    }

    /// <summary>
    /// be able to move lenses left right / up down
    /// </summary>

    public void ResetRotation()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;
        transform.rotation = Quaternion.identity;

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

    private void HandleLensInput()
    {
        float dt = Time.deltaTime;
        float input = 0f;

        if (Input.GetKey(rotateRight)) input -= 0.25f;
        if (Input.GetKey(rotateLeft)) input += 0.25f;

        DriveRotation(input, dt, Space.Self);

        if (Input.GetKey(moveRight)) input += 1f;
        if (Input.GetKey(moveLeft)) input -= 1f;

        DriveMovement(input, dt, Space.World);
    }

}
