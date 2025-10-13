using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Machine : MonoBehaviour, IInteractable, IDraggable, IGridOccupant
{
    private MachineData data;   // (Optional) allow prefab default; factory may overwrite.
    private int upgradeLevel = 0;
    private Coroutine productionRoutine;

    public event System.Action<MaterialType, Vector3> OnMaterialProduced;

    // --- Production / Data ---

    public MachineData Data => data;

    public void Initialize(MachineData machineData)
    {
        data = machineData;
        StartProduction();
    }

    private void StartProduction()
    {
        if (data == null)
        {
            Debug.LogError("Machine.StartProduction: MachineData not set.");
            return;
        }

        if (productionRoutine != null)
            StopCoroutine(productionRoutine);

        productionRoutine = StartCoroutine(ProductionLoop());
    }

    private IEnumerator ProductionLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(data.processingTime);
            ProduceOutput();
        }
    }

    private void ProduceOutput()
    {
        if (data == null) return;
        OnMaterialProduced?.Invoke(data.outputMaterial, transform.position);
    }

    public void Upgrade()
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
            StartProduction();
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
        Debug.Log($"Machine {data.machineName} tapped. Upgrade level: {upgradeLevel}");
    }

    public void OnHold()
    {
        // Optional: highlight / feedback when hold begins before drag.
    }

    // --- Drag (IDraggable) ---

    public bool CanDrag => true;
    public Transform DragTransform => transform;

    public void OnDragStart()
    {
        // Optional: visual pickup (e.g., raise slightly, glow)
    }

    public void OnDrag(Vector3 worldPosition)
    {
        DragTransform.position = worldPosition;
    }

    public void OnDragEnd()
    {
        // Optional: finalize visual, drop animation
    }



    // --- Grid Footprint & Placement (IGridOccupant) ---

    // Base (unrotated) grid size from MachineData
    public Vector2Int BaseSize => data != null ? data.size : Vector2Int.one;

    public GridOrientation Orientation { get; private set; } = GridOrientation.North;
    public Vector2Int Anchor { get; private set; }   // Bottom-left cell occupied in current orientation

    public void SetPlacement(Vector2Int anchor, GridOrientation orientation)
    {
        Anchor = anchor;
        Orientation = orientation;

        transform.rotation = orientation.ToRotation();
        // World position is applied externally during drag; optionally sync here if needed:
        ///ApplyWorldFromPlacement(gridServiceReference);
    }

    public bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation orientation)
    {
        if (grid == null) return false;
        var size = BaseSize.OrientedSize(orientation);
        return grid.IsAreaInside(anchor, size) && grid.IsAreaFree(anchor, size, this);
    }

    // Optional helper if you store a gridService and want to recompute world from anchor later:
    public void ApplyWorldFromPlacement(GridService grid)
    {
        if (grid == null) return;
        var size = BaseSize.OrientedSize(Orientation);
        float y = grid.Origin.y;
        float wx = grid.Origin.x + (Anchor.x + size.x * 0.5f) * grid.CellSize;
        float wz = grid.Origin.z + (Anchor.y + size.y * 0.5f) * grid.CellSize;
        transform.position = new Vector3(wx, y, wz);
    }

    // --- Belt connection helpers ---

    // Returns true if 'belt' occupies a declared port cell on this machine; portType tells if Input or Output.
    // If requireFacing is true:
    //  - Output port: belt must face away from the machine (belt.Orientation == worldSide)
    //  - Input  port: belt must face toward the machine (belt.Orientation == Opposite(worldSide))

    public void OnConveyorItemArrived(MaterialType material)
    {
        // For now: start production if not running (simple test behavior)
        // Extend later to consume buffer / match inputMaterial, etc.
        if (Data != null && (Data.inputMaterial == MaterialType.None || Data.inputMaterial == material))
        {
            // If productionRoutine is private, this will still restart the loop as needed
            StartProduction();
        }
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

    // Get all world cells for declared ports of a given type (useful for visualization)
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

        // Convert local side to world side by rotating with current Orientation
        GridOrientation worldSide = RotateSide(localSide, Orientation);

        // Belt must sit on the adjacent cell on that world side at the specified offset
        Vector2Int portCell = ComputePortCell(localSide, orientedSize, offset);

        if (belt.Anchor != portCell) return false;

        if (!requireFacing) return true;

        // Check belt facing
        if (kind == MachinePortType.Output)
            return belt.Orientation == worldSide;

        if (kind == MachinePortType.Input)
            return belt.Orientation == Opposite(worldSide);

        return false;
    }

    private Vector2Int ComputePortCell(GridOrientation localSide, Vector2Int size, int offset)
    {
        // Convert local side to world side
        GridOrientation side = RotateSide(localSide, Orientation);

        // Side length: horizontal sides span size.x, vertical sides span size.y
        int sideLen = (side == GridOrientation.North || side == GridOrientation.South) ? size.x : size.y;
        int idx = offset < 0 ? Mathf.Max(0, (sideLen - 1) / 2) : Mathf.Clamp(offset, 0, Mathf.Max(0, sideLen - 1));

        switch (side)
        {
            case GridOrientation.North:
                return new Vector2Int(Anchor.x + idx, Anchor.y + size.y);
            case GridOrientation.South:
                return new Vector2Int(Anchor.x + idx, Anchor.y - 1);
            case GridOrientation.East:
                return new Vector2Int(Anchor.x + size.x, Anchor.y + idx);
            case GridOrientation.West:
                return new Vector2Int(Anchor.x - 1, Anchor.y + idx);
            default:
                return Anchor;
        }
    }

    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
    {
        // (local + by) mod 4
        return (GridOrientation)(((int)local + (int)by) & 3);
    }

    private static GridOrientation Opposite(GridOrientation o)
    {
        return (GridOrientation)(((int)o + 2) & 3);
    }
}
