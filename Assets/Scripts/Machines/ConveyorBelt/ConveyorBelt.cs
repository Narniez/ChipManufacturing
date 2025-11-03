using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class ConveyorBelt : MonoBehaviour, IGridOccupant, IInteractable
{
    public enum BeltTurnKind { None, Left, Right }

    [SerializeField] private GridOrientation orientation = GridOrientation.North;

    [Header("Item Visual")]
    [SerializeField, Tooltip("Y offset for item visuals above ground.")] 
    private float itemHeight = 0.8f;

    [Header("Prefab Kind")]
    [SerializeField, Tooltip("Mark this prefab as a 'Turn' conveyor visual. Set this on the prefabs.")]
    private bool isTurnPrefab = false;

    public bool IsTurnPrefab => isTurnPrefab;

    public GridOrientation Orientation => orientation;
    public Vector2Int Anchor { get; private set; }
    public Vector2Int BaseSize => Vector2Int.one;

    private ConveyorItem _item;         
    private GridService _grid;

    // Which corner this turn visually represents (used to add extra yaw to match the turn prefab)
    private BeltTurnKind _turnKind = BeltTurnKind.None;

    private void Awake()
    {
        _grid = FindFirstObjectByType<GridService>();
    }

    private void OnEnable()
    {
        BeltSystemRuntime.Instance?.Register(this);

        // Notify adjacent machines next frame (ensures grid occupancy is up-to-date)
        StartCoroutine(NotifyMachines());
    }

    private IEnumerator NotifyMachines()
    {
        yield return null;
        NotifyAdjacentMachinesOfConnection();
    }

    private void OnDisable()
    {
        BeltSystemRuntime.Instance?.Unregister(this);
    }

    public bool HasItem => _item != null;
    public ConveyorItem PeekItem() => _item;

    // snapVisual: true for initial spawn; false for belt-to-belt handoff
    public bool TrySetItem(ConveyorItem item, bool snapVisual = true)
    {
        if (_item != null || item == null) return false;
        _item = item;

        if (snapVisual && _item.Visual != null && _grid != null)
        {
            _item.Visual.transform.position = GetWorldCenter();
            _item.T = 1f;
            _item.Duration = 0f;
        }
        return true;
    }

    public ConveyorItem TakeItem()
    {
        var item = _item;
        _item = null;
        return item;
    }

    // IGridOccupant
    public void SetPlacement(Vector2Int anchor, GridOrientation newOrientation)
    {
        Anchor = anchor;
        orientation = newOrientation;
        transform.rotation = newOrientation.ToRotation();

        // Also ping when placement changes
        NotifyAdjacentMachinesOfConnection();
    }

    public bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation newOrientation)
    {
        if (grid == null) return false;
        return grid.IsAreaInside(anchor, Vector2Int.one) && grid.IsAreaFree(anchor, Vector2Int.one, this);
    }

    // Allow PlacementManager to set what kind of turn (affects visual yaw offset)
    public void SetTurnKind(BeltTurnKind kind)
    {
        _turnKind = kind;
        ApplyTurnVisualRotation();
    }

    private void ApplyTurnVisualRotation()
    {
        // Base rotation from logical orientation
        transform.rotation = orientation.ToRotation();

        // For turn prefabs, apply extra yaw so right-corners end up with Y=180 (as requested)
        if (isTurnPrefab)
        {
            if (_turnKind == BeltTurnKind.Right)
            {
                // Example: logic East (90) +90 => 180 for your right-turn mesh
                transform.rotation *= Quaternion.Euler(0f, 90f, 0f);
            }
            else if (_turnKind == BeltTurnKind.Left)
            {
                // If your left-turn mesh needs an offset, add it here.
                // transform.rotation *= Quaternion.Euler(0f, <leftY>, 0f);
            }
        }
    }

    // IDraggable
    public bool CanDrag => true;
    public Transform DragTransform => transform;
    public void OnDragStart() { 
        if(_item != null && _item.Visual != null)
        {
            Destroy(_item.Visual);
            _item = null;
        }
    }
    public void OnDrag(Vector3 worldPosition) { transform.position = worldPosition; }
    public void OnDragEnd() { }

    // IInteractable
    public void OnTap() { }
    public void OnHold() { }

    // Movement logic invoked by tick system
    public void TickMoveAttempt()
    {
        if (_item == null || _grid == null || !_grid.HasGrid) return;

        var forwardDir = orientation;
        var rightDir   = orientation.RotatedCW();
        var leftDir    = orientation.RotatedCCW();

        var forwardCell = Anchor + ToDelta(forwardDir);
        var rightCell   = Anchor + ToDelta(rightDir);
        var leftCell    = Anchor + ToDelta(leftDir);

        if (TryMoveOntoNeighborBelt(forwardCell)) return;
        if (TryMoveOntoNeighborBelt(rightCell))   return;
        if (TryMoveOntoNeighborBelt(leftCell))    return;

        // Forward-only machine delivery
        if (TryDeliverToMachine(forwardCell)) return;
    }


    private bool TryMoveOntoNeighborBelt(Vector2Int cell)
    {
        if (!_grid.TryGetCell(cell, out var data) || data.occupant == null) return false;

        GameObject occGO = data.occupant as GameObject;
        if (occGO == null)
        {
            var comp = data.occupant as Component;
            occGO = comp != null ? comp.gameObject : null;
        }
        if (occGO == null) return false;

        var nextBelt = occGO.GetComponent<ConveyorBelt>();
        if (nextBelt == null) return false;

        if (IsOpposite(nextBelt.Orientation, orientation)) return false;
        if (nextBelt.HasItem) return false;

        // Prepare animation from current center to next center
        var moving = TakeItem();
        if (moving.Visual != null)
        {
            var from = GetWorldCenter();
            var to   = nextBelt.GetWorldCenter();
            var dur  = BeltSystemRuntime.Instance != null ? BeltSystemRuntime.Instance.ItemMoveDuration : 0.2f;
            moving.BeginMove(from, to, dur);
        }

        // Handoff without snapping visual
        if (nextBelt.TrySetItem(moving, snapVisual: false))
            return true;

        // Failed, put it back
        TrySetItem(moving, snapVisual: true);
        return false;
    }

    private bool TryDeliverToMachine(Vector2Int cell)
    {
        if (!_grid.TryGetCell(cell, out var data) || data.occupant == null) return false;

        GameObject occGO = data.occupant as GameObject;
        if (occGO == null)
        {
            var comp = data.occupant as Component;
            occGO = comp != null ? comp.gameObject : null;
        }
        if (occGO == null) return false;

        var machine = occGO.GetComponent<Machine>();
        if (machine == null || machine.IsBroken) return false;

        if (machine.TryGetBeltConnection(this, out var portType, requireFacing: true) &&
            portType == MachinePortType.Input)
        {
            var moving = TakeItem();
            if (moving.Visual != null) Destroy(moving.Visual);
            machine.OnConveyorItemArrived(moving.materialData);
            return true;
        }
        return false;
    }

    public Vector3 GetWorldCenter()
    {
        float yBase = _grid != null ? _grid.Origin.y : 0f;
        return (_grid != null
            ? _grid.CellToWorldCenter(Anchor, yBase)
            : transform.position)
            + Vector3.up * itemHeight;
    }

    private static bool IsOpposite(GridOrientation a, GridOrientation b)
        => (int)a == (((int)b + 2) & 3);

    private static Vector2Int ToDelta(GridOrientation o)
    {
        switch (o)
        {
            case GridOrientation.North: return Vector2Int.up;
            case GridOrientation.East:  return Vector2Int.right;
            case GridOrientation.South: return Vector2Int.down;
            case GridOrientation.West:  return Vector2Int.left;
            default: return Vector2Int.zero;
        }
    }

    private void NotifyAdjacentMachinesOfConnection()
    {
        if (_grid == null || !_grid.HasGrid) return;

        // Check the four neighbors; any machine that considers this an OUTPUT port should be nudged to start
        foreach (var nb in _grid.GetNeighbors(Anchor))
        {
            if (!_grid.TryGetCell(nb.coord, out var data) || data.occupant == null) continue;

            GameObject occGO = data.occupant as GameObject;
            if (occGO == null)
            {
                var comp = data.occupant as Component;
                occGO = comp != null ? comp.gameObject : null;
            }
            if (occGO == null) continue;

            var machine = occGO.GetComponent<Machine>();
            if (machine == null || machine.IsBroken) continue;

            if (machine.TryGetBeltConnection(this, out var portType, requireFacing: true) &&
                portType == MachinePortType.Output)
            {
                machine.TryStartIfIdle();
            }
        }
    }
}