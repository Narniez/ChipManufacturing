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

    [Header("Shape Control")]
    [SerializeField, Tooltip("If true (set at runtime), an existing corner will not auto-downgrade back to straight.")]
    private bool lockCorner = false;

    [Header("Economy")]
    [SerializeField] private int cost = 50;   // tweak in Inspector
    public int Cost => cost;

    public bool IsTurnPrefab => isTurnPrefab;
    public bool CornerLocked => lockCorner;
    public GridOrientation Orientation => orientation;
    public Vector2Int Anchor { get; private set; }
    public Vector2Int BaseSize => Vector2Int.one;

    public ConveyorBelt PreviousInChain { get; set; }
    public ConveyorBelt NextInChain { get; set; }

    private ConveyorItem _item;
    private GridService _grid;
    private BeltTurnKind _turnKind = BeltTurnKind.None;
    private bool _isDragging;
    private bool _isConnectedToMachine;

    private void Awake() => _grid = FindFirstObjectByType<GridService>();

    private void OnEnable()
    {
        BeltSystemRuntime.Instance?.Register(this);
        StartCoroutine(DeferredInit());
    }

    private IEnumerator DeferredInit()
    {
        yield return null;

        // Re-snap existing item visual after reactivation (e.g. after isolation)
        if (_item != null && _item.Visual != null)
            _item.Visual.transform.position = GetWorldCenter();

        NotifyAdjacentMachinesOfConnection();
        RefreshChainLinks();

        // Avoid overwriting an existing saved belt entry during load.
        // If GameState already contains an entry for this anchor, assume loader
        // will restore item state and skip writing here.
        var svc = GameStateService.Instance;
        bool hasSavedEntry = false;
        if (svc?.State != null)
        {
            hasSavedEntry = svc.State.belts.Exists(x => x.anchor == Anchor);
        }

        if (!hasSavedEntry)
            GameStateSync.TryAddOrUpdateBelt(this); // write only if no pre-existing save entry
    }

    private void OnDisable()
    {
        // Disabled due to scene isolation or replacement; keep belt entry & item state
        UnlinkFromChain();
        BeltSystemRuntime.Instance?.Unregister(this);
        // keep GameState entry until actual destruction
    }

    private void OnDestroy()
    {
        GameStateSync.TryRemoveBelt(this);
        if (_item != null && _item.Visual != null) Destroy(_item.Visual);
        _item = null;
    }

    public void LockCorner() { if (isTurnPrefab) lockCorner = true; }
    public void UnlockCorner() { lockCorner = false; }

    public bool HasItem => _item != null;
    public ConveyorItem PeekItem() => _item;

    // Called during normal runtime placement/movement
    public bool TrySetItem(ConveyorItem item, bool snapVisual = true)
    {
        // Do not accept items while this belt is being dragged
        if (_isDragging) return false;

        if (_item != null || item == null) return false;
        _item = item;
        if (snapVisual && _item.Visual != null && _grid != null)
        {
            _item.Visual.transform.position = GetWorldCenter();
            _item.smoothTime = 1f;
            _item.Duration = 0f;
        }
        // Update persisted belt item state
        GameStateSync.TryAddOrUpdateBelt(this);
        return true;
    }

    // Loader-only helper: force-attach an item (bypasses drag/state checks)
    public void RestoreItem(ConveyorItem item)
    {
        if (item == null) return;

        // Clean up any existing visual attached to current item
        if (_item != null && _item.Visual != null)
            Destroy(_item.Visual);

        _item = item;

        // Snap visual to belt if available and grid ready
        if (_item.Visual != null)
        {
            EnsureGrid();
            if (_grid != null)
            {
                _item.Visual.transform.position = GetWorldCenter();
                _item.smoothTime = 1f;
                _item.Duration = 0f;
            }
        }

        // Persist restored state (now that item is attached)
        GameStateSync.TryAddOrUpdateBelt(this);
    }

    public ConveyorItem TakeItem()
    {
        var item = _item;
        _item = null;
        GameStateSync.TryAddOrUpdateBelt(this);
        return item;
    }

    private void EnsureGrid()
    {
        if (_grid == null) _grid = FindFirstObjectByType<GridService>();
    }

    public void SetPlacement(Vector2Int anchor, GridOrientation newOrientation)
    {
        Anchor = anchor;
        orientation = newOrientation;

        EnsureGrid();
        ApplyWorldFromPlacement();

        NotifyAdjacentMachinesOfConnection();
        ApplyTurnVisualRotation();
        RefreshChainLinks();
    }

    private void ApplyWorldFromPlacement()
    {
        EnsureGrid();
        if (_grid == null) return;
        float y = _grid.Origin.y; // ground Y
        Vector2Int size = BaseSize; // 1x1
        float wx = _grid.Origin.x + (Anchor.x + size.x * 0.5f) * _grid.CellSize;
        float wz = _grid.Origin.z + (Anchor.y + size.y * 0.5f) * _grid.CellSize;
        transform.position = new Vector3(wx, y, wz);
    }

    public bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation newOrientation) =>
        grid != null && grid.IsAreaInside(anchor, Vector2Int.one) && grid.IsAreaFree(anchor, Vector2Int.one, this);

    public void SetTurnKind(BeltTurnKind kind)
    {
        _turnKind = kind;
        ApplyTurnVisualRotation();
    }

    private void ApplyTurnVisualRotation()
    {
        transform.rotation = orientation.ToRotation();
        if (isTurnPrefab)
        {
            if (_turnKind == BeltTurnKind.Right)
                transform.rotation *= Quaternion.Euler(0f, 90f, 0f);
        }
    }

    public bool CanDrag => true;
    public Transform DragTransform => transform;

    public void OnDragStart()
    {
        _isDragging = true;
        UnlockCorner();
        if (_item != null && _item.Visual != null)
        {
            Destroy(_item.Visual);
            _item = null;
        }
    }

    public void OnDrag(Vector3 worldPosition) => transform.position = worldPosition;

    public void OnDragEnd()
    {
        _isDragging = false;
        RefreshChainLinks();
        GameStateSync.TryUpdateBeltOrientation(this);
    }

    public void OnTap() { }
    public void OnHold() { }

    // public facing - attempt to move once; returns true when a move occurred.
    // This is a small signature change (was void) so callers can know if a move happened.
    public bool TickMoveAttempt()
    {
        if (_item == null) return false;
        if (IsItemAnimating()) return false;

        EnsureForwardLinkStrict();

        if (NextInChain != null && TryMoveOntoChainChild(NextInChain))
            return true;

        if (TryMoveOntoForwardBeltFallback())
            return true;

        // Delivery attempt if no belt ahead
        if (TryDeliverToMachine(Anchor + GridOrientationExtentions.OrientationToDelta(orientation)))
            return true;

        return false;
    }

    // Strict chain link maintenance (does not override existing parent on forward belt unless invalid)
    private void EnsureForwardLinkStrict()
    {
        if (_grid == null || !_grid.HasGrid) return;
        if (NextInChain != null)
        {
            // Validate adjacency; if orientation changed (corner promotion) but link invalid => clear
            if (Anchor + GridOrientationExtentions.OrientationToDelta(orientation) != NextInChain.Anchor)
            {
                if (NextInChain.PreviousInChain == this)
                    NextInChain.PreviousInChain = null;
                NextInChain = null;
            }
            else return;
        }

        var forwardCell = Anchor + GridOrientationExtentions.OrientationToDelta(orientation);
        if (_grid.TryGetCell(forwardCell, out var data) && data.occupant != null)
        {
            var go = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            var forwardBelt = go != null ? go.GetComponent<ConveyorBelt>() : null;
            if (forwardBelt != null)
            {
                // If forward belt's claimed parent is invalid (not actually behind it) allow relink
                bool parentInvalid =
                    forwardBelt.PreviousInChain == null ||
                    forwardBelt.PreviousInChain.Anchor + GridOrientationExtentions.OrientationToDelta(forwardBelt.PreviousInChain.orientation) != forwardBelt.Anchor;

                if (parentInvalid)
                {
                    if (forwardBelt.PreviousInChain != null && forwardBelt.PreviousInChain.NextInChain == forwardBelt)
                        forwardBelt.PreviousInChain.NextInChain = null;

                    forwardBelt.PreviousInChain = this;
                    NextInChain = forwardBelt;
                }
            }
        }
    }

    // Fallback direct forward scan (ignores chain fields). Used when corner promotion broke links temporarily.
    private bool TryMoveOntoForwardBeltFallback()
    {
        if (_grid == null || !_grid.HasGrid) return false;

        var forwardCell = Anchor + GridOrientationExtentions.OrientationToDelta(orientation);
        if (!_grid.TryGetCell(forwardCell, out var data) || data.occupant == null) return false;

        GameObject occGO = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
        if (occGO == null) return false;

        var forwardBelt = occGO.GetComponent<ConveyorBelt>();
        if (forwardBelt == null || forwardBelt.HasItem) return false;

        if (forwardBelt.PreviousInChain == null)
        {
            forwardBelt.PreviousInChain = this;
            NextInChain = forwardBelt;
        }

        return TryMoveOntoChainChild(forwardBelt);
    }

    // Ensure we ping machines when this belt becomes empty after moving an item forward
    private bool TryMoveOntoChainChild(ConveyorBelt child)
    {
        if (child == null || child.HasItem) return false;
        var moving = TakeItem();
        if (moving == null) return false;

        if (child.TrySetItem(moving, snapVisual: false))
        {
            if (moving.Visual != null)
            {
                var from = GetWorldCenter();
                var to = child.GetWorldCenter();
                float dur = BeltSystemRuntime.Instance != null ? BeltSystemRuntime.Instance.ItemMoveDuration : 0.2f;
                moving.BeginMove(from, to, dur);
            }
            NotifyAdjacentMachinesOfConnection();
            return true;
        }

        TrySetItem(moving, snapVisual: true);
        return false;
    }

    private bool TryDeliverToMachine(Vector2Int cell)
    {
        if (_grid == null || !_grid.TryGetCell(cell, out var data) || data.occupant == null) return false;

        GameObject occGO = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
        if (occGO == null) return false;

        var machine = occGO.GetComponent<Machine>();
        if (machine == null || machine.IsBroken || machine.IsProducing) return false;

        if (machine.TryGetBeltConnection(this, out var portType, requireFacing: true) &&
            portType == MachinePortType.Input)
        {
            var moving = TakeItem();
            if (moving?.Visual != null) Destroy(moving.Visual);
            machine.OnConveyorItemArrived(moving.materialData);
            return true;
        }
        return false;
    }

    public Vector3 GetWorldCenter()
    {
        float yBase = _grid != null ? _grid.Origin.y : 0f;
        return (_grid != null ? _grid.CellToWorldCenter(Anchor, yBase) : transform.position) + Vector3.up * itemHeight;
    }

    public void RefreshChainLinks()
    {
        if (_grid == null || !_grid.HasGrid) return;

        // Validate existing previous link
        if (PreviousInChain != null &&
            PreviousInChain.Anchor + GridOrientationExtentions.OrientationToDelta(PreviousInChain.orientation) != Anchor)
        {
            if (PreviousInChain.NextInChain == this)
                PreviousInChain.NextInChain = null;
            PreviousInChain = null;
        }

        // Validate existing next link
        if (NextInChain != null &&
            Anchor + GridOrientationExtentions.OrientationToDelta(orientation) != NextInChain.Anchor)
        {
            if (NextInChain.PreviousInChain == this)
                NextInChain.PreviousInChain = null;
            NextInChain = null;
        }

        // Rebuild backward link if missing
        var backCell = Anchor + GridOrientationExtentions.OrientationToDelta(Opposite(orientation));
        if (PreviousInChain == null &&
            _grid.TryGetCell(backCell, out var backData) && backData.occupant != null)
        {
            var backGO = backData.occupant as GameObject ?? (backData.occupant as Component)?.gameObject;
            var backBelt = backGO != null ? backGO.GetComponent<ConveyorBelt>() : null;
            if (backBelt != null)
            {
                // Ensure physical adjacency matches forward of backBelt
                if (backBelt.Anchor + GridOrientationExtentions.OrientationToDelta(backBelt.orientation) == Anchor)
                {
                    PreviousInChain = backBelt;
                    backBelt.NextInChain = this;
                }
            }
        }
        EnsureForwardLinkStrict();
    }

    public void UnlinkFromChain()
    {
        if (PreviousInChain != null && PreviousInChain.NextInChain == this)
            PreviousInChain.NextInChain = NextInChain;
        if (NextInChain != null && NextInChain.PreviousInChain == this)
            NextInChain.PreviousInChain = PreviousInChain;
        PreviousInChain = null;
        NextInChain = null;
    }

    public void NotifyAdjacentMachinesOfConnection()
    {
        if (_grid == null || !_grid.HasGrid) return;

        _isConnectedToMachine = false;

        foreach (var nb in _grid.GetNeighbors(Anchor))
        {
            if (!_grid.TryGetCell(nb.coord, out var data) || data.occupant == null) continue;
            GameObject occGO = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            if (occGO == null) continue;
            var machine = occGO.GetComponent<Machine>();
            if (machine == null || machine.IsBroken) continue;

            if (machine.TryGetBeltConnection(this, out var portType, requireFacing: true) &&
                portType == MachinePortType.Output)
            {
                _isConnectedToMachine = true;
                machine.TryStartIfIdle();

                TutorialEventBus.PublishConveyorConnectedToMachine(machine);

            }
        }
    }

    private static GridOrientation Opposite(GridOrientation o) => (GridOrientation)(((int)o + 2) & 3);

    public bool IsCorner => isTurnPrefab && _turnKind != BeltTurnKind.None;

    public BeltTurnKind TurnKind => _turnKind;

    public bool IsItemAnimating() =>
        _item != null && _item.Visual != null && _item.Duration > 0f && _item.smoothTime < 1f;
}