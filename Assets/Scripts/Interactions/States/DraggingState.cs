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

    private float _heightOffset;

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
        }
        else
        {
            _hadOriginalArea = false;
        }

        _heightOffset = PlaceMan.ComputePivotBottomOffset(_dragging.DragTransform);

        var snapped = ApplySnap(_startWorld);
        _lastValidWorld = snapped;

        _dragging.OnDragStart();
        _dragging.OnDrag(snapped);
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
        }
        else
        {
            if (_hadOriginalArea)
            {
                grid.SetAreaOccupant(_originalAnchor, _originalSize, (_dragging as Component).gameObject);
                _dragging.SetPlacement(_originalAnchor, _originalOrientation);
                snapped = _originalWorld;
            }
            else
            {
                Debug.LogWarning("Invalid placement and no original area to restore.");
            }
        }

        _dragging.OnDrag(snapped);
        _dragging.OnDragEnd();

        if (PlaceMan.CameraCtrl != null) PlaceMan.CameraCtrl.SetInputLocked(false);
        PlaceMan.SetState(new IdleState(PlaceMan));
    }

    public override void OnRotateRequested()
    {
        if (_dragging == null) return;

        _currentOrientation = _currentOrientation.RotatedCW();
        var size = _dragging.BaseSize.OrientedSize(_currentOrientation);
        _currentAnchor = PlaceMan.GridService.ClampAnchor(_currentAnchor, size);

        Vector3 world = PlaceMan.AnchorToWorldCenter(_currentAnchor, size, _heightOffset);
        _lastValidWorld = world;

        _dragging.SetPlacement(_currentAnchor, _currentOrientation);
        _dragging.OnDrag(world);
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

        Vector2Int anchor = grid.ClampAnchor(cell, size);
        _currentAnchor = anchor;
        _dragging.SetPlacement(anchor, _currentOrientation);

        Vector3 snappedWorld = PlaceMan.AnchorToWorldCenter(anchor, size, _heightOffset);
        _lastValidWorld = snappedWorld;
        return snappedWorld;
    }
}
