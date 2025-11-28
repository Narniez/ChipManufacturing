using UnityEngine;

public class DraggingState : BasePlacementState
{
    private readonly IGridOccupant _dragging;
    private readonly Vector3 _startWorld;

    private GridOrientation _currentOrientation = GridOrientation.North;
    private Vector2Int _currentAnchor;
    private Vector3 _lastValidWorld;

    // Restore if invalid
    private bool _hadOriginalArea;
    private Vector2Int _originalAnchor;
    private GridOrientation _originalOrientation;
    private Vector2Int _originalSize;
    private Vector3 _originalWorld;

    // Port indicator helper (created for the duration of a drag)
    private MachinePortIndicatorController _portController;

    //private float _heightOffset;

    public DraggingState(PlacementManager ctx, IGridOccupant dragging, Vector3 startWorld) : base(ctx)
    {
        _dragging = dragging;
        _startWorld = startWorld;
    }

    public override void Enter()
    {
        if (PlaceMan.CameraCtrl != null) PlaceMan.CameraCtrl.SetInputLocked(true);

        _currentOrientation = _dragging.Orientation;

        // Cache original occupancy & clear
        var placedSize = _dragging.BaseSize.OrientedSize(_dragging.Orientation);
        var grid = PlaceMan.GridService;
        if (grid.IsAreaInside(_dragging.Anchor, placedSize))
        {
            _hadOriginalArea = true;
            _originalAnchor = _dragging.Anchor;
            _originalOrientation = _dragging.Orientation;
            _originalSize = placedSize;
            _originalWorld = _dragging.DragTransform.position;
            grid.SetAreaOccupant(_originalAnchor, _originalSize, null);

            // Remove the previous save entry for this occupant so moving it doesn't leave an orphan entry.
            if (_dragging is Machine machine)
                GameStateSync.TryRemoveMachine(machine);
            else if (_dragging is ConveyorBelt belt)
                GameStateSync.TryRemoveBelt(belt);
        }
        else
        {
            _hadOriginalArea = false;
        }

        // Create indicator controller (will no-op if grid missing)
        _portController = new MachinePortIndicatorController(PlaceMan);

        //_heightOffset = PlaceMan.ComputePivotBottomOffset(_dragging.DragTransform);

        var snapped = ApplySnap(_startWorld);
        _lastValidWorld = snapped;

        _dragging.OnDragStart();
        _dragging.OnDrag(snapped);

        // Show port indicators immediately for machines
        if (_portController != null && _dragging is Machine initialMachine)
            _portController.ShowFor(initialMachine);
    }

    public override void Update()
    {
        // Editor keyboard rotate
        if (Input.GetKeyDown(PlaceMan.RotateKey))
            OnRotateRequested();

        // Mobile: second-finger tap rotates
        if (PlaceMan.EnableSecondFingerRotate && Input.touchCount >= 2)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    OnRotateRequested();
                    break;
                }
            }
        }
    }

    public override void OnHoldMove(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (target != _dragging) return;
        PlaceMan.EdgeScrollCamera(screen);
        var snapped = ApplySnap(world);
        _dragging.OnDrag(snapped);

        // Update indicators while dragging (orientation/anchor may have changed)
        if (_portController != null && _dragging is Machine m)
            _portController.ShowFor(m);
    }

    public override void OnHoldEnd(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (!ReferenceEquals(target, _dragging)) return;

        var grid = PlaceMan.GridService;
        var snapped = ApplySnap(world);
        var size = _dragging.BaseSize.OrientedSize(_currentOrientation);

        if (PlaceMan.ValidatePlacement(_dragging, _currentAnchor, _currentOrientation, out var _))
        {
            grid.SetAreaOccupant(_currentAnchor, size, (_dragging as Component).gameObject);
            _dragging.SetPlacement(_currentAnchor, _currentOrientation);

            // Persist new placement/state
            if (_dragging is Machine placedMachine)
                GameStateSync.TryAddOrUpdateMachine(placedMachine);
            else if (_dragging is ConveyorBelt placedBelt)
                GameStateSync.TryAddOrUpdateBelt(placedBelt);
        }
        else
        {
            if (_hadOriginalArea)
            {
                grid.SetAreaOccupant(_originalAnchor, _originalSize, (_dragging as Component).gameObject);
                _dragging.SetPlacement(_originalAnchor, _originalOrientation);
                snapped = _originalWorld;

                // Restore previous save entry
                if (_dragging is Machine origMachine)
                    GameStateSync.TryAddOrUpdateMachine(origMachine);
                else if (_dragging is ConveyorBelt origBelt)
                    GameStateSync.TryAddOrUpdateBelt(origBelt);
            }
            else
            {
                Debug.LogWarning("Invalid placement and no original area to restore.");
            }
        }

        _dragging.OnDrag(snapped);
        _dragging.OnDragEnd();

        // Cleanup indicators when drag completes
        if (_portController != null)
        {
            _portController.Cleanup();
            _portController = null;
        }

        if (PlaceMan.CameraCtrl != null) PlaceMan.CameraCtrl.SetInputLocked(false);
        PlaceMan.SetState(new IdleState(PlaceMan));
    }

    public override void OnRotateRequested()
    {
        if (_dragging == null) return;

        // Preserve center cell for odd-sized footprints so it doesn't jump when rotating
        var oldSize = _dragging.BaseSize.OrientedSize(_currentOrientation);
        Vector2Int preservedCenter = ShouldCenterOnCell(oldSize)
            ? CenterCellFromAnchor(_currentAnchor, oldSize)
            : _currentAnchor;

        _currentOrientation = _currentOrientation.RotatedCW();
        var newSize = _dragging.BaseSize.OrientedSize(_currentOrientation);

        var desiredAnchor = ShouldCenterOnCell(newSize)
            ? AnchorFromCenterCell(preservedCenter, newSize)
            : _currentAnchor;

        _currentAnchor = PlaceMan.GridService.ClampAnchor(desiredAnchor, newSize);

        Vector3 world = PlaceMan.AnchorToWorldCenter(_currentAnchor, newSize, 0);
        _lastValidWorld = world;

        _dragging.SetPlacement(_currentAnchor, _currentOrientation);
        _dragging.OnDrag(world);

        // Update indicators after rotate
        if (_portController != null && _dragging is Machine m)
            _portController.ShowFor(m);
    }

    private Vector3 ApplySnap(Vector3 world)
    {
        var grid = PlaceMan.GridService;

        if (!PlaceMan.SnapToGrid || grid == null || _dragging == null || !grid.HasGrid)
            return world;

        var size = _dragging.BaseSize.OrientedSize(_currentOrientation);
        Vector2Int cell = grid.WorldToCell(world);

        if (cell.x < 0 || cell.y < 0 || cell.x >= grid.Cols || cell.y >= grid.Rows)
            return _lastValidWorld;

        // For odd x and y sizes, treat the finger/world cell as the center; otherwise use bottom-left as before
        Vector2Int desiredAnchor = ShouldCenterOnCell(size)
            ? AnchorFromCenterCell(cell, size)
            : cell;

        Vector2Int anchor = grid.ClampAnchor(desiredAnchor, size);
        _currentAnchor = anchor;

        Debug.Log($"ApplySnap: worldY={world.y:F3}, cell={cell}, anchor={anchor}, gridOriginY={grid.Origin.y:F3}");

        _dragging.SetPlacement(anchor, _currentOrientation);

        Vector3 snappedWorld = PlaceMan.AnchorToWorldCenter(anchor, size, 0);
        _lastValidWorld = snappedWorld;

        return snappedWorld;
    }

    // Helpers to support center-cell dragging for odd-sized footprints
    private static bool ShouldCenterOnCell(Vector2Int size) => (size.x & 1) == 1 && (size.y & 1) == 1;

    private static Vector2Int AnchorFromCenterCell(Vector2Int centerCell, Vector2Int size)
        => new Vector2Int(centerCell.x - size.x / 2, centerCell.y - size.y / 2);

    private static Vector2Int CenterCellFromAnchor(Vector2Int anchor, Vector2Int size)
        => new Vector2Int(anchor.x + size.x / 2, anchor.y + size.y / 2);
}
