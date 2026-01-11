using System.Collections.Generic;
using UnityEngine;

public class BeltChainPreviewController
{
    private readonly PlacementManager _pm;
    private readonly GridService _grid;
    private readonly GameObject _straightPrefab;
    private readonly GameObject _turnPrefab;
    private readonly Material _previewMaterial;

    // Active ghosts currently shown (forward, left, right)
    private readonly List<GameObject> _activeGhosts = new List<GameObject>(8);

    // Pool of inactive ghosts ready for reuse
    private readonly Stack<GameObject> _ghostPool = new Stack<GameObject>(35);

   // private readonly List<GameObject> _ghosts = new List<GameObject>();
    private ConveyorBelt _currentTail;

    // Pool config
    private readonly int _maxPoolSize;
    private readonly bool _prewarmOnConstruct;
    private readonly int _prewarmCount;

    public BeltChainPreviewController(PlacementManager pm, int prewarmCount = 0, int maxPoolSize = 64)
    {
        _pm = pm;
        _grid = pm.GridService;
        _straightPrefab = pm.GetConveyorPrefab(false);
        _turnPrefab = pm.GetConveyorPrefab(true);
        _previewMaterial = pm.GetPreviewMaterial();

        _maxPoolSize = Mathf.Max(0, maxPoolSize);
        _prewarmCount = Mathf.Max(0, prewarmCount);
        _prewarmOnConstruct = _prewarmCount > 0;

        if(_prewarmOnConstruct) Prewarm(_prewarmCount);
      
    }

    // Pre-create a number of ghosts so the first measured seconds don't include instantiation cost
    public void Prewarm(int count)
    {
        if(_straightPrefab == null || _grid == null) return;

        int target = Mathf.Min(count, _maxPoolSize);
        while(_ghostPool.Count + _activeGhosts.Count < target)
        {
            var go = CreateGhost();
            if(go == null) break;
            go.SetActive(false);
            _ghostPool.Push(go);
        }
    }

    // Release active ghosts back to pool (NOT destroying them)
    private void ReleaseActiveGhosts()
    {
        for(int i = 0; i < _activeGhosts.Count; i++)
        {
            var go = _activeGhosts[i];
            if(go == null) continue;

            // clearing preview data
            var prev = go.GetComponent<ConveyorPreview>();
            if(prev != null)
            {
                prev.Cell = default;
                prev.Orientation = default;
                prev.IsTurn = false;
            }

            go.SetActive(false);

            if(_ghostPool.Count < _maxPoolSize)           
                _ghostPool.Push(go);
            else            
                Object.Destroy(go); // pool full, destroy
        }
        _activeGhosts.Clear();
    }

    // Returns active ghosts and destroys pooled ones
    public void Cleanup()
    {
        ReleaseActiveGhosts();
        
        while(_ghostPool.Count > 0)
        {
            var go = _ghostPool.Pop();
            if(go != null)
                Object.Destroy(go);
        }
        _currentTail = null;
    }

    public void ShowOptionsFrom(ConveyorBelt tail)
    {
        ReleaseActiveGhosts();

        if (tail == null || _grid == null || !_grid.HasGrid) return;
        _currentTail = tail;

        var forward = tail.Orientation;
        // Forward always allowed
        TrySpawnGhost(tail.Anchor + GridOrientationExtentions.OrientationToDelta(forward), forward, isTurn: false);

        // Lateral options only if tail is straight (not a corner)
        if (!tail.IsTurnPrefab)
        {
            TrySpawnGhost(tail.Anchor + GridOrientationExtentions.OrientationToDelta(forward.RotatedCCW()), forward.RotatedCCW(), isTurn: true);
            TrySpawnGhost(tail.Anchor + GridOrientationExtentions.OrientationToDelta(forward.RotatedCW()), forward.RotatedCW(), isTurn: true);
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

        //Assign child as parent of belt in front if that belt has no parent
        LinkForwardIfParentMissing(child);

        ShowOptionsFrom(child);

        //compute contiguous chain length and publish if threshold met
        int chainLength = ComputeContiguousChainLength(child);
        const int defaultPublishThreshold = 4;
        if (chainLength >= defaultPublishThreshold)
        {
            TutorialEventBus.PublishConveyorChainLengthReached(chainLength);
        }

        return child;
    }

    // Count contiguous belts by following PreviousInChain and NextInChain from `start`
    private int ComputeContiguousChainLength(ConveyorBelt start)
    {
        if (start == null) return 0;
        var visited = new HashSet<ConveyorBelt>();
        // walk backward
        var cur = start;
        while (cur != null && !visited.Contains(cur))
        {
            visited.Add(cur);
            cur = cur.PreviousInChain;
        }
        // walk forward from start's NextInChain
        cur = start.NextInChain;
        while (cur != null && !visited.Contains(cur))
        {
            visited.Add(cur);
            cur = cur.NextInChain;
        }
        return visited.Count;
    }

    private void PromoteTailIfBend(ConveyorBelt parent, ConveyorBelt child)
    {
        if (parent == null || child == null) return;
        if (parent.IsTurnPrefab) return;

        Vector2Int delta = child.Anchor - parent.Anchor;
        var outgoing = DeltaToOrientation(delta);
        if (!outgoing.HasValue) return;

        // straight
        if (outgoing.Value == parent.Orientation) return; 

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
        var frontCell = newParent.Anchor + GridOrientationExtentions.OrientationToDelta(newParent.Orientation);
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

        var go = AcquireGhost();
        if (go == null) return;

        Vector3 pos = _pm.AnchorToWorldCenter(cell, Vector2Int.one, 0f);
        go.transform.SetPositionAndRotation(pos, ori.ToRotation());

        // updating preview data
        var prev = go.GetComponent<ConveyorPreview>();
        if (prev == null) prev = go.AddComponent<ConveyorPreview>();
        prev.Cell = cell;
        prev.Orientation = ori;
        prev.IsTurn = isTurn;
              
        ApplyPreviewMaterial(go);

        go.SetActive(true);
        _activeGhosts.Add(go);
    }

    private GameObject AcquireGhost()
    {
        if(_ghostPool.Count > 0) return _ghostPool.Pop();

        // allowing new ghost only if pool size limit not reached
        if (_activeGhosts.Count + _ghostPool.Count >= _maxPoolSize) return null;

        return CreateGhost();
    }

    private GameObject CreateGhost()
    {
        if(_straightPrefab == null) return null;

        // creating once, after that reusing from pool
        var go = Object.Instantiate(_straightPrefab);

        // cleaning up behaviours that are not needed for preview (e.g. ConveyorBelt, ConveyorSound, etc)
        // only happens on creation and prewarm, not per refresh
        var monos = go.GetComponents<MonoBehaviour>();
        for(int i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null) continue;
            Object.Destroy(monos[i]);
        }

        //ensuring collider exists for interaction
        if(go.GetComponent<Collider>() == null)
        {
            var collider = go.AddComponent<BoxCollider>();
            if(_grid != null)
            {
                collider.size = new Vector3(_grid.CellSize * 0.9f, 0.1f, _grid.CellSize * 0.9f);
                collider.center = Vector3.zero;
            }
        }

        // adding the preview component we actually need
        go.AddComponent<ConveyorPreview>();

        // applying preview material once
        ApplyPreviewMaterial(go);

        // starting inactive so AcquireGhost can position then activate
        go.SetActive(false);
        return go;
    }

    private void ApplyPreviewMaterial(GameObject go)
    {
        if (_previewMaterial == null) return;
        var cache = go.GetComponent<PreviewMaterialCache>();
        if (cache == null) cache = go.AddComponent<PreviewMaterialCache>();
        cache.ApplyPreview(_previewMaterial);
    }

}