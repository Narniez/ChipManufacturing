using UnityEngine;

public class PreviewPlacementState : BasePlacementState
{
    private readonly GameObject _prefab;
    private readonly Material _previewMaterial;
    private readonly GridOrientation? _initialOrientation;
    private readonly MachineData _machineData; // optional

    private GridService _grid;
    private GameObject _instance;
    private IGridOccupant _occ;

    private Vector2Int _anchor;              
    private GridOrientation _orientation;
    private float _heightOffset;

    private bool _committed;
    private bool _isConveyor;

    public PreviewPlacementState(
        PlacementManager pm,
        GameObject prefab,
        Material previewMaterial,
        GridOrientation? initialOrientation = null,
        MachineData machineData = null) : base(pm)
    {
        _prefab = prefab;
        _previewMaterial = previewMaterial;
        _initialOrientation = initialOrientation;
        _machineData = machineData;
    }

    public override void Enter()
    {
        _grid = PlaceMan.GridService;
        if (_grid == null || !_grid.HasGrid || _prefab == null)
        {
            Debug.LogWarning("PreviewPlacementState: Missing grid or prefab.");
            PlaceMan.SetState(new IdleState(PlaceMan));
            return;
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector3 world = ScreenToGround(screenCenter);
        Vector2Int desiredCell = _grid.WorldToCell(world);

        _instance = Object.Instantiate(_prefab);
        _occ = _instance.GetComponent<IGridOccupant>();
        if (_occ == null)
        {
            Debug.LogError("PreviewPlacementState: Prefab lacks IGridOccupant.");
            Object.Destroy(_instance);
            PlaceMan.SetState(new IdleState(PlaceMan));
            return;
        }

        _isConveyor = _instance.GetComponent<ConveyorBelt>() != null;
        ApplyPreviewMaterial(_instance);

        _orientation = _initialOrientation ?? _occ.Orientation;
        var size = GetOrientedSize();

        _anchor = ShouldCenterOnCell(size)
            ? AdjustAnchorInsideGrid(AnchorFromCenterCell(desiredCell, size), size)
            : AdjustAnchorInsideGrid(desiredCell, size);

        // Machines use nearest free area search; conveyors have separate logic later when dragging.
        if (!_isConveyor)
        {
            if (!TryFindNearestFreeArea(_anchor, size, out var freeAnchor))
            {
                return;
            }
            _anchor = freeAnchor;
        }
        //else
        //{
        //    if (!_grid.IsAreaFree(_anchor, size))
        //    {
        //        _anchor = FindFreeToRight(_anchor, size);
        //    }
        //}

        _heightOffset = PlaceMan.ComputePivotBottomOffset(_instance.transform);
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
        _instance.transform.position = snapped;
        _occ.SetPlacement(_anchor, _orientation);
    }

    public override void Exit()
    {
        PlaceMan.SelectionUI?.Hide();
        if (!_committed && _instance != null)
            Object.Destroy(_instance);
    }

    public override void Update()
    {
        if (Input.GetKeyDown(PlaceMan.RotateKey))
            OnRotateRequested();
    }

    public override void OnTap(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (_instance == null || _occ == null || _grid == null || !_grid.HasGrid) return;

        var size = GetOrientedSize();
        Vector2Int cell = _grid.WorldToCell(world);
        if (!_grid.IsInside(cell)) return;

        Vector2Int newAnchor = ShouldCenterOnCell(size)
            ? AdjustAnchorInsideGrid(AnchorFromCenterCell(cell, size), size)
            : AdjustAnchorInsideGrid(cell, size);

        if (_isConveyor)
        {
            newAnchor = FindFreeToRight(newAnchor, size);
        }
        else
        {
            if (!TryFindNearestFreeArea(newAnchor, size, out var freeAnchor))
            {
                return;
            }
            newAnchor = freeAnchor;
        }

        if (newAnchor == _anchor) return;

        _anchor = newAnchor;
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
        _occ.SetPlacement(_anchor, _orientation);
        _instance.transform.position = snapped;
    }

    public override void OnHoldMove(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (_instance == null || _occ == null) return;

        PlaceMan.EdgeScrollCamera(screen);

        var size = GetOrientedSize();
        Vector2Int cell = _grid.WorldToCell(world);
        if (!_grid.IsInside(cell)) return;

        Vector2Int newAnchor = ShouldCenterOnCell(size)
            ? AdjustAnchorInsideGrid(AnchorFromCenterCell(cell, size), size)
            : AdjustAnchorInsideGrid(cell, size);

        newAnchor = _isConveyor
            ? FindFreeToRight(newAnchor, size)
            : (TryFindNearestFreeArea(newAnchor, size, out var freeAnchor) ? freeAnchor : newAnchor);

        // If machine search failed (preview canceled), abort further movement.
        if (_instance == null) return;

        if (newAnchor == _anchor) return;

        _anchor = newAnchor;
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
        _occ.SetPlacement(_anchor, _orientation);
        _instance.transform.position = snapped;
    }

    public override void OnRotateRequested()
    {
        Rotate(clockwise: true);
    }

    private void Rotate(bool clockwise)
    {
        if (_instance == null || _occ == null) return;

        var oldSize = GetOrientedSize();
        Vector2Int preservedCenter = ShouldCenterOnCell(oldSize)
            ? CenterCellFromAnchor(_anchor, oldSize)
            : _anchor;

        _orientation = clockwise ? _orientation.RotatedCW() : _orientation.RotatedCCW();

        var newSize = GetOrientedSize();

        var newAnchor = ShouldCenterOnCell(newSize)
            ? AnchorFromCenterCell(preservedCenter, newSize)
            : _anchor;

        newAnchor = AdjustAnchorInsideGrid(newAnchor, newSize);

        if (_isConveyor)
        {
            newAnchor = FindFreeToRight(newAnchor, newSize);
        }
        else
        {
            if (!TryFindNearestFreeArea(newAnchor, newSize, out var freeAnchor))
            {
                // Canceled (no space). Stop rotation (Exit will clean up).
                return;
            }
            newAnchor = freeAnchor;
        }

        if (_instance == null) return;

        _anchor = newAnchor;
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, newSize, _heightOffset);
        _occ.SetPlacement(_anchor, _orientation);
        _instance.transform.position = snapped;
    }

    public void ConfirmPlacement()
    {
        if (_committed || _instance == null || _occ == null) return;

        var size = GetOrientedSize();
        if (!PlaceMan.ValidatePlacement(_occ, _anchor, _orientation, out var err))
        {
            Debug.LogWarning($"Cannot confirm placement: {err}");
            return;
        }

        var machine = _instance.GetComponent<Machine>();
        if (_machineData != null && machine != null)
        {
            machine.Initialize(_machineData);
        }

        RestorePreviewMaterial(_instance);

        _grid.SetAreaOccupant(_anchor, size, _instance);
        _committed = true;

        if (_isConveyor)
        {
            var belt = _instance.GetComponent<ConveyorBelt>();
            belt?.NotifyAdjacentMachinesOfConnection();
            PlaceMan.SetState(new SelectingState(PlaceMan, _occ));
        }
        else
        {
            PlaceMan.SetState(new IdleState(PlaceMan));
        }
    }

    public void CancelPlacement()   
    {
        if (_committed) return;
        PlaceMan.SetState(new IdleState(PlaceMan));
    }

    private Vector2Int GetOrientedSize()
    {
        Vector2Int baseSize = _machineData != null ? _machineData.size : _occ.BaseSize;
        return baseSize.OrientedSize(_orientation);
    }

    private Vector2Int AdjustAnchorInsideGrid(Vector2Int desiredAnchor, Vector2Int size)
    {
        if (_grid == null || !_grid.HasGrid) return desiredAnchor;
        int maxX = _grid.Cols - size.x;
        int maxY = _grid.Rows - size.y;
        return new Vector2Int(
            Mathf.Clamp(desiredAnchor.x, 0, Mathf.Max(0, maxX)),
            Mathf.Clamp(desiredAnchor.y, 0, Mathf.Max(0, maxY))
        );
    }

    private static bool ShouldCenterOnCell(Vector2Int size) => (size.x & 1) == 1 && (size.y & 1) == 1;
    private static Vector2Int AnchorFromCenterCell(Vector2Int centerCell, Vector2Int size)
        => new Vector2Int(centerCell.x - size.x / 2, centerCell.y - size.y / 2);
    private static Vector2Int CenterCellFromAnchor(Vector2Int anchor, Vector2Int size)
        => new Vector2Int(anchor.x + size.x / 2, anchor.y + size.y / 2);

    private Vector2Int FindFreeToRight(Vector2Int startAnchor, Vector2Int size)
    {
        if (_grid == null || !_grid.HasGrid) return startAnchor;

        Vector2Int a = startAnchor;
        int maxX = _grid.Cols - size.x;

        if (_grid.IsAreaFree(a, size))
            return a;

        while (a.x < maxX)
        {
            a.x++;
            if (_grid.IsAreaFree(a, size))
                return a;
        }
        return startAnchor;
    }

    // NEW: Attempt to find a free area; if none exists, cancel preview (destroy instance) and return false.
    private bool TryFindNearestFreeArea(Vector2Int startAnchor, Vector2Int size, out Vector2Int freeAnchor)
    {
        freeAnchor = startAnchor;
        if (_grid == null || !_grid.HasGrid)
            return false;

        if (_grid.IsAreaFree(startAnchor, size))
        {
            freeAnchor = startAnchor;
            return true;
        }

        int maxR = Mathf.Max(_grid.Cols, _grid.Rows);
        for (int r = 1; r <= maxR; r++)
        {
            for (int x = startAnchor.x - r; x <= startAnchor.x + r; x++)
            {
                var top = new Vector2Int(x, startAnchor.y - r);
                var bot = new Vector2Int(x, startAnchor.y + r);

                if (_grid.IsAreaInside(top, size) && _grid.IsAreaFree(top, size))
                {
                    freeAnchor = top;
                    return true;
                }
                if (_grid.IsAreaInside(bot, size) && _grid.IsAreaFree(bot, size))
                {
                    freeAnchor = bot;
                    return true;
                }
            }
            for (int y = startAnchor.y - r + 1; y <= startAnchor.y + r - 1; y++)
            {
                var left = new Vector2Int(startAnchor.x - r, y);
                var right = new Vector2Int(startAnchor.x + r, y);

                if (_grid.IsAreaInside(left, size) && _grid.IsAreaFree(left, size))
                {
                    freeAnchor = left;
                    return true;
                }
                if (_grid.IsAreaInside(right, size) && _grid.IsAreaFree(right, size))
                {
                    freeAnchor = right;
                    return true;
                }
            }
        }

        // No free area anywhere -> cancel preview
        CancelPlacement();
        // Instance will be destroyed in Exit(); callers must check if _instance is null after call.
        return false;
    }

    private void ApplyPreviewMaterial(GameObject go)
    {
        if (_previewMaterial == null) return;
        var cache = go.GetComponent<PreviewMaterialCache>() ?? go.AddComponent<PreviewMaterialCache>();
        cache.ApplyPreview(_previewMaterial);
    }

    private void RestorePreviewMaterial(GameObject go)
    {
        var cache = go.GetComponent<PreviewMaterialCache>();
        if (cache != null)
        {
            cache.Restore();
            Object.Destroy(cache);
        }
    }

    private static Vector3 ScreenToGround(Vector2 screenPos)
    {
        var cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : Vector3.zero;
    }
}