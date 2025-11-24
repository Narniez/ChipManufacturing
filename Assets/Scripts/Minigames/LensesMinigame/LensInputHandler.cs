using UnityEngine;
using UnityEngine.InputSystem;

public class LensInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string touchMapName = "Touch";
    [SerializeField] private string pointActionName = "Point";  // Vector2
    [SerializeField] private string deltaActionName = "Delta";  // Vector2
    [SerializeField] private string clickActionName = "Click";  // Button (or "Click")
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LensesController lensesController;

    private InputAction pointAction;
    private InputAction deltaAction;
    private InputAction clickAction;

    private Lens activeLens;
    private bool isDragging;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (inputActions == null)
        {
            Debug.LogError("LensTouchInput: InputActionAsset is not assigned.");
            enabled = false;
            return;
        }

        var map = inputActions.FindActionMap(touchMapName, true);
        pointAction = map.FindAction(pointActionName, true);
        deltaAction = map.FindAction(deltaActionName, true);
        clickAction = map.FindAction(clickActionName, true);
    }

    private void OnEnable()
    {
        inputActions.Enable();    // IMPORTANT on device builds

        pointAction.Enable();
        deltaAction.Enable();
        clickAction.Enable();

        clickAction.started += OnPressStarted;
        clickAction.canceled += OnPressCanceled;
    }

    private void OnDisable()
    {
        clickAction.started -= OnPressStarted;
        clickAction.canceled -= OnPressCanceled;

        clickAction.Disable();
        deltaAction.Disable();
        pointAction.Disable();

        inputActions.Disable();
    }

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (mainCamera == null) return;

        Vector2 screenPos = pointAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            var lens = hit.collider.GetComponentInParent<Lens>();
            if (lens != null)
            {
                activeLens = lens;
                if (lensesController != null)
                    lensesController.SelectLens(lens);

                isDragging = true;
            }
            else
            {
                activeLens = null;
                isDragging = false;
            }
        }
        else
        {
            activeLens = null;
            isDragging = false;
        }
    }

    private void OnPressCanceled(InputAction.CallbackContext ctx)
    {
        isDragging = false;
        // keep activeLens selected until user taps something else
    }

    private void Update()
    {
        if (!isDragging || activeLens == null)
            return;

        Vector2 dragDelta = deltaAction.ReadValue<Vector2>();
        if (dragDelta.sqrMagnitude > 0.0001f)
        {
            activeLens.HandleTouch(dragDelta);
        }
    }
}
