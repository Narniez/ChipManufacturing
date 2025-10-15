using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Machine : MonoBehaviour, IInteractable, IDraggable, IGridOccupant
{
    [Header("Optional visuals")]
    [SerializeField, Tooltip("Visual prefab used for produced items on belts (optional)")]
    private GameObject itemVisualPrefab;

    private MachineData data;   // (Optional) allow prefab default; factory may overwrite.
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;

    private int inputBuffer = 0; //how many materials will go into the machine

    public event System.Action<MaterialType, Vector3> OnMaterialProduced;

    // --- Production / Data ---

    public MachineData Data => data;

    public void Initialize(MachineData machineData)
    {
        data = machineData;
       // StartProduction();
    }

    public void StartProduction()
    {
        if (data == null)
        {
            Debug.LogError("Machine.StartProduction: MachineData not set.");
            return;
        }

        if (productionRoutine != null) return;          // already processing one
        if (_inputQueue.Count == 0) return;             // nothing to process

        productionRoutine = StartCoroutine(ProcessOne());
    }

    // Process exactly one input from the queue, produce output, then stop
    private IEnumerator ProcessOne()
    public bool AcceptMaterial(MaterialData material)
    {
        if (data == null || material == null) return false;
        return data.inputMaterial == material.materialType;
    }

    public void QueueInput(int quantity)
    {
        if (data == null || quantity <= 0) return;

        inputBuffer += quantity;

        if (productionRoutine == null)
            productionRoutine = StartCoroutine(ProductionLoop());
    }


    private IEnumerator ProductionLoop()
    {
        var consumed = _inputQueue.Dequeue();
        yield return new WaitForSeconds(data.processingTime);

        ProduceOutput();

        // Stop this cycle
        productionRoutine = null;

        // If more inputs are queued, start next cycle
        if (_inputQueue.Count > 0)
            StartProduction();
        while (inputBuffer > 0)
        {
            yield return new WaitForSeconds(data.processingTime);
            inputBuffer--;
            ProduceOutput();
        }
        productionRoutine = null;
    }

    private void ProduceOutput()
    {
        if (data == null) return;

        // Optional external signal
        OnMaterialProduced?.Invoke(data.outputMaterial, transform.position);
        Debug.Log($"Machine produced {data.outputMaterial}");

        // Try push onto an output belt (if connected and facing away)
        TryPushOutputToBelt(data.outputMaterial);
    }

    private void TryPushOutputToBelt(MaterialType mat)
    {
        var grid = FindFirstObjectByType<GridService>();
        if (grid == null || !grid.HasGrid) return;

        var orientedSize = BaseSize.OrientedSize(Orientation);

        // Build candidate output cells with their world-side (used to reject opposite-facing belts)
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
            // Fallback: single output on forward-center
            var worldSide = Orientation;
            var cell = ComputePortCell(Orientation, orientedSize, -1);
            outputs.Add((cell, worldSide));
        }

        // Resolve visual prefab for this material (scene registry -> per-machine fallback)
        GameObject visualPrefab = MaterialVisualRegistry.Instance != null
            ? MaterialVisualRegistry.Instance.GetPrefab(mat)
            : null;
        if (visualPrefab == null) visualPrefab = itemVisualPrefab;

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

            // Reject belts facing back into the machine only; allow straight or turns
            if (belt.Orientation == Opposite(worldSide)) continue;
            if (belt.HasItem) continue;

            GameObject visual = null;
            if (visualPrefab != null)
                visual = Instantiate(visualPrefab);

            var item = new ConveyorItem(mat, visual);

            // Place on the belt; belt will snap/animate the visual as configured
            if (belt.TrySetItem(item))
                return;

            // Failed to place; cleanup visual and try next candidate
            if (visual != null) Destroy(visual);
        }
    }

    private void Upgrade()
    {
        if (data == null)
        {
            Debug.LogError("Machine.Upgrade: MachineData not set.");
            return;
        }

        if (upgradeLevel < data.upgrades.Count)
        {
            var upgrade = data.upgrades[upgradeLevel];
            data.processingTime *= upgrade.prrocessingSpeedMultiplier;
            upgradeLevel++;
            // Production continues per-item; no need to restart here
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
        Debug.Log($"Machine {data.machineName} tapped. Upgrade level: {upgradeLevel}, inQ={_inputQueue.Count}, producing={(productionRoutine != null)}");
    }

    public void OnHold()
    {
        // Optional
    }

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

    // --- Conveyor I/O ---

    // Called by belts when a conveyor item arrives on an INPUT port
    public void OnConveyorItemArrived(MaterialType material)
    {
        if (Data == null) return;

        // Accept only matching inputs (or None = any)
        if (Data.inputMaterial != MaterialType.None && Data.inputMaterial != material)
            return;

        _inputQueue.Enqueue(material);
        StartProduction(); // processes one item; stops; continues if queue has more
    }

    public bool TryGetBeltConnection(ConveyorBelt belt, out MachinePortType portType, bool requireFacing = true)
    {
        portType = MachinePortType.None;
        if (belt == null) return false;

        if (data == null || data.ports == null || data.ports.Count == 0)
        {
            // Fallback: single output on the machine forward side, centered
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

    public IEnumerable<Vector2Int> GetPortCells(MachinePortType kind)
    {
        var orientedSize = BaseSize.OrientedSize(Orientation);

        if (data == null || data.ports == null || data.ports.Count == 0)
        {
            if (kind == MachinePortType.Output)
                yield return ComputePortCell(Orientation, orientedSize, -1);
            yield break;
        }

        foreach (var port in data.ports)
        {
            if (port.kind != kind) continue;
            yield return ComputePortCell(port.side, orientedSize, port.offset);
        }
    }

    private bool IsBeltOnSide(ConveyorBelt belt, GridOrientation localSide, MachinePortType kind, int offset, bool requireFacing)
    {
        var orientedSize = BaseSize.OrientedSize(Orientation);
        GridOrientation worldSide = RotateSide(localSide, Orientation);
        Vector2Int portCell = ComputePortCell(localSide, orientedSize, offset);

        if (belt.Anchor != portCell) return false;

        if (!requireFacing) return true;

        if (kind == MachinePortType.Output) return belt.Orientation == worldSide;            // away
        if (kind == MachinePortType.Input)  return belt.Orientation == Opposite(worldSide);  // toward
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

    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);
}
