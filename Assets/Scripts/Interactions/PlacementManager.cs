using UnityEngine;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Factory")]
    [SerializeField] private MachineFactory factory;

    [Header("Grid Snapping")]
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private GridService gridService;
    [SerializeField] private float snapYOffset = 0f; 

    [Header("Edge Scroll While Dragging")]
    [SerializeField] private float edgeZonePixels = 48f;
    [SerializeField] private float edgeScrollSpeed = 12f;

    [Header("Input")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [SerializeField, Tooltip("Allow rotating by touching with a second finger while dragging.")]
    private bool enableSecondFingerRotate = true;

    [Header("Selection UI")]
    [SerializeField] private SelectionUI selectionUI;

    private CameraController _camCtrl;

    // State machine
    private IPlacementState _state;

    // New: commands + placement rules
    public CommandHistory History { get; private set; }
    public PlacementRulePipeline PlacementRules { get; private set; }

    // Expose what states need (read-only)
    public GridService GridService => gridService;
    public CameraController CameraCtrl => _camCtrl;
    public bool SnapToGrid => snapToGrid;
    public KeyCode RotateKey => rotateKey;
    public bool EnableSecondFingerRotate => enableSecondFingerRotate;
    public SelectionUI SelectionUI => selectionUI;

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        _camCtrl = FindFirstObjectByType<CameraController>();

        // Build default rule pipeline (extend freely)
        PlacementRules = new PlacementRulePipeline()
            .Add(new InsideGridRule())
            .Add(new AreaFreeRule());

        History = new CommandHistory();

        SetState(new IdleState(this));
    }

    void Update()
    {
        _state?.Update();
    }

    // State control
    public void SetState(IPlacementState next)
    {
        _state?.Exit();
        _state = next;
        _state?.Enter();
    }

    // UI and other callers can request rotate; state decides what to do
    public void RequestRotate() => _state?.OnRotateRequested();

    private void OnEnable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart += OnHoldStart;
            InteractionManager.Instance.OnHoldMove  += OnHoldMove;
            InteractionManager.Instance.OnHoldEnd   += OnHoldEnd;
            InteractionManager.Instance.OnTap       += OnTap;
            //Close selection on empty tap by forwarding as a null target tap to current state
            InteractionManager.Instance.OnTapEmpty  += OnTapEmpty;
        }
    }

    private void OnDisable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart -= OnHoldStart;
            InteractionManager.Instance.OnHoldMove  -= OnHoldMove;
            InteractionManager.Instance.OnHoldEnd   -= OnHoldEnd;
            InteractionManager.Instance.OnTap       -= OnTap;
            InteractionManager.Instance.OnTapEmpty  -= OnTapEmpty;
        }
    }

    private void OnTap(IInteractable interactable, Vector2 screen, Vector3 world) =>
        _state?.OnTap(interactable, screen, world);

    private void OnTapEmpty(Vector2 screen, Vector3 world) =>
        _state?.OnTap(null, screen, world);

    private void OnHoldStart(IInteractable interactable, Vector2 screen, Vector3 world) =>
        _state?.OnHoldStart(interactable, screen, world);

    private void OnHoldMove(IInteractable interactable, Vector2 screen, Vector3 world) =>
        _state?.OnHoldMove(interactable, screen, world);

    private void OnHoldEnd(IInteractable interactable, Vector2 screen, Vector3 world) =>
        _state?.OnHoldEnd(interactable, screen, world);

    // Buy spawns immediately at screen center (no confirm for now)
    public void StartPlacement(MachineData machineData)
    {
        if (factory == null || machineData == null || machineData.prefab == null)
        {
            Debug.LogError("PlacementManager.StartPlacement: Missing factory or data/prefab.");
            return;
        }

        var cam = Camera.main;
        Vector3 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector3 world = ScreenToGround(screenCenter, cam);

        Machine machine = factory.CreateMachine(machineData, world);
        if (snapToGrid && gridService != null && machine.TryGetComponent<IGridOccupant>(out var occ))
        {
            var size = occ.BaseSize.OrientedSize(machineData.defaultOrientation);
            Vector2Int cell = gridService.WorldToCell(world);
            Vector2Int anchor = gridService.ClampAnchor(cell, size);
            world = AnchorToWorldCenter(anchor, size, ComputePivotBottomOffset(machine.transform));

            // Validate with rules (optional here, recommended if later rules require specific tiles, etc.)
            if (PlacementRules.Validate(gridService, occ, anchor, machineData.defaultOrientation, out var error))
            {
                occ.SetPlacement(anchor, machineData.defaultOrientation);
                machine.transform.position = world;
                gridService.SetAreaOccupant(anchor, size, machine.gameObject);
            }
            else
            {
                Debug.LogWarning($"Cannot place new machine: {error}");
                // Optionally destroy or queue a preview state
            }
        }
    }

    // Convenience: shared validation wrapper
    public bool ValidatePlacement(IGridOccupant occ, Vector2Int anchor, GridOrientation orientation, out string error) =>
        PlacementRules.Validate(gridService, occ, anchor, orientation, out error);

    // Convenience: run rotate command (for UI/selection)
    public void ExecuteRotateCommand(IGridOccupant occ, bool clockwise)
    {
        History.Do(new RotateCommand(occ, gridService, clockwise));
    }

    public void UndoLastCommand() => History.Undo();

    // Utilities exposed to states

    public Vector3 AnchorToWorldCenter(Vector2Int anchor, Vector2Int size, float heightOffset)
    {
        float y = gridService.Origin.y + heightOffset + snapYOffset;
        float wx = gridService.Origin.x + (anchor.x + size.x * 0.5f) * gridService.CellSize;
        float wz = gridService.Origin.z + (anchor.y + size.y * 0.5f) * gridService.CellSize;
        return new Vector3(wx, y, wz);
    }

    public float ComputePivotBottomOffset(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return 0f;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        float pivotY = root.position.y;
        float bottomY = b.min.y;
        return pivotY - bottomY;
    }

    public void EdgeScrollCamera(Vector2 screenPos)
    {
        if (_camCtrl == null) return;

        float w = Screen.width;
        float h = Screen.height;

        float xDir = 0f;
        if (screenPos.x < edgeZonePixels) xDir = -(1f - Mathf.Clamp01(screenPos.x / edgeZonePixels));
        else if (screenPos.x > w - edgeZonePixels) xDir = (1f - Mathf.Clamp01((w - screenPos.x) / edgeZonePixels));

        float yDir = 0f;
        if (screenPos.y < edgeZonePixels) yDir = -(1f - Mathf.Clamp01(screenPos.y / edgeZonePixels));
        else if (screenPos.y > h - edgeZonePixels) yDir = (1f - Mathf.Clamp01((h - screenPos.y) / edgeZonePixels));

        if (Mathf.Approximately(xDir, 0f) && Mathf.Approximately(yDir, 0f)) return;

        Transform camT = _camCtrl.transform;
        Vector3 right = camT.right;
        Vector3 forward = camT.forward; forward.y = 0f; forward.Normalize();

        Vector3 move = (right * xDir + forward * yDir) * edgeScrollSpeed * Time.unscaledDeltaTime;
        _camCtrl.NudgeWorld(move);
    }

    private static Vector3 ScreenToGround(Vector2 screenPos, Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : Vector3.zero;
    }
}
