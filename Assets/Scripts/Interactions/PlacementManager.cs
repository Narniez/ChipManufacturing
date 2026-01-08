using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Factory")]
    [SerializeField] private MachineFactory factory;

    [Header("Grid Snapping")]
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private GridService gridService;
    [SerializeField] private float snapYOffset = 0f;


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
    private MachinePortIndicatorController _portIndicatorController;

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

        _portIndicatorController = new MachinePortIndicatorController(this);
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
        // After confirm, ensure GameState record exists (PreviewPlacementState already called SetPlacement).
        if (CurrentSelection is Machine m)
        {
            Debug.Log("Should save machine to game state.");
            GameStateSync.TryAddOrUpdateMachine(m);
        }
        else if (CurrentSelection is ConveyorBelt b)
        {
            GameStateSync.TryAddOrUpdateBelt(b);
        }
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
        // Rotation may change orientation; update state.
        if (occ is Machine m) GameStateSync.TryUpdateMachineOrientation(m);
        else if (occ is ConveyorBelt b) GameStateSync.TryUpdateBeltOrientation(b);
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

        Bounds b = new Bounds();
        bool initialized = false;

        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (r == null || r.gameObject == null) continue;
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (!initialized) return 0f;

        float pivotY = root.position.y;
        float bottomY = b.min.y;
        return pivotY - bottomY;
    }

    public void EdgeScrollCamera(Vector2 screenPos)
    {
        _camCtrl?.EdgeScrollFromScreen(screenPos);
    }

    public GameObject GetConveyorPrefab(bool isTurn) => isTurn ? conveyorTurnPrefab : conveyorStraightPrefab;
    public Material GetPreviewMaterial() => placementPreviewMaterial;

    internal void SetCurrentSelection(IGridOccupant occ)
    {
        CurrentSelection = occ;
        TutorialEventBus.PublishSelectionChanged(occ);

    }

    // External call from PreviewPlacementState when it instantiates a machine/belt
    internal void NotifySpawned(GameObject go)
    {
        MoveToFactoryScene(go);
    }

    private void MoveToFactoryScene(GameObject go)
    {
        if (go == null) return;
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && go.scene != active)
            SceneManager.MoveGameObjectToScene(go, active);
    }

    // Helper used by loader to instantiate a prefab and move to factory scene
    public GameObject InstantiateAndMove(GameObject prefab)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab);
        MoveToFactoryScene(go);
        return go;
    }

    // Place a machine using MachineData (used by FactorySceneBuilder)
    // This one performs all placement responsibilities: initialize, set internal state on machine,
    // position, mark grid occupancy and persist GameState � mirrors PreviewPlacementState.ConfirmPlacement.
    public GameObject PlaceMachineFromSave(MachineData machineData, Vector2Int anchor, GridOrientation orientation, bool isBroken = false)
    {
        if (machineData == null || machineData.prefab == null)
        {
            Debug.LogWarning("PlaceMachineFromSave: invalid data.");
            return null;
        }

        var go = InstantiateAndMove(machineData.prefab);
        if (go == null) return null;

        var machine = go.GetComponent<Machine>();
        if (machine == null)
        {
            Debug.LogError("PlaceMachineFromSave: prefab missing Machine component.");
            Destroy(go);
            return null;
        }

        // Initialize machine internals
        machine.Initialize(machineData);

        // Set transform/orientation on the machine (component-local setter)
        machine.SetPlacement(anchor, orientation); // assume it only sets fields + transform

        // Ensure we have a valid GridService reference
        var grid = gridService ?? FindFirstObjectByType<GridService>();
        if (grid == null || !grid.HasGrid)
        {
            Debug.LogError("PlaceMachineFromSave: GridService not ready. Machine placed but grid occupancy NOT set.");
        }
        else
        {
            // Mark occupancy exactly like ConfirmPlacement()
            var size = machine.BaseSize.OrientedSize(orientation);
            grid.SetAreaOccupant(anchor, size, go);

            // Persist to game state
            GameStateSync.TryAddOrUpdateMachine(machine);

            // Validate immediately (helpful during debugging)
#if UNITY_EDITOR
            if (!grid.TryGetCell(anchor, out var cd) || cd.occupant != go)
                Debug.LogWarning($"PlaceMachineFromSave: occupancy write failed at {anchor} for {go.name}.");
#endif
        }

        if (isBroken) machine.Break();
        return go;
    }

    // Place a belt using prefab (used by FactorySceneBuilder)
    // Mirrors PreviewPlacementState Confirm flow for belts (occupancy + state).
    public ConveyorBelt PlaceBeltFromSave(GameObject prefab, Vector2Int anchor, GridOrientation orientation, bool isTurn, int turnKind, bool persist = true)
    {
        if (prefab == null)
        {
            Debug.LogWarning("PlaceBeltFromSave: prefab null.");
            return null;
        }

        var go = InstantiateAndMove(prefab);
        if (go == null) return null;

        var belt = go.GetComponent<ConveyorBelt>();
        if (belt == null)
        {
            Debug.LogError("PlaceBeltFromSave: prefab missing ConveyorBelt component.");
            Destroy(go);
            return null;
        }

        belt.SetTurnKind((ConveyorBelt.BeltTurnKind)turnKind);

        // Set transform/orientation
        belt.SetPlacement(anchor, orientation); // lightweight setter on component

        // Ensure we have a grid
        var grid = gridService ?? FindFirstObjectByType<GridService>();
        if (grid == null || !grid.HasGrid)
        {
            Debug.LogError("PlaceBeltFromSave: GridService not ready. Belt placed but grid occupancy NOT set.");
        }
        else
        {
            grid.SetAreaOccupant(anchor, Vector2Int.one, go);

            if (persist)
            {
                GameStateSync.TryAddOrUpdateBelt(belt);
            }

#if UNITY_EDITOR
            if (!grid.TryGetCell(anchor, out var cd) || cd.occupant != go)
                Debug.LogWarning($"PlaceBeltFromSave: occupancy write failed at {anchor} for {go.name}.");
#endif
        }

        return belt;
    }

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

            // Detect if 'source' sits on a Machine OUTPUT port cell (regardless of facing)
            GridOrientation? requiredWorldSide = GetRequiredMachineOutputSide(anchor);
            bool isOnMachineOutputPort = requiredWorldSide.HasValue;

            // Default final orientation & turn kind
            GridOrientation finalOrientation = overrideOrientation;
            ConveyorBelt.BeltTurnKind finalTurnKind = turnKind;
            bool forceTurnPrefab = false;

            if (isOnMachineOutputPort && overrideOrientation != requiredWorldSide.Value)
            {
                // Choose Left/Right depending on port outward vs outgoing
                var outSide = requiredWorldSide.Value;
                finalTurnKind =
                    (overrideOrientation == outSide.RotatedCW()) ? ConveyorBelt.BeltTurnKind.Right :
                    (overrideOrientation == outSide.RotatedCCW()) ? ConveyorBelt.BeltTurnKind.Left :
                    ConveyorBelt.BeltTurnKind.None;

                // If sideways branch, ensure we do place a turn prefab
                forceTurnPrefab = true;
            }

            // If we must force a turn, swap prefab accordingly
            bool willUseTurnPrefab = forceTurnPrefab || useTurnPrefab;

            // Special case: if this belt sits on a machine's output cell and the final orientation
            // after extension equals the machine's required output side, prefer a straight prefab.
            // This makes the belt look like a straight extension right at the machine output
            // instead of a corner, which improves visual in this scenario.
            if (isOnMachineOutputPort && requiredWorldSide.HasValue)
            {
                if (finalOrientation == requiredWorldSide.Value)
                {
                    // prefer straight even if earlier logic set forceTurnPrefab
                    willUseTurnPrefab = false;
                    finalTurnKind = ConveyorBelt.BeltTurnKind.None;
                }
            }

            prefab = GetConveyorPrefab(willUseTurnPrefab);

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

            var go = Instantiate(prefab, world, finalOrientation.ToRotation());
            MoveToFactoryScene(go);

            var newBelt = go.GetComponent<ConveyorBelt>();
            if (newBelt == null)
            {
                Destroy(go);
                source.gameObject.SetActive(true);
                gridService.SetAreaOccupant(anchor, size, source.gameObject);
                Debug.LogError("ReplaceConveyorPrefab: new prefab missing ConveyorBelt.");
                return source;
            }

            newBelt.SetTurnKind(finalTurnKind);
            newBelt.SetPlacement(anchor, finalOrientation);
            gridService.SetAreaOccupant(anchor, size, go);

            newBelt.PreviousInChain = parent;
            newBelt.NextInChain = child;
            if (parent != null && parent.NextInChain == source)
                parent.NextInChain = newBelt;
            if (child != null && child.PreviousInChain == source)
                child.PreviousInChain = newBelt;

            if (item != null)
                newBelt.TrySetItem(item, snapVisual: true);

            AutoLinkForward(newBelt);
            newBelt.NotifyAdjacentMachinesOfConnection();

            // Update state entry (replace existing)
            GameStateSync.TryReplaceBelt(source, newBelt);

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
        var machineToDestroy = (occ as Component)?.GetComponent<Machine>();

        // capture anchor of the thing being destroyed so we can normalize neighbours afterwards
        Vector2Int destroyedAnchor = occ.Anchor;

        if (beltToDestroy != null)
        {
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
                GameStateSync.TryRemoveBelt(beltToDestroy);
            }
            else if (machineToDestroy != null)
            {
                GameStateSync.TryRemoveMachine(machineToDestroy);
            }

            // Actually destroy the object
            Destroy(go);

            // After destroying a belt, normalize neighboring belt prefabs:
            // convert nearby turn-prefabs back to straight when their only connected neighbors
            // are straight and aligned.
            if (beltToDestroy != null && gridService != null && gridService.HasGrid)
            {
                NormalizeAdjacentBelts(destroyedAnchor);
            }
        }

        SetState(new IdleState(this));
    }


    // Inspect physical neighbors on the grid (not only chain links), collects "straight" neighbor
    // orientations (including machines that output into the cell) and if all candidates agree it replaces
    // the turn prefab with a straight prefab oriented accordingly.
    private void NormalizeAdjacentBelts(Vector2Int anchor)
    {
        foreach (var neighbor in gridService.GetNeighbors(anchor))
        {
            if (!gridService.TryGetCell(neighbor.coord, out var data) || data.occupant == null) continue;
            var go = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            if (go == null) continue;
            var belt = go.GetComponent<ConveyorBelt>();
            if (belt == null) continue;

            TryPromoteToStraight(belt);
        }
    }

    // If belt is a turn prefab, examine physical neighbors (4-way).
    // Collect candidate orientations from straight neighboring belts and from machines that output into this belt cell.
    // If all collected candidates agree on the same orientation, replace the current turn prefab with a straight
    // prefab oriented to that direction.
    private void TryPromoteToStraight(ConveyorBelt belt)
    {
        if (belt == null) return;
        if (!belt.IsTurnPrefab) return;

        var candidates = new List<GridOrientation>();

        // Check four cardinal neighbors
        for (int i = 0; i < 4; i++)
        {
            GridOrientation dir = (GridOrientation)i;
            var neighborCell = belt.Anchor + GridOrientationExtentions.OrientationToDelta(dir);
            if (!gridService.TryGetCell(neighborCell, out var neighborData) || neighborData.occupant == null) continue;

            var neighborGo = neighborData.occupant as GameObject ?? (neighborData.occupant as Component)?.gameObject;
            if (neighborGo == null) continue;

            // If neighbor is a straight conveyor, use its orientation as a candidate
            var nbBelt = neighborGo.GetComponent<ConveyorBelt>();
            if (nbBelt != null && !nbBelt.IsTurnPrefab)
            {
                candidates.Add(nbBelt.Orientation);
                continue;
            }

            // If neighbor is a machine that outputs into the belt cell, include that machine output side
            var nbMachine = neighborGo.GetComponent<Machine>();
            if (nbMachine != null && nbMachine.Data != null)
            {
                // Use helper to see if any machine adjacent to this belt claims this cell as an output.
                // GetRequiredMachineOutputSide returns the world-side for a machine that has an output on the given beltAnchor.
                var required = GetRequiredMachineOutputSide(belt.Anchor);
                if (required.HasValue)
                {
                    candidates.Add(required.Value);
                    continue;
                }
            }

            // Also consider the case neighbor is a conveyor turn that visually still implies a direction:
            // if neighbor is a turn but oriented straight relative to this cell (rare), we could consider it,
            // but to be conservative we only use explicit straight prefabs and machine outputs as candidates.
        }

        if (candidates.Count == 0) return;

        // Check if all candidates agree
        var first = candidates[0];
        foreach (var candidate in candidates)
        {
            if (candidate != first) return; // conflict -> do not promote
        }

        // All candidates agree -> replace this turn with a straight oriented to 'first'
        ReplaceConveyorPrefab(belt, useTurnPrefab: false, overrideOrientation: first, turnKind: ConveyorBelt.BeltTurnKind.None);
    }

    private GridOrientation? GetRequiredMachineOutputSide(Vector2Int beltAnchor)
    {
        foreach (var neighbor in gridService.GetNeighbors(beltAnchor))
        {
            if (!gridService.TryGetCell(neighbor.coord, out var data) || data.occupant == null) continue;
            var machineGo = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            if (machineGo == null) continue;

            var machine = machineGo.GetComponent<Machine>();
            if (machine == null || machine.Data == null) continue;

            var machineData = machine.Data;
            var machineOrientation = machine.Orientation;
            var orientedSize = machineData.size.OrientedSize(machineOrientation);

            if (machineData.ports != null && machineData.ports.Count > 0)
            {
                for (int i = 0; i < machineData.ports.Count; i++)
                {
                    var port = machineData.ports[i];
                    if (port.kind != MachinePortType.Output) continue;
                    var worldSide = RotateSideLocalToWorld(port.side, machineOrientation);
                    var portCell = ComputePortCellForMachine(machine.Anchor, machineOrientation, orientedSize, port.side, port.offset);
                    if (portCell == beltAnchor)
                        return worldSide;
                }
            }
            else
            {
                var worldSide = machineOrientation;
                var portCell = ComputePortCellForMachine(machine.Anchor, machineOrientation, orientedSize, machineOrientation, -1);
                if (portCell == beltAnchor)
                    return worldSide;
            }
        }
        return null;
    }

    public void ShowPortIndicatorsFor(Machine machine)
    {
        _portIndicatorController?.ShowFor(machine);
    }

    public void HidePortIndicators()
    {
        _portIndicatorController?.Cleanup();
    }

    private static Vector2Int ComputePortCellForMachine(Vector2Int machineAnchor, GridOrientation machineOrientation,
                                                        Vector2Int orientedSize, GridOrientation localSide, int offset)
    {
        GridOrientation side = RotateSideLocalToWorld(localSide, machineOrientation);
        int sideLen = (side == GridOrientation.North || side == GridOrientation.South) ? orientedSize.x : orientedSize.y;
        int idx = offset < 0 ? Mathf.Max(0, (sideLen - 1) / 2) : Mathf.Clamp(offset, 0, Mathf.Max(0, sideLen - 1));

        switch (side)
        {
            case GridOrientation.North: return new Vector2Int(machineAnchor.x + idx, machineAnchor.y + orientedSize.y);
            case GridOrientation.South: return new Vector2Int(machineAnchor.x + idx, machineAnchor.y - 1);
            case GridOrientation.East: return new Vector2Int(machineAnchor.x + orientedSize.x, machineAnchor.y + idx);
            case GridOrientation.West: return new Vector2Int(machineAnchor.x - 1, machineAnchor.y + idx);
            default: return machineAnchor;
        }
    }

    private static GridOrientation RotateSideLocalToWorld(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);
}