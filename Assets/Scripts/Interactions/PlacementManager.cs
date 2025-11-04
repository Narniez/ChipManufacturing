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

    [Header("Conveyor Prefabs")]
    [SerializeField] private GameObject conveyorStraightPrefab;
    [SerializeField] private GameObject conveyorTurnPrefab;

    [Header("Placement Preview")]
    [SerializeField] private Material placementPreviewMaterial;

    [Header("Conveyor Test")]
    [SerializeField] private GameObject conveyorItemPrefab;
    [SerializeField, Tooltip("Default machine to use when spawning a test item without arguments.")]
    private MaterialData testMaterialData;

    private CameraController _camCtrl;

    // State machine
    private IPlacementState _state;

    // Commands + placement rules
    public CommandHistory History { get; private set; }
    public PlacementRulePipeline PlacementRules { get; private set; }

    // Expose what states need (read-only)
    public GridService GridService => gridService;
    public CameraController CameraCtrl => _camCtrl;
    public bool SnapToGrid => snapToGrid;
    public KeyCode RotateKey => rotateKey;
    public bool EnableSecondFingerRotate => enableSecondFingerRotate;
    public SelectionUI SelectionUI => selectionUI;

    // Track current selection for UI actions
    public IGridOccupant CurrentSelection { get; private set; }

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        _camCtrl = FindFirstObjectByType<CameraController>();
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

    // Buy machine -> place at screen-center where the picked cell becomes the footprint bottom-left (center pivot)
    public void StartPlacement(MachineData machineData)
    {
        if (factory == null || machineData == null || machineData.prefab == null)
        {
            Debug.LogError("PlacementManager.StartPlacement: Missing factory or data/prefab.");
            return;
        }

        // Start the generic preview placement so the preview material is applied.
        // The PreviewPlacementState handles bottom-left anchor, move/rotate, and confirm/restore.
        StartPrefabPlacement(machineData.prefab, machineData.defaultOrientation, machineData);
    }

    // SHOP BUTTONS
    public void StartBuyConveyorStraight()
    {
        if (conveyorStraightPrefab == null) { Debug.LogWarning("Straight belt prefab not assigned."); return; }
        StartPrefabPlacement(conveyorStraightPrefab, GridOrientation.North);
    }

    public void StartBuyConveyorTurn()
    {
        if (conveyorTurnPrefab == null) { Debug.LogWarning("Turn belt prefab not assigned."); return; }
        StartPrefabPlacement(conveyorTurnPrefab, GridOrientation.North);
    }

    // Generic preview placement for any IGridOccupant prefab
    public void StartPrefabPlacement(GameObject prefab, GridOrientation? initialOrientation = null)
    {
        if (prefab == null) { Debug.LogError("StartPrefabPlacement: prefab is null"); return; }
        if (gridService == null || !gridService.HasGrid)
        {
            Debug.LogWarning("Grid is not ready.");
            return;
        }

        SetState(new PreviewPlacementState(this, prefab, placementPreviewMaterial, initialOrientation));
    }

    // Overload that carries MachineData (so we can initialize on confirm)
    public void StartPrefabPlacement(GameObject prefab, GridOrientation? initialOrientation, MachineData machineData)
    {
        if (prefab == null) { Debug.LogError("StartPrefabPlacement: prefab is null"); return; }
        if (gridService == null || !gridService.HasGrid)
        {
            Debug.LogWarning("Grid is not ready.");
            return;
        }

        SetState(new PreviewPlacementState(this, prefab, placementPreviewMaterial, initialOrientation, machineData));
    }

    // UI Confirm/Cancel buttons should call these
    public void ConfirmPreview()
    {
        if (_state is PreviewPlacementState p) p.ConfirmPlacement();
    }

    public void CancelPreview()
    {
        if (_state is PreviewPlacementState p) p.CancelPlacement();
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

    public GameObject GetConveyorPrefab(bool isTurn) => isTurn ? conveyorTurnPrefab : conveyorStraightPrefab;

    public Material GetPreviewMaterial() => placementPreviewMaterial;

    internal void SetCurrentSelection(IGridOccupant occ)
    {
        CurrentSelection = occ;
    }

   // public void SpawnTestItemOnSelectedBelt() => SpawnTestItemOnSelectedBelt(MaterialType.Silicon);

    public void SpawnTestItemOnSelectedBelt()
    {
        if (testMaterialData == null)
        {
            Debug.LogWarning("PlacementManager: No MaterialData set.");
            return;
        }
        SpawnTestItemOnSelectedBelt(testMaterialData);
    }

    // UI button: spawn a test item on the selected belt
    public void SpawnTestItemOnSelectedBelt(MaterialData testMaterialData)
    {
        if (!(CurrentSelection is ConveyorBelt belt))
        {
            Debug.LogWarning("No conveyor belt selected.");
            return;
        }

        if (belt.HasItem)
        {
            Debug.Log("Selected belt already has an item.");
            return;
        }

        // Prefer registry for visuals, fallback to generic prefab
        GameObject visualPrefab = MaterialVisualRegistry.Instance != null
            ? MaterialVisualRegistry.Instance.GetPrefab(testMaterialData.materialType)
            : null;
        if (visualPrefab == null) visualPrefab = conveyorItemPrefab;

        GameObject visual = null;
        if (visualPrefab != null)
            visual = Instantiate(visualPrefab);

        var item = new ConveyorItem(testMaterialData, visual);
        if (!belt.TrySetItem(item))
        {
            if (visual != null) Destroy(visual);
            Debug.Log("Failed to place item on belt.");
        }
    }

    public void DestroyCurrentSelection()
    {
        var occ = CurrentSelection;
        if (occ == null) return;

        // Clear grid occupancy
        if (gridService != null && gridService.HasGrid)
        {
            var size = occ.BaseSize.OrientedSize(occ.Orientation);
            gridService.SetAreaOccupant(occ.Anchor, size, null);
        }

        // Destroy the GO and clear selection/UI/state
        var go = (occ as Component)?.gameObject;

        SetCurrentSelection(null);
        selectionUI?.Hide();

        if (go != null) Destroy(go);

        SetState(new IdleState(this));
    }
}
