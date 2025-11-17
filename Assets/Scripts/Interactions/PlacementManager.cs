using System.Collections.Generic;
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

    private NewCameraControls _camCtrl;
    private IPlacementState _state;

    public CommandHistory History { get; private set; }
    public PlacementRulePipeline PlacementRules { get; private set; }

    public GridService GridService => gridService;
    public NewCameraControls CameraCtrl => _camCtrl;
    public bool SnapToGrid => snapToGrid;
    public KeyCode RotateKey => rotateKey;
    public bool EnableSecondFingerRotate => enableSecondFingerRotate;
    public SelectionUI SelectionUI => selectionUI;
    public IGridOccupant CurrentSelection { get; private set; }

    private readonly HashSet<Vector2Int> _beltSwapInProgress = new HashSet<Vector2Int>();

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        _camCtrl = FindFirstObjectByType<NewCameraControls>();

        PlacementRules = new PlacementRulePipeline()
            .Add(new InsideGridRule())
            .Add(new AreaFreeRule());

        History = new CommandHistory();
        SetState(new IdleState(this));
    }

    void Update() => _state?.Update();

    public void SetState(IPlacementState next)
    {
        _state?.Exit();
        _state = next;
        _state?.Enter();
    }

    public void RequestRotate() => _state?.OnRotateRequested();

    void OnEnable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart += OnHoldStart;
            InteractionManager.Instance.OnHoldMove += OnHoldMove;
            InteractionManager.Instance.OnHoldEnd += OnHoldEnd;
            InteractionManager.Instance.OnTap += OnTap;
            InteractionManager.Instance.OnTapEmpty += OnTapEmpty;
        }
    }

    void OnDisable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart -= OnHoldStart;
            InteractionManager.Instance.OnHoldMove -= OnHoldMove;
            InteractionManager.Instance.OnHoldEnd -= OnHoldEnd;
            InteractionManager.Instance.OnTap -= OnTap;
            InteractionManager.Instance.OnTapEmpty -= OnTapEmpty;
        }
    }

    private void OnTap(IInteractable interactable, Vector2 screen, Vector3 world) => _state?.OnTap(interactable, screen, world);
    private void OnTapEmpty(Vector2 screen, Vector3 world) => _state?.OnTap(null, screen, world);

    private void OnHoldStart(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        _camCtrl?.StopMotion(snapToTarget: true);
        _camCtrl?.SetInputLocked(true);
        _state?.OnHoldStart(interactable, screen, world);
    }

    private void OnHoldMove(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        _camCtrl?.EdgeScrollFromScreen(screen);
        _state?.OnHoldMove(interactable, screen, world);
    }

    private void OnHoldEnd(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        _state?.OnHoldEnd(interactable, screen, world);
        _camCtrl?.StopMotion(snapToTarget: true);
        _camCtrl?.SetInputLocked(false);
        _camCtrl?.BlockInputUntilNoTouchRelease();
    }

    public void StartPlacement(MachineData machineData)
    {
        if (factory == null || machineData == null || machineData.prefab == null)
        {
            Debug.LogError("PlacementManager.StartPlacement: Missing factory or data/prefab.");
            return;
        }
        StartPrefabPlacement(machineData.prefab, machineData.defaultOrientation, machineData);
    }

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

    public void ConfirmPreview()
    {
        if (_state is PreviewPlacementState p) p.ConfirmPlacement();
    }

    public void CancelPreview()
    {
        if (_state is PreviewPlacementState p) p.CancelPlacement();
    }

    public bool ValidatePlacement(IGridOccupant occ, Vector2Int anchor, GridOrientation orientation, out string error) =>
        PlacementRules.Validate(gridService, occ, anchor, orientation, out error);

    public void ExecuteRotateCommand(IGridOccupant occ, bool clockwise)
    {
        History.Do(new RotateCommand(occ, gridService, clockwise));
    }

    public void UndoLastCommand() => History.Undo();

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

    // Delegate to camera controller
    public void EdgeScrollCamera(Vector2 screenPos)
    {
        _camCtrl?.EdgeScrollFromScreen(screenPos);
    }

    public GameObject GetConveyorPrefab(bool isTurn) => isTurn ? conveyorTurnPrefab : conveyorStraightPrefab;
    public Material GetPreviewMaterial() => placementPreviewMaterial;

    internal void SetCurrentSelection(IGridOccupant occ) => CurrentSelection = occ;

    public ConveyorBelt ReplaceConveyorPrefab(ConveyorBelt source, bool useTurnPrefab, GridOrientation overrideOrientation, ConveyorBelt.BeltTurnKind turnKind = ConveyorBelt.BeltTurnKind.None)
    {
        if (gridService == null || !gridService.HasGrid || source == null) return source;

        var anchor = source.Anchor;
        var size = Vector2Int.one;

        if (!_beltSwapInProgress.Add(anchor))
            return source;

        try
        {
            var prefab = GetConveyorPrefab(useTurnPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("ReplaceConveyorPrefab: missing conveyor prefab.");
                return source;
            }

            var world = AnchorToWorldCenter(anchor, size, 0f);

            ConveyorItem item = null;
            if (source.HasItem)
            {
                item = source.TakeItem();
                if (item != null && item.Visual != null)
                    item.Visual.transform.position = world;
            }

            var parent = source.PreviousInChain;
            var child = source.NextInChain;

            source.gameObject.SetActive(false);
            gridService.SetAreaOccupant(anchor, size, null);

            var go = Instantiate(prefab, world, overrideOrientation.ToRotation());
            var newBelt = go.GetComponent<ConveyorBelt>();
            if (newBelt == null)
            {
                Destroy(go);
                source.gameObject.SetActive(true);
                gridService.SetAreaOccupant(anchor, size, source.gameObject);
                Debug.LogError("ReplaceConveyorPrefab: new prefab missing ConveyorBelt.");
                return source;
            }

            newBelt.SetTurnKind(turnKind);
            newBelt.SetPlacement(anchor, overrideOrientation);
            gridService.SetAreaOccupant(anchor, size, go);

            // Preserve chain links
            newBelt.PreviousInChain = parent;
            newBelt.NextInChain = child;
            if (parent != null && parent.NextInChain == source)
                parent.NextInChain = newBelt;
            if (child != null && child.PreviousInChain == source)
                child.PreviousInChain = newBelt;

            if (item != null)
                newBelt.TrySetItem(item, snapVisual: true);

            // Forward link auto-parent if missing
            AutoLinkForward(newBelt);
            newBelt.NotifyAdjacentMachinesOfConnection();
            Destroy(source.gameObject);
            return newBelt;
        }
        finally
        {
            _beltSwapInProgress.Remove(anchor);
        }
    }

    private void AutoLinkForward(ConveyorBelt belt)
    {
        if (belt == null || gridService == null || !gridService.HasGrid) return;
        var forwardCell = belt.Anchor + GridOrientationExtentions.OrientationToDelta(belt.Orientation);
        if (gridService.TryGetCell(forwardCell, out var data) && data.occupant != null)
        {
            var go = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            var forward = go != null ? go.GetComponent<ConveyorBelt>() : null;
            if (forward != null && forward.PreviousInChain == null && forward != belt)
            {
                belt.NextInChain = forward;
                forward.PreviousInChain = belt;
            }
        }
    }



    public void DestroyCurrentSelection()
    {
        var occ = CurrentSelection;
        if (occ == null) return;

        var beltToDestroy = (occ as Component)?.GetComponent<ConveyorBelt>();

        if (beltToDestroy != null)
        {
            // Re-link chain around destroyed belt
            var parent = beltToDestroy.PreviousInChain;
            var child = beltToDestroy.NextInChain;
            if (parent != null && parent.NextInChain == beltToDestroy) parent.NextInChain = child;
            if (child != null && child.PreviousInChain == beltToDestroy) child.PreviousInChain = parent;
            beltToDestroy.PreviousInChain = null;
            beltToDestroy.NextInChain = null;
        }

        if (gridService != null && gridService.HasGrid)
        {
            var size = occ.BaseSize.OrientedSize(occ.Orientation);
            gridService.SetAreaOccupant(occ.Anchor, size, null);
        }

        var go = (occ as Component)?.gameObject;

        SetCurrentSelection(null);
        selectionUI?.Hide();

        if (go != null)
        {
            if (beltToDestroy != null)
            {
                var item = beltToDestroy.TakeItem();
                if (item != null && item.Visual != null)
                    Destroy(item.Visual);
            }
            Destroy(go);
        }

        SetState(new IdleState(this));
    }
}
