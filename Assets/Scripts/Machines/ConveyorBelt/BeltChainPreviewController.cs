using System.Collections.Generic;
using UnityEngine;

public class BeltChainPreviewController
{
    private readonly PlacementManager _pm;
    private readonly GridService _grid;
    private readonly GameObject _straightPrefab;
    private readonly GameObject _turnPrefab;
    private readonly Material _previewMaterial;

    private readonly List<GameObject> _ghosts = new List<GameObject>();

    public BeltChainPreviewController(PlacementManager pm)
    {
        _pm = pm;
        _grid = pm.GridService;
        _straightPrefab = GetStraightPrefab(pm);
        _turnPrefab = GetTurnPrefab(pm);
        _previewMaterial = GetPreviewMaterial(pm);
    }

    public void Cleanup()
    {
        for (int i = 0; i < _ghosts.Count; i++)
            if (_ghosts[i] != null) Object.Destroy(_ghosts[i]);
        _ghosts.Clear();
    }

    public void ShowOptionsFrom(ConveyorBelt tail)
    {
        Cleanup();
        if (tail == null || _grid == null || !_grid.HasGrid) return;

        var tailAnchor = tail.Anchor;
        var forward = tail.Orientation;

        // Compute orientations
        var forwardOri = forward;
        var leftOri = forward.RotatedCCW();
        var rightOri = forward.RotatedCW();

        // Cells
        var fCell = tailAnchor + ToDelta(forwardOri);
        var lCell = tailAnchor + ToDelta(leftOri);
        var rCell = tailAnchor + ToDelta(rightOri);

        // Spawn forward (straight)
        TrySpawnGhost(fCell, forwardOri, isTurn: false);
        // Spawn left (turn)
        TrySpawnGhost(lCell, leftOri, isTurn: true);
        // Spawn right (turn)
        TrySpawnGhost(rCell, rightOri, isTurn: true);
    }

    // Places the belt for the tapped preview, returns the placed belt (or null)
    public ConveyorBelt PlaceFromPreview(ConveyorPreview p)
    {
        if (p == null || _grid == null || !_grid.HasGrid) return null;

        // Validate cell still free
        if (!_grid.IsInside(p.Cell) || !_grid.IsAreaFree(p.Cell, Vector2Int.one))
            return null;

        GameObject prefab = p.IsTurn ? _turnPrefab : _straightPrefab;
        if (prefab == null) return null;

        Vector3 pos = _pm.AnchorToWorldCenter(p.Cell, Vector2Int.one, 0f);
        var go = Object.Instantiate(prefab, pos, p.Orientation.ToRotation());

        if (!go.TryGetComponent<IGridOccupant>(out var occ))
        {
            Object.Destroy(go);
            Debug.LogError("Placed conveyor prefab is missing IGridOccupant.");
            return null;
        }

        occ.SetPlacement(p.Cell, p.Orientation);
        _grid.SetAreaOccupant(p.Cell, Vector2Int.one, go);

        var belt = go.GetComponent<ConveyorBelt>();

        if (belt != null)
            ShowOptionsFrom(belt);

        return belt;
    }

    private void TrySpawnGhost(Vector2Int cell, GridOrientation ori, bool isTurn)
    {
        if (!_grid.IsInside(cell) || !_grid.IsAreaFree(cell, Vector2Int.one)) return;

        var prefab =  _straightPrefab;
        if (prefab == null) return;

        Vector3 pos = _pm.AnchorToWorldCenter(cell, Vector2Int.one, 0f);
        var go = Object.Instantiate(prefab, pos, ori.ToRotation());

        // If the prefab has a ConveyorBelt, unregister and remove it so this is a pure ghost
        var beltComp = go.GetComponent<ConveyorBelt>();
        if (beltComp != null)
        {
            BeltSystemRuntime.Instance?.Unregister(beltComp);
            Object.Destroy(beltComp);
        }

        // Apply preview material (no color changes)
        ApplyPreviewMaterial(go);

        // Ensure collider for tap detection
        if (go.GetComponent<Collider>() == null)
        {
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(_grid.CellSize * 0.9f, 0.1f, _grid.CellSize * 0.9f);
            col.center = Vector3.zero;
        }

        var prev = go.AddComponent<ConveyorPreview>();
        prev.Cell = cell;
        prev.Orientation = ori;
        prev.IsTurn = isTurn;

        _ghosts.Add(go);
    }

    private void ApplyPreviewMaterial(GameObject go)
    {
        if (_previewMaterial == null) return;

        var cache = go.GetComponent<PreviewMaterialCache>();
        if (cache == null) cache = go.AddComponent<PreviewMaterialCache>();
        cache.ApplyPreview(_previewMaterial);
    }

    private static Vector2Int ToDelta(GridOrientation o)
    {
        switch (o)
        {
            case GridOrientation.North: return Vector2Int.up;
            case GridOrientation.East:  return Vector2Int.right;
            case GridOrientation.South: return Vector2Int.down;
            case GridOrientation.West:  return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }

    private GameObject GetStraightPrefab(PlacementManager pm) => pm.GetConveyorPrefab(false);
    private GameObject GetTurnPrefab(PlacementManager pm) => pm.GetConveyorPrefab(true);
    private Material GetPreviewMaterial(PlacementManager pm) => pm.GetPreviewMaterial();
}