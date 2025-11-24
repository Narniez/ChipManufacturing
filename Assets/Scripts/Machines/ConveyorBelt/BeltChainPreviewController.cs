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
    private ConveyorBelt _currentTail;

    public BeltChainPreviewController(PlacementManager pm)
    {
        _pm = pm;
        _grid = pm.GridService;
        _straightPrefab = pm.GetConveyorPrefab(false);
        _turnPrefab = pm.GetConveyorPrefab(true);
        _previewMaterial = pm.GetPreviewMaterial();
    }

    public void Cleanup()
    {
        for (int i = 0; i < _ghosts.Count; i++)
            if (_ghosts[i] != null) Object.Destroy(_ghosts[i]);
        _ghosts.Clear();
        _currentTail = null;
    }

    public void ShowOptionsFrom(ConveyorBelt tail)
    {
        Cleanup();
        if (tail == null || _grid == null || !_grid.HasGrid) return;
        _currentTail = tail;

        var forward = tail.Orientation;
        // Forward always allowed
        TrySpawnGhost(tail.Anchor + ToDelta(forward), forward, isTurn: false);

        // Lateral options only if tail is straight (not a corner)
        if (!tail.IsTurnPrefab)
        {
            TrySpawnGhost(tail.Anchor + ToDelta(forward.RotatedCCW()), forward.RotatedCCW(), isTurn: true);
            TrySpawnGhost(tail.Anchor + ToDelta(forward.RotatedCW()), forward.RotatedCW(), isTurn: true);
        }
    }

    public ConveyorBelt PlaceFromPreview(ConveyorPreview p)
    {
        if (p == null || _grid == null || !_grid.HasGrid) return null;
        if (_currentTail == null) return null;
        if (!_grid.IsInside(p.Cell) || !_grid.IsAreaFree(p.Cell, Vector2Int.one)) return null;

        //cannot place belt from preview if not enough money
        var econ = EconomyManager.Instance;
        if (econ != null)
        {
            // Get cost from the prefab's ConveyorBelt component
            var beltTemplate = _straightPrefab != null ? _straightPrefab.GetComponent<ConveyorBelt>() : null;
            int cost = beltTemplate != null ? beltTemplate.Cost : 0;

            if (econ.playerBalance < cost)
            {
                Debug.LogWarning("Cannot confirm belt placement: not enough moni:(");
                return null;
            }

            // charge now
           econ.PurchaseConveyor(beltTemplate, ref econ.playerBalance);
        }

        // Always straight child
        GameObject prefab = _straightPrefab;
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

        var child = go.GetComponent<ConveyorBelt>();
        if (child == null) return null;

        // Link to tail
        child.PreviousInChain = _currentTail;
        _currentTail.NextInChain = child;

        PromoteTailIfBend(_currentTail, child);

        // ALSO: assign child as parent of belt in front if that belt has no parent
        LinkForwardIfParentMissing(child);

        ShowOptionsFrom(child);
        return child;
    }



    private void PromoteTailIfBend(ConveyorBelt parent, ConveyorBelt child)
    {
        if (parent == null || child == null) return;
        if (parent.IsTurnPrefab) return;

        Vector2Int delta = child.Anchor - parent.Anchor;
        var outgoing = DeltaToOrientation(delta);
        if (!outgoing.HasValue) return;

        if (outgoing.Value == parent.Orientation) return; // straight

        PromoteCorner(parent, parent.Orientation, outgoing.Value);
    }

    private void PromoteCorner(ConveyorBelt belt, GridOrientation incoming, GridOrientation outgoing)
    {
        if (belt == null) return;
        var turnKind = outgoing == incoming.RotatedCW()
            ? ConveyorBelt.BeltTurnKind.Right
            : (outgoing == incoming.RotatedCCW() ? ConveyorBelt.BeltTurnKind.Left : ConveyorBelt.BeltTurnKind.None);

        if (turnKind == ConveyorBelt.BeltTurnKind.None) return;

        if (belt.IsTurnPrefab && belt.Orientation == outgoing)
        {
            belt.SetTurnKind(turnKind);
            belt.LockCorner();
            return;
        }

        var replaced = _pm.ReplaceConveyorPrefab(belt, useTurnPrefab: true, overrideOrientation: outgoing, turnKind: turnKind);
        replaced?.LockCorner();
        if (replaced != null)
            _currentTail = replaced;
    }

    private void LinkForwardIfParentMissing(ConveyorBelt newParent)
    {
        if (newParent == null || _grid == null || !_grid.HasGrid) return;
        var frontCell = newParent.Anchor + ToDelta(newParent.Orientation);
        if (_grid.TryGetCell(frontCell, out var data) && data.occupant != null)
        {
            var go = data.occupant as GameObject ?? (data.occupant as Component)?.gameObject;
            var beltForward = go != null ? go.GetComponent<ConveyorBelt>() : null;
            if (beltForward != null && beltForward.PreviousInChain == null && beltForward != newParent)
            {
                // Link only if not already part of another chain
                beltForward.PreviousInChain = newParent;
                newParent.NextInChain = beltForward;
            }
        }
    }

    private GridOrientation? DeltaToOrientation(Vector2Int d)
    {
        if (d == Vector2Int.up) return GridOrientation.North;
        if (d == Vector2Int.right) return GridOrientation.East;
        if (d == Vector2Int.down) return GridOrientation.South;
        if (d == Vector2Int.left) return GridOrientation.West;
        return null;
    }

    private void TrySpawnGhost(Vector2Int cell, GridOrientation ori, bool isTurn)
    {
        if (!_grid.IsInside(cell) || !_grid.IsAreaFree(cell, Vector2Int.one)) return;
        var prefab = _straightPrefab;
        if (prefab == null) return;

        Vector3 pos = _pm.AnchorToWorldCenter(cell, Vector2Int.one, 0f);
        var go = Object.Instantiate(prefab, pos, ori.ToRotation());

        var beltComp = go.GetComponent<ConveyorBelt>();
        if (beltComp != null)
        {
            BeltSystemRuntime.Instance?.Unregister(beltComp);
            Object.Destroy(beltComp);
        }

        ApplyPreviewMaterial(go);

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
            case GridOrientation.East: return Vector2Int.right;
            case GridOrientation.South: return Vector2Int.down;
            case GridOrientation.West: return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }
}