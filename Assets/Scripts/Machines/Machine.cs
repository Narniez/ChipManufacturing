using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using ProceduralMusic;
using System.Reflection;

public class Machine : MonoBehaviour, IInteractable, IDraggable, IGridOccupant
{
    // Factory sets this via Initialize
    private MachineData data;   
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;
    private GridService _grid;
    // Pending outputs produced by a finished recipe, released on next clock tick
    private readonly List<MaterialData> _pendingOutputs = new List<MaterialData>();

    //private InventoryItem inventoryItem;

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
    private bool _isPlaced = false;
    private bool _initialized;

    public static event Action<Machine, Vector3> OnMachineBroken;
    public static event Action<Machine> OnMachineRepaired;
    // Include MachineRecipe so consumers (sounds, analytics) know which recipe produced the material.
    public static event Action<MaterialData, Vector3, MachineRecipe> OnMaterialProduced;

    //Progress tracking for UI
    private float _productionProgress = 0f; // 0..1
    public float ProductionProgress => Mathf.Clamp01(_productionProgress);
    public event Action<float> ProductionProgressChanged;

    public MachineData Data => data;
    // Expose current recipe for other systems (sound/etc) to query while producing.
    public MachineRecipe CurrentRecipe => _currentRecipe;
    public bool IsProducing => productionRoutine != null;
    public bool IsBroken => _isBroken;

    public bool IsPlaced => _isPlaced;

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
        // subscribe to machine-phase clock ticks (pre-belt)
        ProceduralMusicManager.OnClockTick_Machines += HandleClockTick;
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
        // unsubscribe from clock ticks if subscribed
        try { ProceduralMusicManager.OnClockTick_Machines -= HandleClockTick; } catch { }
    }

    public void Initialize(MachineData machineData)
    {
        data = machineData;
        _buffer.Clear();
        _inputQueue.Clear();
        _currentRecipe = null;
        _isPlaced = true;
        _chanceToBreak = 0f;
        _chanceToBreakIncrement = data.chanceIncreasePerOutput;
        _miniMimumChanceToBreak = data.minimunChanceToBreak;
        _isBroken = false;
        _grid = FindFirstObjectByType<GridService>();

        _initialized = true;

        // Subscribe to clock ticks only once, after initialization
        // ensure we're subscribed to the pre-belt clock phase
        try { ProceduralMusicManager.OnClockTick_Machines += HandleClockTick; } catch { }
        StartProduction();

        // --- auto-attach MachineSoundData using DataRegistry (preferred) or fallback to Resources registry ---
        try
        {
            var registry = DataRegistry.Instance ?? DataRegistry.FindOrLoad();
            MachineSoundData msd = null;
            if (registry != null)
            {
                msd = registry.GetMachineSoundDataForMachineData(data);
            }

            if (msd != null)
            {
                var msComp = GetComponent<MachineSound>();
                if (msComp == null) msComp = gameObject.AddComponent<MachineSound>();
                msComp.AssignSoundData(msd);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Machine.Initialize: error assigning MachineSoundData: {ex}");
        }
    }

    public void TryStartIfIdle()
    {
        if (_isBroken || productionRoutine != null) return;
        if (!_initialized || data == null) return; // guard: not ready yet
        StartProduction();
    }

    // Try to start one cycle if inputs are ready
    private void StartProduction()
    {
        if (productionRoutine != null) return;
        if (_isBroken) return;
        if (data == null)
        {
            Debug.LogWarning("Machine.StartProduction: MachineData not set.");
            return;
        }

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

        // Instead of producing immediately, collect outputs and wait for the next clock tick to release them.
        _pendingOutputs.Clear();
        if (_currentRecipe != null)
        {
            for (int i = 0; i < _currentRecipe.outputs.Count; i++)
            {
                var outStack = _currentRecipe.outputs[i];
                if (outStack.material == null)
                {
                    Debug.LogWarning($"[Machine] {_currentRecipe.name} has null output at index {i}; skipping.");
                    continue;
                }
                int count = Mathf.Max(1, outStack.amount);
                for (int c = 0; c < count; c++)
                    _pendingOutputs.Add(outStack.material);
            }
        }

        // mark production finished; actual release happens on clock tick handler
        productionRoutine = null;
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);

        // Do NOT StartProduction() here; StartProduction() will be called after outputs are released on tick.
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

        // Legacy single output: queue output to be released on next clock tick
        _pendingOutputs.Clear();
        if (data.outputMaterial != null)
            _pendingOutputs.Add(data.outputMaterial);

        productionRoutine = null;
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);
        // Do not StartProduction() here — outputs will be released on the next clock tick, then StartProduction() is called.
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

        // Queue one output per connected belt; actual release (events, break checks, visual instantiation
        // and belt placement) will occur on the next clock tick in HandleClockTick().
        _pendingOutputs.Clear();
        for (int i = 0; i < belts.Count; i++)
        {
            if (data.outputMaterial != null)
                _pendingOutputs.Add(data.outputMaterial);
        }

        productionRoutine = null;
        _productionProgress = 0f;
        ProductionProgressChanged?.Invoke(_productionProgress);
        // Do not StartProduction() here; StartProduction() will be called after outputs are released on tick.
    }

    private void ProduceOneOutput(MaterialData mat)
    {
        if (_isBroken) return;
        Debug.Log($"[Machine] {name} produced one '{mat.materialName}'");
        // Include current recipe when notifying listeners.
        OnMaterialProduced?.Invoke(mat, transform.position, _currentRecipe);

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
        {
            AddOutputToInventory(mat, 1);
            Debug.Log("M: output added to inventory");
        }
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
        // localSide is defined relative to "North" model; Orientation rotates it to world
        var worldSide = RotateSide(localSide, Orientation);

        // Length of the edge in world
        int sideLen = (worldSide == GridOrientation.North || worldSide == GridOrientation.South)
            ? size.x
            : size.y;

        // Resolve local index along the side
        int idxLocal = offset < 0
            ? Mathf.Max(0, (sideLen - 1) / 2) // center; for even choose lower-middle
            : Mathf.Clamp(offset, 0, Mathf.Max(0, sideLen - 1));

        // Mirror index for East and South so "left-to-right" in LOCAL space stays intuitive
        int idxWorld = (worldSide == GridOrientation.East || worldSide == GridOrientation.South)
            ? (sideLen - 1 - idxLocal)
            : idxLocal;

        // Build world cell from bottom-left Anchor
        switch (worldSide)
        {
            case GridOrientation.North:
                // top edge: x runs left->right from Anchor.x
                return new Vector2Int(Anchor.x + idxWorld, Anchor.y + size.y);
            case GridOrientation.South:
                // bottom edge: x runs left->right from Anchor.x
                return new Vector2Int(Anchor.x + idxWorld, Anchor.y - 1);
            case GridOrientation.East:
                // right edge: y runs top->bottom from Anchor.y
                return new Vector2Int(Anchor.x + size.x, Anchor.y + idxWorld);
            case GridOrientation.West:
                // left edge: y runs top->bottom from Anchor.y
                return new Vector2Int(Anchor.x - 1, Anchor.y + idxWorld);
            default:
                return Anchor;
        }
    }
    #endregion

    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);

    // Called on each clock tick from ProceduralMusicManager
    private void HandleClockTick()
    {
        // Guard against receiving ticks before initialization or when data is missing
        if (!_initialized || data == null) return;

        if (_isBroken) return;

        // If a recipe finished and produced pending outputs, release them now
        if (_pendingOutputs.Count > 0)
        {
            Debug.Log($"[Machine] {name} releasing {_pendingOutputs.Count} pending outputs");
            // produce each pending output (this will try to push to belts / inventory)
            for (int i = 0; i < _pendingOutputs.Count; i++)
            {
                Debug.Log($"[Machine] {name} producing index {i}");
                ProduceOneOutput(_pendingOutputs[i]);
                if (_isBroken) break; // stop if machine broke during release
            }
            _pendingOutputs.Clear();
            // After releasing outputs, attempt to start next production
            StartProduction();
            return;
        }
        // Otherwise keep normal behavior: some machines may want to start/resume on tick
        TryStartIfIdle();
    }
}

// Extension helpers to provide a safe AssignSoundData call for older/newer MachineSound implementations.
// Uses reflection to try common field/property/method names; logs a warning if nothing matches.
public static class MachineSoundExtensions
{
    public static void AssignSoundData(this MachineSound msComp, MachineSoundData msd)
    {
        if (msComp == null || msd == null) return;

        var t = msComp.GetType();

        // Try common field names
        var field = t.GetField("soundData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsAssignableFrom(typeof(MachineSoundData)))
        {
            field.SetValue(msComp, msd);
            return;
        }

        field = t.GetField("_soundData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsAssignableFrom(typeof(MachineSoundData)))
        {
            field.SetValue(msComp, msd);
            return;
        }

        // Try common property names
        var prop = t.GetProperty("SoundData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite && prop.PropertyType.IsAssignableFrom(typeof(MachineSoundData)))
        {
            prop.SetValue(msComp, msd);
            return;
        }

        // Try common method names that might perform assignment/initialization
        var method = t.GetMethod("AssignSoundData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
        {
            try { method.Invoke(msComp, new object[] { msd }); return; } catch { /* ignore and continue */ }
        }

        method = t.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
        {
            try { method.Invoke(msComp, new object[] { msd }); return; } catch { /* ignore and continue */ }
        }

        method = t.GetMethod("SetSoundData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
        {
            try { method.Invoke(msComp, new object[] { msd }); return; } catch { /* ignore and continue */ }
        }

        Debug.LogWarning($"AssignSoundData: Could not assign MachineSoundData to MachineSound component on '{msComp.gameObject.name}'; no compatible field/property/method found.");
    }
}
