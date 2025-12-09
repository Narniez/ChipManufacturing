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
    private readonly Queue<MaterialData> _inputQueue = new Queue<MaterialData>();

    // Recipe-mode input buffer per material
    private readonly Dictionary<MaterialData, int> _buffer = new Dictionary<MaterialData, int>();
    private MachineRecipe _currentRecipe;
    private bool _inventoryDumpedThisCycle = false;

    // Machine breaking 
    private float _chanceToBreak = 0f;
    private float _chanceToBreakIncrement = 2f;
    private float _miniMimumChanceToBreak = 0;
    private bool _isBroken = false;

    private bool _initialized; 

    public static event Action<Machine, Vector3> OnMachineBroken;
    public static event Action<Machine> OnMachineRepaired;
    public static event Action<MaterialData, Vector3> OnMaterialProduced;

    //Progress tracking for UI
    private float _productionProgress = 0f; // 0..1
    public float ProductionProgress => Mathf.Clamp01(_productionProgress);
    public event Action<float> ProductionProgressChanged;

    public MachineData Data => data;
    public bool IsProducing => productionRoutine != null;
    public bool IsBroken => _isBroken;

    // Treat as "generator" if no recipes, no input, but has an output
    private bool IsLegacyGenerator =>
        data != null &&
        !data.HasRecipes &&
        (data.inputMaterial == null) &&
        data.outputMaterial != null;

    private void OnEnable()
    {
        if (_grid == null) _grid = FindFirstObjectByType<GridService>();
        // Only resume if machine was initialized (avoid running before Initialize())
        if (_initialized) TryStartIfIdle();
    }

    // Clear stale coroutine handle when disabled
    private void OnDisable()
    {
        if (productionRoutine != null)
        {
            try { StopCoroutine(productionRoutine); } catch { }
            productionRoutine = null;
        }

        // Reset progress when disabled so UI does not show stale value
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);
    }

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

        _initialized = true;

        StartProduction();
    }

    public void TryStartIfIdle()
    {
        if (!_isBroken && productionRoutine == null)
            StartProduction();
    }

    // Try to start one cycle if inputs are ready
    private void StartProduction()
    {
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
            // Reset progress and notify
            _productionProgress = 0f;
            ProductionProgressChanged?.Invoke(_productionProgress);
            productionRoutine = StartCoroutine(ProcessOneRecipe(dur));
        }
        else
        {
            // Legacy behavior with input queue
            if (_inputQueue.Count > 0)
            {
                _productionProgress = 0f;
                ProductionProgressChanged?.Invoke(_productionProgress);
                productionRoutine = StartCoroutine(ProcessOneLegacy());
                return;
            }

            // Legacy generator: no inputs, only output; run only if at least one output belt is connected
            if (IsLegacyGenerator && HasConnectedOutputBelt())
            {
                _productionProgress = 0f;
                ProductionProgressChanged?.Invoke(_productionProgress);
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
                _buffer.TryGetValue(req.material, out int have);
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
            if (_buffer.TryGetValue(req.material, out int have))
            {
                _buffer[req.material] = Mathf.Max(0, have - Mathf.Max(1, req.amount));
            }
        }
        _inventoryDumpedThisCycle = false;
    }

    private IEnumerator ProcessOneRecipe(float duration)
    {
        Debug.Log("M: start recipe");

        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            _productionProgress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            ProductionProgressChanged?.Invoke(_productionProgress);

            // If machine got broken mid-process, abort
            if (_isBroken)
            {
                _productionProgress = 0f;
                ProductionProgressChanged?.Invoke(_productionProgress);
                productionRoutine = null;
                yield break;
            }
        }

        _productionProgress = 1f;
        ProductionProgressChanged?.Invoke(_productionProgress);

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
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);

        // Attempt next recipe if inputs are available
        StartProduction();
    }

    private IEnumerator ProcessOneLegacy()
    {
        // Consume one and process
        var consumed = _inputQueue.Dequeue();

        float duration = data.processingTime;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            _productionProgress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            ProductionProgressChanged?.Invoke(_productionProgress);

            if (_isBroken)
            {
                _productionProgress = 0f;
                ProductionProgressChanged?.Invoke(_productionProgress);
                productionRoutine = null;
                yield break;
            }
        }

        _productionProgress = 1f;
        ProductionProgressChanged?.Invoke(_productionProgress);

        // Produce legacy single output (data.outputMaterial)
        ProduceOneOutput(data.outputMaterial);

        productionRoutine = null;
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);
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
        float duration = data.processingTime;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            _productionProgress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            ProductionProgressChanged?.Invoke(_productionProgress);

            if (_isBroken)
            {
                _productionProgress = 0f;
                ProductionProgressChanged?.Invoke(_productionProgress);
                productionRoutine = null;
                yield break;
            }
        }

        _productionProgress = 1f;
        ProductionProgressChanged?.Invoke(_productionProgress);

        var belts = GetConnectedOutputBelts();
        if (belts.Count == 0)
        {
            // No empty belt right now: schedule a quick retry instead of giving up
            productionRoutine = null;
            // Small delay lets belt movement finish after chain extension
            StartCoroutine(DelayedGeneratorRetry(0.15f));
            _productionProgress = 0f;
            ProductionProgressChanged?.Invoke(_productionProgress);
            yield break;
        }

        foreach (var belt in belts)
        {
            if (_isBroken) break;
            OnMaterialProduced?.Invoke(data.outputMaterial, transform.position);
            _chanceToBreak += _chanceToBreakIncrement;
            if (BreakCheck())
            {
                Break();
                break;
            }

            GameObject visualPrefab = data.outputMaterial != null ? data.outputMaterial.prefab : null;
            GameObject visual = null;
            if (visualPrefab != null) visual = Instantiate(visualPrefab);

            var item = new ConveyorItem(data.outputMaterial, visual);
            if (!belt.TrySetItem(item) && visual != null)
                Destroy(visual);
        }

        productionRoutine = null;
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);
        StartProduction();
    }

    private void ProduceOneOutput(MaterialData mat)
    {
        if (_isBroken) return;

        Debug.Log($"[Machine] {name} produced one '{mat.materialName}'");
        OnMaterialProduced?.Invoke(mat, transform.position);

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

        // Reset progress and notify UI immediately
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);

        // Persist broken state
        GameStateSync.TrySetMachineBroken(this, true);

        OnMachineBroken?.Invoke(this, transform.position);
    }

    public void Repair()
    {
        if (!_isBroken) return;
        _isBroken = false;
        _chanceToBreak = 0f;

        // Persist repaired state
        GameStateSync.TrySetMachineBroken(this, false);

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

        GameObject visualPrefab = mat != null ? mat.prefab : null;

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
        TutorialEventBus.PublishMaterialProduced();
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
        ScanOutputsAndStartIfPossible();
        GameStateSync.TryUpdateMachineOrientation(this); // If orientation changed during drag+rotate workflow.
    }

    public Vector2Int BaseSize => data != null ? data.size : Vector2Int.one;
    public GridOrientation Orientation { get; private set; } = GridOrientation.North;
    public Vector2Int Anchor { get; private set; }

    private void EnsureGrid()
    {
        if (_grid == null) _grid = FindFirstObjectByType<GridService>();
    }

    public void SetPlacement(Vector2Int anchor, GridOrientation orientation)
    {
        Anchor = anchor;
        Orientation = orientation;
        transform.rotation = orientation.ToRotation();

        EnsureGrid();
        ApplyWorldFromPlacement(_grid);

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
        if (Data == null || material == null) return;

        if (data.HasRecipes)
        {
            // Accept only materials that appear in at least one recipe
            if (!AppearsInAnyRecipe(material)) return;
            AddToBuffer(material, 1);
            StartProduction();
            return;
        }

        // Legacy single input
        if (Data.inputMaterial != null && Data.inputMaterial != material) return;
        _inputQueue.Enqueue(material);
        StartProduction();
    }

    public bool CanAcceptInventoryItem(MaterialData item)
    {
        if (item == null || Data == null) return false;
        if (data.HasRecipes) return AppearsInAnyRecipe(item);
        return Data.inputMaterial == null || Data.inputMaterial == item;
    }

    public int TryQueueInventoryItem(MaterialData item, int amount)
    {
        if (item == null || amount <= 0) return 0;
        return TryQueueInventoryMaterial(item, amount);
    }

    public int TryQueueInventoryMaterial(MaterialData material, int amount)
    {
        if (Data == null || material == null || amount <= 0) return 0;

        if (data.HasRecipes)
        {
            if (!AppearsInAnyRecipe(material)) return 0;
            AddToBuffer(material, amount);
            StartProduction();
            return amount;
        }

        // Legacy single input
        if (Data.inputMaterial != null && Data.inputMaterial != material) return 0;
        for (int i = 0; i < amount; i++) _inputQueue.Enqueue(material);
        StartProduction();
        return amount;
    }

    private bool AppearsInAnyRecipe(MaterialData mat)
    {
        if (mat == null || !data.HasRecipes) return false;
        for (int i = 0; i < data.recipes.Count; i++)
        {
            var r = data.recipes[i];
            for (int j = 0; j < r.inputs.Count; j++)
                if (r.inputs[j].material == mat) return true;
        }
        return false;
    }

    private void AddToBuffer(MaterialData mat, int amount)
    {
        if (mat == null || amount <= 0) return;
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
