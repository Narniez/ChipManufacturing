using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConveyorBelt : MonoBehaviour, IGridOccupant, IInteractable
{
    [SerializeField] private GridOrientation orientation = GridOrientation.North;

    [Header("Item Visual")]
    [SerializeField, Tooltip("Y offset for item visuals above ground.")] 
    private float itemHeight = 0.8f;

    public GridOrientation Orientation => orientation;
    public Vector2Int Anchor { get; private set; }
    public Vector2Int BaseSize => Vector2Int.one;

    private ConveyorItem _item;          // single-slot
    private GridService _grid;

    private void Awake()
    {
        _grid = FindFirstObjectByType<GridService>();
    }

    private void OnEnable()
    {
        BeltSystemRuntime.Instance?.Register(this);
    }

    private void OnDisable()
    {
        BeltSystemRuntime.Instance?.Unregister(this);
    }

    public bool HasItem => _item != null;
    public ConveyorItem PeekItem() => _item;

    public bool TrySetItem(ConveyorItem item)
    {
        if (_item != null || item == null) return false;
        _item = item;

        // On spawn, place visual at belt center with Y offset
        if (_item.Visual != null && _grid != null)
        {
            _item.Visual.transform.position = GetWorldCenter();
        }
        return true;
    }

    public ConveyorItem TakeItem()
    {
        var it = _item;
        _item = null;
        return it;
    }

    // IGridOccupant
    public void SetPlacement(Vector2Int anchor, GridOrientation newOrientation)
    {
        Anchor = anchor;
        orientation = newOrientation;
        transform.rotation = newOrientation.ToRotation();
    }

    public bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation newOrientation)
    {
        if (grid == null) return false;
        // Belts are 1x1
        return grid.IsAreaInside(anchor, Vector2Int.one) && grid.IsAreaFree(anchor, Vector2Int.one, this);
    }

    // IDraggable (IGridOccupant extends IDraggable)
    public bool CanDrag => true;
    public Transform DragTransform => transform;
    public void OnDragStart() { }
    public void OnDrag(Vector3 worldPosition) { transform.position = worldPosition; }
    public void OnDragEnd() { }

    // IInteractable
    public void OnTap()
    {
        Debug.Log($"Belt {Anchor}, orientation {orientation}, hasItem={HasItem}");
    }
    public void OnHold() { }

    // Movement logic invoked by tick system
    public void TickMoveAttempt()
    {
        if (_item == null || _grid == null || !_grid.HasGrid) return;

        // Candidate neighbors in priority: forward, right, left (relative to this belt)
        var forwardDir = orientation;
        var rightDir   = orientation.RotatedCW();
        var leftDir    = orientation.RotatedCCW();

        var forwardCell = Anchor + ToDelta(forwardDir);
        var rightCell   = Anchor + ToDelta(rightDir);
        var leftCell    = Anchor + ToDelta(leftDir);

        // Try move onto a neighboring belt that is not facing opposite and is empty
        if (TryMoveOntoNeighborBelt(forwardCell)) return;
        if (TryMoveOntoNeighborBelt(rightCell))   return;
        if (TryMoveOntoNeighborBelt(leftCell))    return;

        // Forward-only machine delivery (keep machine handoff only in front)
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

        // Block only if next belt faces exactly opposite this belt
        if (IsOpposite(nextBelt.Orientation, orientation)) return false;
        if (nextBelt.HasItem) return false;

        // Move item; keep visual at previous position, let runtime animate to target
        var moving = TakeItem();
        if (nextBelt.TrySetItem(moving))
        {
            if (moving.Visual != null)
            {
                // Reset visual to current center so it slides to next center smoothly
                moving.Visual.transform.position = GetWorldCenter();
            }
        }
        return true;
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
        if (machine == null) return false;

        if (machine.TryGetBeltConnection(this, out var portType, requireFacing: true) &&
            portType == MachinePortType.Input)
        {
            var moving = TakeItem();
            if (moving.Visual != null) Destroy(moving.Visual);
            machine.OnConveyorItemArrived(moving.Material);
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
}