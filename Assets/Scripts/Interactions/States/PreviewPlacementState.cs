using System.Collections;
using System.Collections.Generic;
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

    // bottom-left cell of footprint
    private Vector2Int _anchor;              
    private GridOrientation _orientation;
    private float _heightOffset;

    private bool _committed;
    private bool _isConveyor;

    // Port indicator preview
    private Transform _portIndicatorsRoot;

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
        // Ensure instance is in the current scene (PlacementManager helper)
        PlaceMan?.NotifySpawned(_instance);

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

        // Before showing any preview, ensure there is at least one free area in the grid
        // for this footprint in any rotation. If none, cancel preview and UI.
        Vector2Int baseSize = _machineData != null ? _machineData.size : _occ.BaseSize;
        if (!HasAnyFreeAreaForAnyRotation(baseSize))
        {
            Debug.LogWarning("PreviewPlacementState: No free area in grid to place this object. Cancelling preview.");
            PlaceMan.SelectionUI?.Hide();
            Object.Destroy(_instance);
            PlaceMan.SetState(new IdleState(PlaceMan));
            return;
        }

        ApplyPreviewMaterial(_instance);

        _orientation = _initialOrientation ?? _occ.Orientation;
        var size = GetOrientedSize();

        // Compute anchor; if the footprint has a true center cell (odd x and y), treat the tapped cell as the center
        _anchor = ShouldCenterOnCell(size)
            ? AdjustAnchorInsideGrid(AnchorFromCenterCell(desiredCell, size), size)
            : AdjustAnchorInsideGrid(desiredCell, size);

        // Ensure preview does not overlap existing occupants
        _anchor = FindNearestFreeArea(_anchor, size);

        // Defer position/height/indicator work by one frame so renderer bounds, prefab child transforms
        // and any scene move operations have settled. This fixes indicators being offset on first spawn.
        PlaceMan?.StartCoroutine(DeferredPlacementInit(desiredCell));
    }

    private IEnumerator DeferredPlacementInit(Vector2Int desiredCell)
    {
        // Wait one frame for Unity to finish instantiation/layout and renderer bounds to be valid
        yield return null;

        var size = GetOrientedSize();

        // Compute pivot offset using settled renderers
        _heightOffset = PlaceMan.ComputePivotBottomOffset(_instance.transform);

        // Snap to grid center based on anchor/size and computed offset
        Vector3 snapped = PlaceMan.AnchorToWorldCenter(_anchor, size, _heightOffset);
        _instance.transform.position = snapped;

        // Let occupant know its logical placement (may set rotation)
        _occ.SetPlacement(_anchor, _orientation);

        // Build port indicators now that transforms and bounds are stable
        RebuildPortIndicators();

        // Show rotation UI (left/right) for preview
        ShowRotationUI();
    }

    public override void Exit()
    {
        PlaceMan.SelectionUI?.Hide();

        DestroyPortIndicators();

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

        RebuildPortIndicators();
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

        RebuildPortIndicators();
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

        RebuildPortIndicators();
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

        var machine = _instance.GetComponent<Machine>();
        if (_machineData != null && machine != null)
        {
            machine.Initialize(_machineData);
        }

        RestorePreviewMaterial(_instance);

        _grid.SetAreaOccupant(_anchor, size, _instance);
        _committed = true;

        // Persist machine placement immediately so save.json contains the new machine.
        if (machine != null)
        {
            GameStateSync.TryAddOrUpdateMachine(machine);
        }

        // Belt: notify adjacent machines (so generators can start immediately)
        if (_isConveyor)
        {
            var belt = _instance.GetComponent<ConveyorBelt>();
            if (belt != null)
            {
                EconomyManager.Instance?.PurchaseConveyor(belt, ref EconomyManager.Instance.playerBalance);
                belt.NotifyAdjacentMachinesOfConnection();

            }
            PlaceMan.SetState(new SelectingState(PlaceMan, _occ));
        }
        else
        {
            PlaceMan.SetState(new IdleState(PlaceMan));
        }

        // Cleanup preview port indicators after placement
        DestroyPortIndicators();

        if (_machineData != null && machine != null)
        {
            machine.Initialize(_machineData);
            var economyManager = EconomyManager.Instance;
            if (EconomyManager.Instance == null)
            {
                return;
            }
            if (economyManager.playerBalance < economyManager.GetMachineCost(_machineData))
            {
                Debug.LogWarning("Cannot confirm machine placement: not enough moni:(");
                PlaceMan.CancelPreview();
                return;
            }
            else
            {
                economyManager.PurchaseMachine(_machineData, ref EconomyManager.Instance.playerBalance);
            }
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

    // ---------------------------
    // Port indicator preview API
    // ---------------------------

    private void RebuildPortIndicators()
    {
        // Show only for machine previews (not belts) and when we have MachineData
        if (_isConveyor || _instance == null || _machineData == null || _grid == null || !_grid.HasGrid)
        {
            DestroyPortIndicators();
            return;
        }

        if (_portIndicatorsRoot == null)
        {
            var root = new GameObject("PortIndicators");
            root.transform.SetParent(_instance.transform, worldPositionStays: false);
            _portIndicatorsRoot = root.transform;
        }
        else
        {
            // Clear previous children
            for (int i = _portIndicatorsRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(_portIndicatorsRoot.GetChild(i).gameObject);
        }

        var size = GetOrientedSize();
        float y = _grid.Origin.y + 0.02f;

        // DEBUG: report placement state
        Debug.Log($"[Preview] anchor={_anchor} orientation={_orientation} orientedSize={size} instancePos={_instance.transform.position}");

        var ports = _machineData.ports != null && _machineData.ports.Count > 0
            ? _machineData.ports
            : null;

        if (ports == null)
        {
            // Default: single output on front
            var prefab = _machineData.outputPortIndicatorPrefab;
            if (prefab != null)
            {
                var worldSide = _orientation;
                var cell = ComputePortCell(_orientation, size, -1);
                if (_grid.IsInside(cell))
                {
                    var pos = _grid.CellToWorldCenter(cell, y);
                    var rot = worldSide.ToRotation();
                    Object.Instantiate(prefab, pos, rot, _portIndicatorsRoot);
                }
            }
            return;
        }

        // Use per-port indicators
        for (int i = 0; i < ports.Count; i++)
        {
            var p = ports[i];
            GameObject prefab = p.kind == MachinePortType.Input
                ? _machineData.inputPortIndicatorPrefab
                : _machineData.outputPortIndicatorPrefab;

            if (prefab == null) continue;

            var worldSide = RotateSide(p.side, _orientation);
            var cell = ComputePortCell(p.side, size, p.offset);
            if (!_grid.IsInside(cell)) continue;

            var pos = _grid.CellToWorldCenter(cell, y);

            // Outputs point away from the machine, inputs point toward the machine
            Quaternion rot = p.kind == MachinePortType.Input
                ? Opposite(worldSide).ToRotation()
                : worldSide.ToRotation();

            Object.Instantiate(prefab, pos, rot, _portIndicatorsRoot);
        }
    }

    private void DestroyPortIndicators()
    {
        if (_portIndicatorsRoot == null) return;
        for (int i = _portIndicatorsRoot.childCount - 1; i >= 0; i--)
            Object.Destroy(_portIndicatorsRoot.GetChild(i).gameObject);
    }

    // Local copies of helpers to compute port cells during preview (no Machine instance/data yet)
    private static GridOrientation RotateSide(GridOrientation local, GridOrientation by)
        => (GridOrientation)(((int)local + (int)by) & 3);

    private static GridOrientation Opposite(GridOrientation o)
        => (GridOrientation)(((int)o + 2) & 3);

    private Vector2Int ComputePortCell(GridOrientation localSide, Vector2Int orientedSize, int offset)
    {
        GridOrientation side = RotateSide(localSide, _orientation);
        int sideLen = (side == GridOrientation.North || side == GridOrientation.South) ? orientedSize.x : orientedSize.y;
        int idx = offset < 0 ? Mathf.Max(0, (sideLen - 1) / 2) : Mathf.Clamp(offset, 0, Mathf.Max(0, sideLen - 1));

        switch (side)
        {
            case GridOrientation.North: return new Vector2Int(_anchor.x + idx, _anchor.y + orientedSize.y);
            case GridOrientation.South: return new Vector2Int(_anchor.x + idx, _anchor.y - 1);
            case GridOrientation.East:  return new Vector2Int(_anchor.x + orientedSize.x, _anchor.y + idx);
            case GridOrientation.West:  return new Vector2Int(_anchor.x - 1, _anchor.y + idx);
            default: return _anchor;
        }
    }

    // ---------------------------
    // Free-area scanning helpers
    // ---------------------------

    // Checks if there is any free area for the given base footprint in ANY rotation (size or size swapped)
    private bool HasAnyFreeAreaForAnyRotation(Vector2Int baseSize)
    {
        // Same footprint or 90-degree rotated footprint
        var candidates = new List<Vector2Int> { baseSize };
        if (baseSize.x != baseSize.y) candidates.Add(new Vector2Int(baseSize.y, baseSize.x));

        foreach (var s in candidates)
        {
            if (HasAnyFreeArea(s)) return true;
        }
        return false;
    }

    // Scans the grid for at least one anchor where area of 'size' is fully free
    private bool HasAnyFreeArea(Vector2Int size)
    {
        if (_grid == null || !_grid.HasGrid) return false;
        int maxX = _grid.Cols - size.x;
        int maxY = _grid.Rows - size.y;
        if (maxX < 0 || maxY < 0) return false;

        for (int y = 0; y <= maxY; y++)
        {
            for (int x = 0; x <= maxX; x++)
            {
                var a = new Vector2Int(x, y);
                if (_grid.IsAreaFree(a, size)) return true;
            }
        }
        return false;
    }

    // ---------------------------
    // Rotation UI
    // ---------------------------

    private void ShowRotationUI()
    {
        if (PlaceMan?.SelectionUI == null) return;

        string title = _machineData != null && !string.IsNullOrEmpty(_machineData.machineName)
            ? _machineData.machineName
            : (_prefab != null ? _prefab.name : "Preview");

        // Wire rotate-left and rotate-right buttons
        PlaceMan.SelectionUI.Show(
            title,
            onRotateLeft: () => Rotate(clockwise: false),
            onRotateRight: () => Rotate(clockwise: true),
            isBelt: _isConveyor
        );
    }
}