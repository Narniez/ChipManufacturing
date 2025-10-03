using UnityEngine;

public interface IPlacementRule
{
    bool Validate(GridService grid, IGridOccupant occ, Vector2Int anchor, GridOrientation orientation);
    string Error { get; }

}
