using UnityEngine;

// Shows a prefab with preview material, lets you move (hold-drag) and rotate,
// and waits for external confirm/cancel via PlacementManager.ConfirmPreview/CancelPreview.
// Uses bottom-left anchor semantics with centered pivot. If MachineData is provided,
// its size/name are used during preview (Machine.Initialize happens on confirm).
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
    private bool _isConveyor;                // NEW: flag for belt previews

    public PreviewPlacementState(
        PlacementManager ctx,
        GameObject prefab,
        Material previewMaterial,
        GridOrientation? initialOrientation = null,
        MachineData machineData = null) : base(ctx)
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

        // Spawn at screen center -> desired bottom-left anchor
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

        // Anchor from desired, clamped
        _anchor = AdjustAnchorInsideGrid(desiredCell, size);

        // If conveyor: avoid occupied cells by scanning to the right
        if (_isConveyor)
            _anchor = FindFreeToRight(_anchor, size);

        _heightOffset = PlaceMan.ComputePivotBottomOffset(_instance.transform);
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
        _instance.transform.position = snapped;
        _occ.SetPlacement(_anchor, _orientation);

        // Show selection UI while in preview (name + rotate buttons)
        string title = _machineData != null ? _machineData.machineName : _instance.name;
        PlaceMan.SelectionUI?.Show(
            title,
            onRotateLeft: () => Rotate(clockwise: false),
            onRotateRight: () => Rotate(clockwise: true)
        );
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

    public override void OnHoldMove(IInteractable target, Vector2 screen, Vector3 world)
    {
        if (_instance == null || _occ == null) return;

        PlaceMan.EdgeScrollCamera(screen);

        var size = GetOrientedSize();
        Vector2Int cell = _grid.WorldToCell(world);
        if (!_grid.IsInside(cell)) return;

        Vector2Int newAnchor = AdjustAnchorInsideGrid(cell, size);

        // If conveyor: avoid occupied cells by scanning to the right
        if (_isConveyor)
            newAnchor = FindFreeToRight(newAnchor, size);

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

        _orientation = clockwise ? _orientation.RotatedCW() : _orientation.RotatedCCW();

        var size = GetOrientedSize();
        var newAnchor = AdjustAnchorInsideGrid(_anchor, size);

        // If conveyor: avoid occupied cells by scanning to the right
        if (_isConveyor)
            newAnchor = FindFreeToRight(newAnchor, size);

        _anchor = newAnchor;

        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
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
        PlaceMan.SetState(new IdleState(PlaceMan));
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

    //For conveyors, if current area is occupied, scan to the right until free or edge reached
    private Vector2Int FindFreeToRight(Vector2Int startAnchor, Vector2Int size)
    {
        if (_grid == null || !_grid.HasGrid) return startAnchor;

        Vector2Int a = startAnchor;
        int maxX = _grid.Cols - size.x;

        // If starting position is free, keep it
        if (_grid.IsAreaFree(a, size))
            return a;

        // Move right until a free area is found
        while (a.x < maxX)
        {
            a.x++;
            if (_grid.IsAreaFree(a, size))
                return a;
        }

        // No free cell found to the right on this row; keep original (occupied) position
        // Alternatively, could hide the preview or scan left/up/down as needed.
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