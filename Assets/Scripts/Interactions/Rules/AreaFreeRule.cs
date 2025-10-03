using UnityEngine;

public class AreaFreeRule : IPlacementRule
{
    public string Error { get; private set; }

    public bool Validate(GridService grid, IGridOccupant occ, Vector2Int anchor, GridOrientation orientation)
    {
        if (grid == null || occ == null) { Error = "Grid unavailable."; return false; }
        var size = occ.BaseSize.OrientedSize(orientation);
        var go = (occ as Component)?.gameObject;
        bool ok = grid.IsAreaFree(anchor, size, go);
        if (!ok) Error = "Space occupied.";
        return ok;
    }
}
