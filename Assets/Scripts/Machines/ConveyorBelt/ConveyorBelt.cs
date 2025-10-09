using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConveyorBelt : MonoBehaviour, IGridOccupant, IConveyorReciever, IInteractable
{
    [SerializeField] private GridOrientation orientation = GridOrientation.North;
    public GridOrientation Orientation => orientation;

    public Vector2Int Anchor { get; private set; }
    public Vector2Int BaseSize => Vector2Int.one;

    private ConveyorItem _item;    
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
        if (_item != null) return false;
        _item = item;
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

    // IDraggable (through IGridOccupant chain) – you can disable dragging if not needed
    public bool CanDrag => true;
    public Transform DragTransform => transform;
    public void OnDragStart() { }
    public void OnDrag(Vector3 worldPosition) { transform.position = worldPosition; }
    public void OnDragEnd() { }

    // IInteractable
    public void OnTap()
    {
        Debug.Log($"Belt tapped at {Anchor}, item={(HasItem ? _item.Material.ToString() : "None")}");
    }
    public void OnHold() { }

    // Receiver interface
    public bool CanAccept(MaterialType material) => _item == null;
    public bool TryAccept(ConveyorItem item) => TrySetItem(item);

    // Movement logic invoked by tick system
    public void TickMoveAttempt()
    {
        if (_item == null) return;

        Vector2Int nextCell = Anchor + Orientation.ToDirection().ToDelta();
        if (_grid == null || !_grid.IsInside(nextCell)) return;

        if (_grid.TryGetCell(nextCell, out var data) && data.occupant != null)
        {
            var go = (data.occupant as Component)?.gameObject;
            if (go == null) return;

            // Next belt?
            if (go.TryGetComponent<ConveyorBelt>(out var nextBelt))
            {
                if (nextBelt.CanAccept(_item.Material))
                {
                    var moving = TakeItem();
                    nextBelt.TryAccept(moving);
                }
                return;
            }

            // A machine? (Optional future)
            if (go.TryGetComponent<Machine>(out var machine))
            {
                // Implement machine.TryAcceptMaterial if desired
                // if(machine.TryAcceptMaterial(_item.Material, Orientation.ToDirection())) { TakeItem(); }
            }
        }
    }

    public bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation orientation)
    {
        throw new System.NotImplementedException();
    }
}