using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Machine : MonoBehaviour, IInteractable, IDraggable, IGridOccupant
{
    [Header("Optional visuals")]
    [SerializeField, Tooltip("Visual prefab used for produced items on belts (optional)")]
    private GameObject itemVisualPrefab;
    [SerializeField] private InventoryItem inventoryItemPrefab;

    private MachineData data;   // Factory sets this via Initialize
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;

    private InventoryItem inventoryItem;

    // Legacy single-input queue (used only if no recipes defined)
    private readonly Queue<MaterialType> _inputQueue = new Queue<MaterialType>();

    // Recipe-mode input buffer per material
    private readonly Dictionary<MaterialType, int> _buffer = new Dictionary<MaterialType, int>();
    private MachineRecipe _currentRecipe;
    private bool _inventoryDumpedThisCycle = false;


    public event Action<MaterialType, Vector3> OnMaterialProduced;
    public event Action<Machine> OnQueueChanged; // UI can subscribe

    // Expose minimal state
    public MachineData Data => data;
    public bool IsProducing => productionRoutine != null;

    public void Initialize(MachineData machineData)
    {
        data = machineData;
        _buffer.Clear();
        _inputQueue.Clear();
        _currentRecipe = null;
    }

    // Try to start one cycle if inputs are ready
    private void StartProduction()
    {
        if (data == null) { Debug.LogError("Machine.StartProduction: MachineData not set."); return; }
        if (productionRoutine != null) return;

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
            if (_inputQueue.Count == 0) return;
            productionRoutine = StartCoroutine(ProcessOneLegacy());
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
        OnQueueChanged?.Invoke(this);
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
        OnQueueChanged?.Invoke(this);

        yield return new WaitForSeconds(data.processingTime);

        // Produce legacy single output (data.outputMaterial)
        ProduceOneOutput(data.outputMaterial);

        productionRoutine = null;
        StartProduction();
    }

    private void ProduceOneOutput(MaterialData mat)
    {
        Debug.Log("M: produce one");
        OnMaterialProduced?.Invoke(mat.materialType, transform.position);
        TryPushOutputToBelt(mat);
    }

    private void TryPushOutputToBelt(MaterialData mat)
    {
        var grid = FindFirstObjectByType<GridService>();
        if (grid == null || !grid.HasGrid) return;

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

        // Resolve item visual
        GameObject visualPrefab = MaterialVisualRegistry.Instance != null ? MaterialVisualRegistry.Instance.GetPrefab(mat.materialType) : null;
        if (visualPrefab == null) visualPrefab = itemVisualPrefab;

        bool foundBeltAtOutput = false;

        foreach (var (cell, worldSide) in outputs)
        {
            if (!grid.TryGetCell(cell, out var cd) || cd.occupant == null) continue;

            GameObject occGO = cd.occupant as GameObject;
            if (occGO == null)
            {
                var comp = cd.occupant as Component;
                occGO = comp != null ? comp.gameObject : null;
            }
            if (occGO == null) continue;

            var belt = occGO.GetComponent<ConveyorBelt>();
            if (belt == null) continue;

            //Debug.Log("M: belt ahead has no valid forward sink (dead-end)");
            foundBeltAtOutput = true;

            GameObject visual = null;
            if (visualPrefab != null) visual = Instantiate(visualPrefab);

            var item = new ConveyorItem(mat, visual);
            if (belt.TrySetItem(item))
            {
                Debug.Log("M: placed on belt");
                break;
            }

            if (visual != null) Destroy(visual);
        }

        if (!foundBeltAtOutput)
        {
            AddOutputToInventory(mat, 1);
            Debug.Log("M: added to inventory (dead-end)");
        }
    }

    // Add a single material to inventory
    private void AddOutputToInventory(MaterialData mat, int amount)
    {
        if (/*mat == null ||*/ inventoryItemPrefab == null) return;
        var svc = InventoryService.Instance;
        if (svc == null) return;

        //InventoryItem item = Instantiate(inventoryItemPrefab);
       // item.Setup(mat, amount);
        svc.AddOrStack(mat, amount);
        Debug.Log("M: should have been added to inventory");
    }

  /*  public void AddOutputToInventory(MachineRecipe recipe)
    {
        if (recipe == null || recipe.outputs == null) return;

        foreach (var outStack in recipe.outputs)
        {
            if (outStack.material == null)
            {
                Debug.LogWarning("MaterialStack is missing MaterialData");
                continue;
            }
            InventoryItem item = Instantiate(inventoryItemPrefab);
            item.Setup(outStack.material, outStack.amount);
            InventoryService.Instance.AddToInventoryPanel(item, outStack.amount);
        }
    }*/

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

    // --- Interaction (IInteractable) ---
    public void OnTap()
    {
        if (data == null)
        {
            Debug.LogError("Machine.OnTap: MachineData not set.");
            return;
        }
        Debug.Log($"Machine {data.machineName} tapped.");
    }
    public void OnHold() { }

    // --- Drag (IDraggable) ---
    public bool CanDrag => true;
    public Transform DragTransform => transform;
    public void OnDragStart() { }
    public void OnDrag(Vector3 worldPosition) { DragTransform.position = worldPosition; }
    public void OnDragEnd() { }

    // --- Grid Footprint & Placement (IGridOccupant) ---
    public Vector2Int BaseSize => data != null ? data.size : Vector2Int.one;
    public GridOrientation Orientation { get; private set; } = GridOrientation.North;
    public Vector2Int Anchor { get; private set; }

    public void SetPlacement(Vector2Int anchor, GridOrientation orientation)
    {
        Anchor = anchor;
        Orientation = orientation;
        transform.rotation = orientation.ToRotation();
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

    // --- Conveyor I/O & Inventory bridge ---

    // Called by belts when a conveyor item arrives on an INPUT port
    public void OnConveyorItemArrived(MaterialData material)
    {
        if (Data == null) return;

        if (data.HasRecipes)
        {
            // Accept only materials that appear in at least one recipe
            if (!AppearsInAnyRecipe(material.materialType)) return;
            AddToBuffer(material.materialType, 1);
            OnQueueChanged?.Invoke(this);
            StartProduction();
            return;
        }

        // Legacy single input
        if (Data.inputMaterial.materialType != MaterialType.None && Data.inputMaterial.materialType != material.materialType) return;
        _inputQueue.Enqueue(material.materialType);
        OnQueueChanged?.Invoke(this);
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
            OnQueueChanged?.Invoke(this);
            StartProduction();
            return amount;
        }

        // Legacy single input
        if (Data.inputMaterial.materialType != MaterialType.None && Data.inputMaterial.materialType != material) return 0;
        for (int i = 0; i < amount; i++) _inputQueue.Enqueue(material);
        OnQueueChanged?.Invoke(this);
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

    // --- Port helpers (unchanged) ---
    public bool TryGetBeltConnection(ConveyorBelt belt, out MachinePortType portType, bool requireFacing = true)
    {
        portType = MachinePortType.None;
        if (belt == null) return false;

        if (data == null || data.ports == null || data.ports.Count == 0)
        {
            if (IsBeltOnSide(belt, Orientation, MachinePortType.Output, offset: -1, requireFacing))
            {
                portType = MachinePortType.Output;
                return true;
            }
            return false;
        }

        foreach (var port in data.ports)
        {
            if (IsBeltOnSide(belt, port.side, port.kind, port.offset, requireFacing))
            {
                portType = port.kind;
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

        if (belt.Anchor != portCell) return false;
        if (!requireFacing) return true;

        if (kind == MachinePortType.Output) return belt.Orientation == worldSide;
        if (kind == MachinePortType.Input) return belt.Orientation == Opposite(worldSide);
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
            case GridOrientation.East: return new Vector2Int(Anchor.x + size.x, Anchor.y + idx);
            case GridOrientation.West: return new Vector2Int(Anchor.x - 1, Anchor.y + idx);
            default: return Anchor;
        }
    }

  
    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);
}
