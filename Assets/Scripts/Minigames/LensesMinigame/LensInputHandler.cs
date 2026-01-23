using UnityEngine;
using UnityEngine.InputSystem;

public class LensInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string touchMapName = "Touch";
    [SerializeField] private string pointActionName = "Point";  
    [SerializeField] private string clickActionName = "Click";  
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LensesController lensesController;

    private InputAction pointAction;
    private InputAction clickAction;

    private Lens activeLens;
    private bool isDragging;

    private Vector2 lastPointerPos;
    private bool hasLastPointerPos;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (inputActions == null)
        {
            Debug.LogError("LensInputHandler: InputActionAsset is not assigned.");
            enabled = false;
            return;
        }

        var map = inputActions.FindActionMap(touchMapName, true);
        pointAction = map.FindAction(pointActionName, true);
        clickAction = map.FindAction(clickActionName, true);
    }

    private void OnEnable()
    {
        inputActions.Enable();

        pointAction.Enable();
        clickAction.Enable();

        clickAction.started += OnPressStarted;
        clickAction.canceled += OnPressCanceled;
    }

    private void OnDisable()
    {
        clickAction.started -= OnPressStarted;
        clickAction.canceled -= OnPressCanceled;
    }

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (mainCamera == null) return;

        Vector2 screenPos = pointAction.ReadValue<Vector2>();
        lastPointerPos = screenPos;
        hasLastPointerPos = true;

        activeLens = null;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            var lens = hit.collider.GetComponentInParent<Lens>();
            if (lens != null)
            {
                activeLens = lens;
                if (lensesController != null)
                    lensesController.SelectLens(lens);
            }
        }

        isDragging = (activeLens != null);
    }

    private void OnPressCanceled(InputAction.CallbackContext ctx)
    {
        isDragging = false;
        hasLastPointerPos = false;
    }

    private void Update()
    {
        if (!isDragging || activeLens == null)
            return;

        Vector2 curPos = pointAction.ReadValue<Vector2>();
        if (!hasLastPointerPos)
        {
            lastPointerPos = curPos;
            hasLastPointerPos = true;
            return;
        }

        Vector2 dragDelta = curPos - lastPointerPos;
        lastPointerPos = curPos;

        if (dragDelta.sqrMagnitude > 0.0001f)
            activeLens.HandleTouch(dragDelta);
    }
}
