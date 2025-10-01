using UnityEngine;

public interface IGridOccupant : IDraggable
{
    Vector2Int BaseSize { get; }
    GridOrientation Orientation { get; }
    Vector2Int Anchor { get; } // bottom-left cell during placement
    void SetPlacement(Vector2Int anchor, GridOrientation orientation);
    // Optional: validation hook
    bool CanPlace(GridService grid, Vector2Int anchor, GridOrientation orientation);
}
