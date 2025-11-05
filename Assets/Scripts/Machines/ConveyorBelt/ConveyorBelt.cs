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

        // Resolve after occupancy is valid
        StartCoroutine(DeferredResolveShapeAndNeighbors());
        StartCoroutine(NotifyMachines());
    }

    private IEnumerator DeferredResolveShapeAndNeighbors()
    {
        yield return null;
        ResolveAutoShape(notifyNeighbors: true);
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

        // Apply logical orientation then adjust visual based on turn kind
        ApplyTurnVisualRotation();

        // Re-resolve shape on placement/orientation changes (deferred so grid has our occupancy)
        StartCoroutine(DeferredResolveShapeAndNeighbors());

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

    public void ResolveAutoShape(bool notifyNeighbors)
    {
        if (_grid == null || !_grid.HasGrid) return;

        // 1) Find the incoming direction (neighbor pointing to us)
        if (!TryGetIncomingDir(out var incoming))
        {
            // HEAD RULE: if this is a head (no incoming) and we have exactly one neighbor that faces away,
            // align this belt straight towards that neighbor (do not create a turn here).
            if (TryGetAnyOutgoingForHead(out var headOut))
            {
                // Force straight and face the neighbor direction
                if (isTurnPrefab)
                {
                    var pm = PlacementManager.Instance;
                    if (pm != null)
                    {
                        pm.ReplaceConveyorPrefab(this, useTurnPrefab: false, overrideOrientation: headOut, turnKind: BeltTurnKind.None);
                        return;
                    }
                }
                else
                {
                    SetTurnKind(BeltTurnKind.None);
                    if (orientation != headOut)
                        SetPlacement(Anchor, headOut);
                }
            }

            if (notifyNeighbors) ResolveNeighbors();
            return;
        }

        // 2) Find an outgoing belt in order Right -> Forward -> Left relative to the incoming direction.
        // Only consider neighbors whose Orientation matches that direction (faces away from us).
        GridOrientation outgoing;
        bool hasOutgoing = TryGetOutgoingStrict(incoming, out outgoing);

        bool wantTurn = false;
        GridOrientation desiredOrientation = orientation;
        BeltTurnKind turnKind = BeltTurnKind.None;

        if (hasOutgoing)
        {
            desiredOrientation = outgoing;
            wantTurn = outgoing != incoming;

            if (wantTurn)
            {
                turnKind = (outgoing == incoming.RotatedCW()) ? BeltTurnKind.Right
                         : (outgoing == incoming.RotatedCCW()) ? BeltTurnKind.Left
                         : BeltTurnKind.None;
            }
        }
        else
        {
            // End piece: keep straight, don't try to connect sideways to older pieces (prevents loops)
            desiredOrientation = orientation;
            wantTurn = false;
            turnKind = BeltTurnKind.None;
        }

        // Apply
        if (wantTurn != isTurnPrefab)
        {
            var pm = PlacementManager.Instance;
            if (pm != null)
            {
                pm.ReplaceConveyorPrefab(this, useTurnPrefab: wantTurn, overrideOrientation: desiredOrientation, turnKind: turnKind);
                return;
            }
        }
        else
        {
            SetTurnKind(turnKind);
            if (orientation != desiredOrientation)
                SetPlacement(Anchor, desiredOrientation);
        }

        if (notifyNeighbors)
        {
            ResolveNeighbors();
        }
    }

    // --- Helpers for deterministic incoming/outgoing detection ---

    // Neighbor is "incoming" if its forward points to us
    private bool TryGetIncomingDir(out GridOrientation incoming)
    {
        var dirs = new[] { GridOrientation.North, GridOrientation.East, GridOrientation.South, GridOrientation.West };
        for (int i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];
            var nCell = Anchor + ToDelta(dir);
            if (!_grid.TryGetCell(nCell, out var data) || data.occupant == null) continue;

            GameObject occGO = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            if (occGO == null) continue;

            var belt = occGO.GetComponent<ConveyorBelt>();
            if (belt == null) continue;

            // neighbor points to us?
            if (nCell + ToDelta(belt.Orientation) == Anchor)
            {
                incoming = Opposite(dir);
                return true;
            }
        }
        incoming = GridOrientation.North;
        return false;
    }

    // Outgoing: Right -> Forward -> Left relative to 'incoming', but only if the neighbor faces away in that direction.
    private bool TryGetOutgoingStrict(GridOrientation incoming, out GridOrientation outgoing)
    {
        var right = incoming.RotatedCW();
        var fwd = incoming;
        var left = incoming.RotatedCCW();

        if (LooksLikeOutgoing(right)) { outgoing = right; return true; }
        if (LooksLikeOutgoing(fwd)) { outgoing = fwd; return true; }
        if (LooksLikeOutgoing(left)) { outgoing = left; return true; }

        outgoing = incoming;
        return false;
    }

    // HEAD RULE helper: when there is no incoming, align to any single neighbor that faces away.
    private bool TryGetAnyOutgoingForHead(out GridOrientation outDir)
    {
        // Check all 4 directions; pick the first neighbor that faces away (Orientation == dir)
        var dirs = new[] { GridOrientation.North, GridOrientation.East, GridOrientation.South, GridOrientation.West };
        for (int i = 0; i < dirs.Length; i++)
        {
            var d = dirs[i];
            if (LooksLikeOutgoing(d))
            {
                outDir = d;
                return true;
            }
        }
        outDir = GridOrientation.North;
        return false;
    }

    private bool LooksLikeOutgoing(GridOrientation dir)
    {
        var nCell = Anchor + ToDelta(dir);
        if (!_grid.TryGetCell(nCell, out var data) || data.occupant == null) return false;

        GameObject occGO = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
        if (occGO == null) return false;

        var belt = occGO.GetComponent<ConveyorBelt>();
        if (belt == null) return false;

        // Consider "next" only when the neighbor faces away from us (its forward == dir).
        return belt.Orientation == dir;
    }

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);
    private void ResolveNeighbors()
    {
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

            var belt = occGO.GetComponent<ConveyorBelt>();
            belt?.ResolveAutoShape(notifyNeighbors: false);
        }
    }
}