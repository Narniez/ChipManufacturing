using UnityEngine;
using GridCellTypes;

public enum GridOrientation { North = 0, East = 1, South = 2, West = 3 }

public static class GridOrientationExtentions
{
    public static GridOrientation RotatedCW(this GridOrientation o) =>
        (GridOrientation)(((int)o + 1) & 3);

    public static GridOrientation RotatedCCW(this GridOrientation o) =>
        (GridOrientation)(((int)o + 3) & 3);

    public static Vector2Int OrientedSize(this Vector2Int baseSize, GridOrientation o)
    {
        // For 90°/270° swap width/height
        return (o == GridOrientation.East || o == GridOrientation.West)
            ? new Vector2Int(baseSize.y, baseSize.x)
            : baseSize;
    }

    public static float ToYaw(this GridOrientation o) => ((int)o) * 90f;
    public static Quaternion ToRotation(this GridOrientation o) => Quaternion.Euler(0f, o.ToYaw(), 0f);

    // New: map orientation <-> direction and deltas
    public static Direction ToDirection(this GridOrientation o)
    {
        switch (o)
        {
            case GridOrientation.North: return Direction.North;
            case GridOrientation.East:  return Direction.East;
            case GridOrientation.South: return Direction.South;
            case GridOrientation.West:  return Direction.West;
            default: return Direction.North;
        }
    }

    public static GridOrientation ToOrientation(this Direction d)
    {
        switch (d)
        {
            case Direction.North: return GridOrientation.North;
            case Direction.East:  return GridOrientation.East;
            case Direction.South: return GridOrientation.South;
            case Direction.West:  return GridOrientation.West;
            default: return GridOrientation.North;
        }
    }

    public static Vector2Int ToDelta(this Direction d)
    {
        switch (d)
        {
            case Direction.North: return Vector2Int.up;
            case Direction.South: return Vector2Int.down;
            case Direction.East:  return Vector2Int.right;
            case Direction.West:  return Vector2Int.left;
            default: return Vector2Int.zero;
        }
    }

    public static Vector2Int OrientationToDelta(GridOrientation o)
    {
        switch (o)
        {
            case GridOrientation.North: return Vector2Int.up;
            case GridOrientation.East: return Vector2Int.right;
            case GridOrientation.South: return Vector2Int.down;
            case GridOrientation.West: return Vector2Int.left;
            default: return Vector2Int.zero;
        }
    }

    public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);

    // Rotate a local direction by an orientation (i.e., machine/belt facing)
    public static Direction Rotate(this Direction local, GridOrientation by)
    {
        int l = (int)local;
        int b = (int)by;
        return (Direction)((l + b) & 3);
    }
}
