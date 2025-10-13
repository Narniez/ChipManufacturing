using System.Collections;
using UnityEngine;

public class Machine : MonoBehaviour, IInteractable, IDraggable, IGridOccupant
{
    [SerializeField] private MachineData data;   // (Optional) allow prefab default; factory may overwrite.
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

        if (productionRoutine != null)
            StopCoroutine(productionRoutine);

        productionRoutine = StartCoroutine(ProductionLoop());
    }

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
        Debug.Log($"Machine {data.machineName} produced {data.outputMaterial} at upgrade level {upgradeLevel}.");
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
}
