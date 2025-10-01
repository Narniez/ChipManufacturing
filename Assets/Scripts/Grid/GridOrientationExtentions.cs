using UnityEngine;
public enum GridOrientation { North = 0, East = 1, South = 2, West = 3 }
public static class GridOrientationExtentions
{
    public static GridOrientation RotatedCW(this GridOrientation o) =>
        (GridOrientation)(((int)o + 1) & 3);

    public static Vector2Int OrientedSize(this Vector2Int baseSize, GridOrientation o)
    {
        // For 90°/270° swap width/height
        return (o == GridOrientation.East || o == GridOrientation.West)
            ? new Vector2Int(baseSize.y, baseSize.x)
            : baseSize;
    }
}
