using UnityEngine;
using UnityEngine.InputSystem;

public class LensInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LensesController lensesController;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointAction;  // Vector2 (Pointer position)
    [SerializeField] private InputActionReference clickAction;  // Button (Pointer press)

    [Header("Raycast")]
    [SerializeField] private float maxRayDistance = 100f;
    [SerializeField] private LayerMask lensHitMask = ~0; // optional filtering

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (pointAction != null)
            pointAction.action.Enable();

        if (clickAction != null)
        {
            clickAction.action.Enable();
            clickAction.action.performed += OnPointerDown;
        }
    }

    private void OnDisable()
    {
        if (pointAction != null)
            pointAction.action.Disable();

        if (clickAction != null)
        {
            clickAction.action.performed -= OnPointerDown;
            clickAction.action.Disable();
        }
    }

    private void OnPointerDown(InputAction.CallbackContext ctx)
    {
        if (mainCamera == null || lensesController == null)
            return;

        // Mouse on PC, touch on phone – same action
        Vector2 screenPos = pointAction.action.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, lensHitMask, QueryTriggerInteraction.Ignore))
        {
            Lens lens = hit.collider.GetComponentInParent<Lens>();
            if (lens != null)
            {
                lensesController.SelectLens(lens);
            }
        }
    }
}
