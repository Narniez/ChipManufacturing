using UnityEngine;

public class InsideGridRule : IPlacementRule
{
    public string Error { get; private set; }

    public bool Validate(GridService grid, IGridOccupant occ, Vector2Int anchor, GridOrientation orientation)
    {
        if (grid == null || occ == null || !grid.HasGrid) { Error = "Grid unavailable."; return false; }
        var size = occ.BaseSize.OrientedSize(orientation);
        bool ok = grid.IsAreaInside(anchor, size);
        if (!ok) Error = "Outside grid bounds.";
        return ok;
    }
}
