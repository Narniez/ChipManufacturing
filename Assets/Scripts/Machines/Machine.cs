using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Machine : MonoBehaviour, IInteractable, IDraggable, IGridOccupant
{
    // Factory sets this via Initialize
    private MachineData data;   
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;
    private GridService _grid;

    private InventoryItem inventoryItem;

    // Legacy single-input queue (used only if no recipes defined)
    private readonly Queue<MaterialType> _inputQueue = new Queue<MaterialType>();

    // Recipe-mode input buffer per material
    private readonly Dictionary<MaterialType, int> _buffer = new Dictionary<MaterialType, int>();
    private MachineRecipe _currentRecipe;
    private bool _inventoryDumpedThisCycle = false;

    // Machine breaking 
    private float _chanceToBreak = 0f;
    private float _chanceToBreakIncrement = 2f;
    private float _miniMimumChanceToBreak = 0;
    private bool _isBroken = false;

    public static event Action<Machine, Vector3> OnMachineBroken;
    public static event Action<Machine> OnMachineRepaired;

    public static event Action<MaterialType, Vector3> OnMaterialProduced;

    // Expose minimal state
    public MachineData Data => data;
    public bool IsProducing => productionRoutine != null;
    public bool IsBroken => _isBroken;

    // Treat as "generator" if no recipes, no input, but has an output
    private bool IsLegacyGenerator =>
        data != null &&
        !data.HasRecipes &&
        (data.inputMaterial == null || data.inputMaterial.materialType == MaterialType.None) &&
        data.outputMaterial != null && data.outputMaterial.materialType != MaterialType.None;

    public void Initialize(MachineData machineData)
    {
        data = machineData;
        _buffer.Clear();
        _inputQueue.Clear();
        _currentRecipe = null;

        _chanceToBreak = 0f;
        _chanceToBreakIncrement = data.chanceIncreasePerOutput;
        _miniMimumChanceToBreak = data.minimunChanceToBreak;
        _isBroken = false;
         _grid = FindFirstObjectByType<GridService>();


        StartProduction();
    }

    // Allow external systems to start production 
    public void TryStartIfIdle()
    {
        if (!_isBroken && productionRoutine == null)
            StartProduction();
    }

    // Try to start one cycle if inputs are ready
    private void StartProduction()
    {
        if (data == null) { Debug.LogError("Machine.StartProduction: MachineData not set."); return; }
        if (productionRoutine != null) return;
        if (_isBroken) return;

        if (data.HasRecipes)
        {
            // Find a satisfiable recipe
            var recipe = FindReadyRecipe();
            if (recipe == null) return;

            // Consume inputs up-front
            ConsumeInputs(recipe);
            _currentRecipe = recipe;

            float dur = recipe.processingTimeOverride > 0f ? recipe.processingTimeOverride : data.processingTime;
            productionRoutine = StartCoroutine(ProcessOneRecipe(dur));
        }
        else
        {
            // Legacy behavior with input queue
            if (_inputQueue.Count > 0)
            {
                productionRoutine = StartCoroutine(ProcessOneLegacy());
                return;
            }

            // Legacy generator: no inputs, only output; run only if at least one output belt is connected
            if (IsLegacyGenerator && HasConnectedOutputBelt())
            {
                productionRoutine = StartCoroutine(ProcessLegacyGenerator());
            }
        }
    }

    private MachineRecipe FindReadyRecipe()
    {
        if (!data.HasRecipes) return null;

        for (int i = 0; i < data.recipes.Count; i++)
        {
            var r = data.recipes[i];
            bool ok = true;
            for (int j = 0; j < r.inputs.Count; j++)
            {
                var req = r.inputs[j];
                _buffer.TryGetValue(req.material.materialType, out int have);
                if (have < Mathf.Max(1, req.amount)) { ok = false; break; }
            }
            if (ok) return r;
        }
        return null;
    }

    private void ConsumeInputs(MachineRecipe recipe)
    {
        for (int i = 0; i < recipe.inputs.Count; i++)
        {
            var req = recipe.inputs[i];
            if (_buffer.TryGetValue(req.material.materialType, out int have))
            {
                _buffer[req.material.materialType] = Mathf.Max(0, have - Mathf.Max(1, req.amount));
            }
        }
        _inventoryDumpedThisCycle = false;
    }

    private IEnumerator ProcessOneRecipe(float duration)
    {
        Debug.Log("M: start recipe");
        yield return new WaitForSeconds(duration);

        // Produce all outputs of current recipe
        if (_currentRecipe != null)
        {
            for (int i = 0; i < _currentRecipe.outputs.Count; i++)
            {
                var outStack = _currentRecipe.outputs[i];
                int count = Mathf.Max(1, outStack.amount);
                for (int c = 0; c < count; c++)
                    ProduceOneOutput(outStack.material);
            }
        }

        productionRoutine = null;
        _currentRecipe = null;

        // Attempt next recipe if inputs are available
        StartProduction();
    }

    private IEnumerator ProcessOneLegacy()
    {
        // Consume one and process
        var consumed = _inputQueue.Dequeue();

        yield return new WaitForSeconds(data.processingTime);

        // Produce legacy single output (data.outputMaterial)
        ProduceOneOutput(data.outputMaterial);

        productionRoutine = null;
        StartProduction();
    }

    private IEnumerator DelayedGeneratorRetry(float delaySeconds)
    {
        // If machine was broken or data missing, abort
        if (_isBroken || data == null) yield break;
        yield return new WaitForSeconds(delaySeconds);
        // Retry starting (will self-check belts / inputs)
        TryStartIfIdle();
    }

    private IEnumerator ProcessLegacyGenerator()
    {
        yield return new WaitForSeconds(data.processingTime);

        var belts = GetConnectedOutputBelts();
        if (belts.Count == 0)
        {
            // No empty belt right now: schedule a quick retry instead of giving up
            productionRoutine = null;
            // Small delay lets belt movement finish after chain extension
            StartCoroutine(DelayedGeneratorRetry(0.15f));
            yield break;
        }

        foreach (var belt in belts)
        {
            if (_isBroken) break;
            OnMaterialProduced?.Invoke(data.outputMaterial.materialType, transform.position);
            _chanceToBreak += _chanceToBreakIncrement;
            if (BreakCheck())
            {
                Break();
                break;
            }

            GameObject visualPrefab = MaterialVisualRegistry.Instance != null
                ? MaterialVisualRegistry.Instance.GetPrefab(data.outputMaterial.materialType)
                : null;
            GameObject visual = null;
            if (visualPrefab != null) visual = Instantiate(visualPrefab);

            var item = new ConveyorItem(data.outputMaterial, visual);
            if (!belt.TrySetItem(item) && visual != null)
                Destroy(visual);
        }

        productionRoutine = null;
        StartProduction();
    }

    private void ProduceOneOutput(MaterialData mat)
    {
        if (_isBroken) return;

        Debug.Log("M: produce one");
        OnMaterialProduced?.Invoke(mat.materialType, transform.position);

        // Increase break chance per produced output
        _chanceToBreak += _chanceToBreakIncrement;

        if (BreakCheck())
        {
            Debug.Log("M: machine broke!");
            Break();
            return; 
        }

        TryPushOutputToBelt(mat);
    }

    public void Break()
    {
        if (_isBroken) return;
        _isBroken = true;

        // Stop ongoing production cycle
        if (productionRoutine != null)
        {
            StopCoroutine(productionRoutine);
            productionRoutine = null;
        }

        OnMachineBroken?.Invoke(this, transform.position);
    }

    public void Repair()
    {
        if (!_isBroken) return;
        _isBroken = false;
        _chanceToBreak = 0f;

        OnMachineRepaired?.Invoke(this);
        StartProduction();
    }

    #region Belt Output Logic

    // --- Output belt acceptance logic ---
    // Straight belt: must face outward (worldSide).
    // Corner belt: accept if machine outward side equals belt forward OR either orthogonal turn side.
    private bool BeltAcceptsOutput(ConveyorBelt belt, GridOrientation worldSide)
    {
        if (belt == null) return false;

        if (!belt.IsCorner && !belt.IsTurnPrefab) // plain straight
            return belt.Orientation == worldSide;

        

        // Corner visual: outward side can be belt forward or its CW/CCW rotation
        var fwd = belt.Orientation;
        if (fwd == worldSide) return true;
        if (fwd.RotatedCW() == worldSide) return true;
        if (fwd.RotatedCCW() == worldSide) return true;
        return false;
    }

    private void TryPushOutputToBelt(MaterialData mat)
    {
        if (_grid == null || !_grid.HasGrid) return;

        var orientedSize = BaseSize.OrientedSize(Orientation);
        var outputs = new List<(Vector2Int cell, GridOrientation worldSide)>();
        Debug.Log("M: outputs ready");

        if (data != null && data.ports != null && data.ports.Count > 0)
        {
            foreach (var p in data.ports)
            {
                if (p.kind != MachinePortType.Output) continue;
                var worldSide = RotateSide(p.side, Orientation);
                var cell = ComputePortCell(p.side, orientedSize, p.offset);
                outputs.Add((cell, worldSide));
            }
        }
        else
        {
            var worldSide = Orientation;
            var cell = ComputePortCell(Orientation, orientedSize, -1);
            outputs.Add((cell, worldSide));
        }

        GameObject visualPrefab = MaterialVisualRegistry.Instance != null
            ? MaterialVisualRegistry.Instance.GetPrefab(mat.materialType)
            : null;

        bool placed = false;

        foreach (var (cell, worldSide) in outputs)
        {
            if (!_grid.TryGetCell(cell, out var cd) || cd.occupant == null) continue;
            GameObject occGO = cd.occupant as GameObject ?? (cd.occupant as Component)?.gameObject;
            if (occGO == null) continue;
            var belt = occGO.GetComponent<ConveyorBelt>();
            if (!BeltAcceptsOutput(belt, worldSide)) continue;

            GameObject visual = null;
            if (visualPrefab != null) visual = Instantiate(visualPrefab);

            var item = new ConveyorItem(mat, visual);
            if (belt.TrySetItem(item))
            {
                placed = true;
                break;
            }
            if (visual != null) Destroy(visual);
        }

        if (!placed)
            AddOutputToInventory(mat, 1);
    }

    private List<ConveyorBelt> GetConnectedOutputBelts()
    {
        var belts = new List<ConveyorBelt>();
        if (_grid == null || !_grid.HasGrid) return belts;

        var orientedSize = BaseSize.OrientedSize(Orientation);
        var outputs = new List<(Vector2Int cell, GridOrientation worldSide)>();

        if (data != null && data.ports != null && data.ports.Count > 0)
        {
            foreach (var p in data.ports)
            {
                if (p.kind != MachinePortType.Output) continue;
                var worldSide = RotateSide(p.side, Orientation);
                var cell = ComputePortCell(p.side, orientedSize, p.offset);
                outputs.Add((cell, worldSide));
            }
        }
        else
        {
            var worldSide = Orientation;
            var cell = ComputePortCell(Orientation, orientedSize, -1);
            outputs.Add((cell, worldSide));
        }

        foreach (var (cell, worldSide) in outputs)
        {
            if (!_grid.TryGetCell(cell, out var cd) || cd.occupant == null) continue;
            GameObject occGO = cd.occupant as GameObject ?? (cd.occupant as Component)?.gameObject;
            if (occGO == null) continue;
            var belt = occGO.GetComponent<ConveyorBelt>();
            if (belt == null) continue;

            if (BeltAcceptsOutput(belt, worldSide) && !belt.HasItem)
                belts.Add(belt);
        }

        return belts;
    }

    private void AddOutputToInventory(MaterialData mat, int amount)
    {
        var svc = InventoryService.Instance;
        if (svc == null) return;
        svc.AddOrStack(mat, amount);
    }
    #endregion

    public void Upgrade()
    {
        if (data == null) { Debug.LogError("Machine.Upgrade: MachineData not set."); return; }
        if (upgradeLevel < data.upgrades.Count)
        {
            var upgrade = data.upgrades[upgradeLevel];
            data.processingTime *= upgrade.prrocessingSpeedMultiplier;
            upgradeLevel++;
        }
    }

    private bool BreakCheck()
    {
        float random = UnityEngine.Random.Range(_miniMimumChanceToBreak, 100f);
        return random < _chanceToBreak;
    }

    #region Interaction
    // --- Interaction (IInteractable) ---
    public void OnTap()
    {
        if (data == null)
        {
            //Debug.LogError("Machine.OnTap: MachineData not set.");
            return;
        }
        Debug.Log($"Machine {data.machineName} tapped.");
        if (_isBroken)
        {
            // Forward to BrokenMachineManager to open UI if tapping the machine itself
            var mgr = FindFirstObjectByType<BrokenMachineManager>();
            if (mgr != null) mgr.OpenRepairUI(this);
        }
    }
    public void OnHold() { }

    #endregion

    #region Drag
    // --- Drag (IDraggable) ---
    public bool CanDrag => true;
    public Transform DragTransform => transform;
    public void OnDragStart() { }
    public void OnDrag(Vector3 worldPosition) { DragTransform.position = worldPosition; }
    public void OnDragEnd()
    {
        // When drag finishes, re-scan for output belts at new position/orientation.
        ScanOutputsAndStartIfPossible();
    }

    #endregion

    #region Grid Placement 
    // --- Grid Footprint & Placement (IGridOccupant) ---
    public Vector2Int BaseSize => data != null ? data.size : Vector2Int.one;
    public GridOrientation Orientation { get; private set; } = GridOrientation.North;
    public Vector2Int Anchor { get; private set; }

    public void SetPlacement(Vector2Int anchor, GridOrientation orientation)
    {
        Anchor = anchor;
        Orientation = orientation;
        transform.rotation = orientation.ToRotation();

        ScanOutputsAndStartIfPossible();
    }

    public bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation orientation)
    {
        if (grid == null) return false;
        var size = BaseSize.OrientedSize(orientation);
        return grid.IsAreaInside(anchor, size) && grid.IsAreaFree(anchor, size, this);
    }

    public void ApplyWorldFromPlacement(GridService grid)
    {
        if (grid == null) return;
        var size = BaseSize.OrientedSize(Orientation);
        float y = grid.Origin.y;
        float wx = grid.Origin.x + (Anchor.x + size.x * 0.5f) * grid.CellSize;
        float wz = grid.Origin.z + (Anchor.y + size.y * 0.5f) * grid.CellSize;
        transform.position = new Vector3(wx, y, wz);
    }
    #endregion


    #region Conveyor I/O & Inventory
    // --- Conveyor I/O & Inventory bridge ---
    private void ScanOutputsAndStartIfPossible()
    {
        if (_isBroken) return;

        // Generators need at least one outward-facing belt; other machines may start only if inputs are satisfied.
        // TryStartIfIdle will internally gate based on recipe/input availability.
        if (HasConnectedOutputBelt())
            TryStartIfIdle();
    }

    public void OnConveyorItemArrived(MaterialData material)
    {
        if (Data == null) return;

        if (data.HasRecipes)
        {
            // Accept only materials that appear in at least one recipe
            if (!AppearsInAnyRecipe(material.materialType)) return;
            AddToBuffer(material.materialType, 1);
            StartProduction();
            return;
        }

        // Legacy single input
        if (Data.inputMaterial.materialType != MaterialType.None && Data.inputMaterial.materialType != material.materialType) return;
        _inputQueue.Enqueue(material.materialType);
        StartProduction();
    }

    public bool CanAcceptInventoryItem(MaterialData item)
    {
        if (item == null || Data == null) return false;
        if (data.HasRecipes) return AppearsInAnyRecipe(item.materialType);
        return Data.inputMaterial.materialType == MaterialType.None || Data.inputMaterial.materialType == item.materialType;
    }

    public int TryQueueInventoryItem(MaterialData item, int amount)
    {
        if (item == null || amount <= 0) return 0;
        return TryQueueInventoryMaterial(item.materialType, amount);
    }

    public int TryQueueInventoryMaterial(MaterialType material, int amount)
    {
        if (Data == null || amount <= 0) return 0;

        if (data.HasRecipes)
        {
            if (!AppearsInAnyRecipe(material)) return 0;
            AddToBuffer(material, amount);
            StartProduction();
            return amount;
        }

        // Legacy single input
        if (Data.inputMaterial.materialType != MaterialType.None && Data.inputMaterial.materialType != material) return 0;
        for (int i = 0; i < amount; i++) _inputQueue.Enqueue(material);
        StartProduction();
        return amount;
    }

    private bool AppearsInAnyRecipe(MaterialType mat)
    {
        if (!data.HasRecipes) return false;
        for (int i = 0; i < data.recipes.Count; i++)
        {
            var r = data.recipes[i];
            for (int j = 0; j < r.inputs.Count; j++)
                if (r.inputs[j].material.materialType == mat) return true;
        }
        return false;
    }

    private void AddToBuffer(MaterialType mat, int amount)
    {
        if (!_buffer.TryGetValue(mat, out int have)) have = 0;
        _buffer[mat] = have + Mathf.Max(1, amount);
    }

    // True if at least one acceptable belt is connected to any output
    private bool HasConnectedOutputBelt()
    {
        if (_grid == null || !_grid.HasGrid) return false;

        var orientedSize = BaseSize.OrientedSize(Orientation);
        var outputs = new List<(Vector2Int cell, GridOrientation worldSide)>();

        if (data != null && data.ports != null && data.ports.Count > 0)
        {
            foreach (var p in data.ports)
            {
                if (p.kind != MachinePortType.Output) continue;
                var worldSide = RotateSide(p.side, Orientation);
                var cell = ComputePortCell(p.side, orientedSize, p.offset);
                outputs.Add((cell, worldSide));
            }
        }
        else
        {
            var worldSide = Orientation;
            var cell = ComputePortCell(Orientation, orientedSize, -1);
            outputs.Add((cell, worldSide));
        }

        foreach (var (cell, worldSide) in outputs)
        {
            if (!_grid.TryGetCell(cell, out var cd) || cd.occupant == null) continue;
            GameObject occGO = cd.occupant as GameObject ?? (cd.occupant as Component)?.gameObject;
            if (occGO == null) continue;
            var belt = occGO.GetComponent<ConveyorBelt>();
            if (BeltAcceptsOutput(belt, worldSide))
                return true;
        }
        return false;
    }

    public bool TryGetBeltConnection(ConveyorBelt belt, out MachinePortType portType, bool requireFacing = true)
    {
        portType = MachinePortType.None;
        if (belt == null) return false;

        if (data == null || data.ports == null || data.ports.Count == 0)
        {
            if (IsBeltOnSide(belt, Orientation, MachinePortType.Output, offset: -1, requireFacing))
            {
                portType = MachinePortType.Output;
                if (IsLegacyGenerator && productionRoutine == null && !_isBroken) StartProduction();
                return true;
            }
            return false;
        }

        foreach (var port in data.ports)
        {
            if (IsBeltOnSide(belt, port.side, port.kind, port.offset, requireFacing))
            {
                portType = port.kind;
                if (portType == MachinePortType.Output && IsLegacyGenerator && productionRoutine == null && !_isBroken)
                    StartProduction();
                return true;
            }
        }
        return false;
    }

    private bool IsBeltOnSide(ConveyorBelt belt, GridOrientation localSide, MachinePortType kind, int offset, bool requireFacing)
    {
        var orientedSize = BaseSize.OrientedSize(Orientation);
        GridOrientation worldSide = RotateSide(localSide, Orientation);
        Vector2Int portCell = ComputePortCell(localSide, orientedSize, offset);

        if (belt == null || belt.Anchor != portCell) return false;
        if (!requireFacing) return true;

        if (kind == MachinePortType.Output)
            return BeltAcceptsOutput(belt, worldSide);

        if (kind == MachinePortType.Input)
            return belt.Orientation == Opposite(worldSide);

        return false;
    }

    private Vector2Int ComputePortCell(GridOrientation localSide, Vector2Int size, int offset)
    {
        GridOrientation side = RotateSide(localSide, Orientation);
        int sideLen = (side == GridOrientation.North || side == GridOrientation.South) ? size.x : size.y;
        int idx = offset < 0 ? Mathf.Max(0, (sideLen - 1) / 2) : Mathf.Clamp(offset, 0, Mathf.Max(0, sideLen - 1));

        switch (side)
        {
            case GridOrientation.North: return new Vector2Int(Anchor.x + idx, Anchor.y + size.y);
            case GridOrientation.South: return new Vector2Int(Anchor.x + idx, Anchor.y - 1);
            case GridOrientation.East:  return new Vector2Int(Anchor.x + size.x, Anchor.y + idx);
            case GridOrientation.West:  return new Vector2Int(Anchor.x - 1, Anchor.y + idx);
            default: return Anchor;
        }
    }
    #endregion

    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);
}
