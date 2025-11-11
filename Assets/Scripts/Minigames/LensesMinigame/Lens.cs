using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class Lens : MonoBehaviour, IPointerDownHandler
{
    [Header("Rotation")]
    [SerializeField] public KeyCode rotateLeft = KeyCode.Q;
    [SerializeField] public KeyCode rotateRight = KeyCode.E;
    [SerializeField] float rotateSpeed = 60f;            // degrees per second
    [SerializeField] Vector3 rotationAxis = Vector3.up;  // rotate around global Y by default
    [SerializeField] bool allowRotation = true;

    [Header("Lens Data")]
    [SerializeField] private Sprite lensSprite;
    [SerializeField] public bool isReflective = true;

    private bool isSelected;
    private Renderer cachedRenderer;
    private Color originalColor;

    private void Awake()
    {
        // Register with controller if present
        var minigameController = FindAnyObjectByType<LensesController>();
        if (gameObject.activeSelf && minigameController != null)
            minigameController.RegisterLens(gameObject);

        // Cache renderer + original color for highlight toggle
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
            originalColor = cachedRenderer.material.color;
    }

    public void DriveRotation(float input, float dt, Space space = Space.World)
    {
        if (!allowRotation || Mathf.Approximately(input, 0f)) return;
        transform.Rotate(rotationAxis, input * rotateSpeed * dt, space);
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false; // ensure physics raycasts hit
    }

    // Select on mouse/pointer DOWN (works with the EventSystem + PhysicsRaycaster)
    public void OnPointerDown(PointerEventData eventData)
    {
        SetSelected(true);
        Debug.Log($"[Lens] OnPointerDown {gameObject.name} (button:{eventData.button}) Selected:{isSelected}");
    }

    // Helper to toggle highlight
    private void SetSelected(bool value)
    {
        isSelected = value;
        if (cachedRenderer == null) return;
        cachedRenderer.material.color = isSelected ? Color.yellow : originalColor;
    }

    // Call this from your controller when another lens is chosen, or when you want to clear selection
    public void Deselect()
    {
        SetSelected(false);
    }
}
