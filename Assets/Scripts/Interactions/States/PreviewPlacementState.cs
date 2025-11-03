using UnityEngine;

// Shows a prefab with preview material, lets you move (hold-drag) and rotate,
// and waits for external confirm/cancel via PlacementManager.ConfirmPreview/CancelPreview.
// Uses bottom-left anchor with centered pivot. If MachineData is provided,
// its size/name are used during preview.
public class PreviewPlacementState : BasePlacementState
{
    private readonly GameObject _prefab;
    private readonly Material _previewMaterial;
    private readonly GridOrientation? _initialOrientation;
    private readonly MachineData _machineData; // optional

    private GridService _grid;
    private GameObject _instance;
    private IGridOccupant _occ;

    private Vector2Int _anchor;              // bottom-left cell of footprint
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

        // Spawn at screen center
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

        // Detect conveyor belt previews
        _isConveyor = _instance.GetComponent<ConveyorBelt>() != null;

        ApplyPreviewMaterial(_instance);

        _orientation = _initialOrientation ?? _occ.Orientation;
        var size = GetOrientedSize();

        // Compute anchor; if the footprint has a true center cell (odd x and y), treat the tapped cell as the center
        _anchor = ShouldCenterOnCell(size)
            ? AdjustAnchorInsideGrid(AnchorFromCenterCell(desiredCell, size), size)
            : AdjustAnchorInsideGrid(desiredCell, size);

        // Ensure preview does not overlap existing occupants
        _anchor = FindNearestFreeArea(_anchor, size);

        _heightOffset = PlaceMan.ComputePivotBottomOffset(_instance.transform);
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
        _instance.transform.position = snapped;
        _occ.SetPlacement(_anchor, _orientation);

        // Show selection UI while in preview (name + rotate buttons)
        //string title = _machineData != null ? _machineData.machineName : _instance.name;
        //PlaceMan.SelectionUI?.Show(
        //    title,
        //    onRotateLeft: () => Rotate(clockwise: false),
        //    onRotateRight: () => Rotate(clockwise: true)
        //);
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

    // Tap anywhere to move the preview
    public override void OnTap(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (_instance == null || _occ == null || _grid == null || !_grid.HasGrid) return;

        var size = GetOrientedSize();
        Vector2Int cell = _grid.WorldToCell(world);
        if (!_grid.IsInside(cell)) return;

        Vector2Int newAnchor = ShouldCenterOnCell(size)
            ? AdjustAnchorInsideGrid(AnchorFromCenterCell(cell, size), size)
            : AdjustAnchorInsideGrid(cell, size);

        // Ensure preview does not overlap existing occupants
        newAnchor = FindNearestFreeArea(newAnchor, size);

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

        // Ensure preview does not overlap existing occupants
        newAnchor = _isConveyor ? FindFreeToRight(newAnchor, size) : FindNearestFreeArea(newAnchor, size);

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

        // Preserve center cell if the footprint has a true center
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

        // Ensure preview does not overlap existing occupants
        newAnchor = _isConveyor ? FindFreeToRight(newAnchor, newSize) : FindNearestFreeArea(newAnchor, newSize);

        _anchor = newAnchor;

        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, newSize, _heightOffset);
        _occ.SetPlacement(_anchor, _orientation);
        _instance.transform.position = snapped;
    }

    // External confirm (called by PlacementManager.ConfirmPreview)
    public void ConfirmPlacement()
    {
        if (_committed || _instance == null || _occ == null) return;

        var size = GetOrientedSize();
        if (!PlaceMan.ValidatePlacement(_occ, _anchor, _orientation, out var err))
        {
            Debug.LogWarning($"Cannot confirm placement: {err}");
            return;
        }

        // Initialize machine data only when confirmed
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
            PlaceMan.SetState(new SelectingState(PlaceMan, _occ));
        }
        else
        {
            PlaceMan.SetState(new IdleState(PlaceMan));
        }
    }

    // External cancel (called by PlacementManager.CancelPreview)
    public void CancelPlacement()
    {
        if (_committed) return;
        PlaceMan.SetState(new IdleState(PlaceMan));
    }

    private Vector2Int GetOrientedSize()
    {
        // Use MachineData.size during preview if available (Machine.BaseSize would be 1x1 until initialized)
        Vector2Int baseSize = _machineData != null ? _machineData.size : _occ.BaseSize;
        return baseSize.OrientedSize(_orientation);
    }

    private Vector2Int AdjustAnchorInsideGrid(Vector2Int desiredAnchor, Vector2Int size
    )
    {
        if (_grid == null || !_grid.HasGrid) return desiredAnchor;
        int maxX = _grid.Cols - size.x;
        int maxY = _grid.Rows - size.y;
        return new Vector2Int(
            Mathf.Clamp(desiredAnchor.x, 0, Mathf.Max(0, maxX)),
            Mathf.Clamp(desiredAnchor.y, 0, Mathf.Max(0, maxY))
        );
    }

    // Footprints with a true center cell (odd x and odd y) can use the tapped cell as center.
    private static bool ShouldCenterOnCell(Vector2Int size) => (size.x & 1) == 1 && (size.y & 1) == 1;

    private static Vector2Int AnchorFromCenterCell(Vector2Int centerCell, Vector2Int size)
        => new Vector2Int(centerCell.x - size.x / 2, centerCell.y - size.y / 2);

    private static Vector2Int CenterCellFromAnchor(Vector2Int anchor, Vector2Int size)
        => new Vector2Int(anchor.x + size.x / 2, anchor.y + size.y / 2);

    // For conveyors, if current area is occupied, scan to the right until free or edge reached
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

    // For machines (any footprint), find the nearest anchor whose area fits and is free
    private Vector2Int FindNearestFreeArea(Vector2Int startAnchor, Vector2Int size)
    {
        if (_grid == null || !_grid.HasGrid) return startAnchor;

        // Already free?
        if (_grid.IsAreaFree(startAnchor, size))
            return startAnchor;

        int maxR = Mathf.Max(_grid.Cols, _grid.Rows);
        // Expand in a square "ring" around the start anchor
        for (int r = 1; r <= maxR; r++)
        {
            // Top and bottom rows of the ring
            for (int x = startAnchor.x - r; x <= startAnchor.x + r; x++)
            {
                var top = new Vector2Int(x, startAnchor.y - r);
                var bot = new Vector2Int(x, startAnchor.y + r);

                if (_grid.IsAreaInside(top, size) && _grid.IsAreaFree(top, size)) return top;
                if (_grid.IsAreaInside(bot, size) && _grid.IsAreaFree(bot, size)) return bot;
            }
            // Left and right columns of the ring (excluding corners already checked)
            for (int y = startAnchor.y - r + 1; y <= startAnchor.y + r - 1; y++)
            {
                var left = new Vector2Int(startAnchor.x - r, y);
                var right = new Vector2Int(startAnchor.x + r, y);

                if (_grid.IsAreaInside(left, size) && _grid.IsAreaFree(left, size)) return left;
                if (_grid.IsAreaInside(right, size) && _grid.IsAreaFree(right, size)) return right;
            }
        }

        // Fallback (should rarely happen)
        return startAnchor;
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