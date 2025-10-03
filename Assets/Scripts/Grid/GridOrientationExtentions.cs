using UnityEngine;
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
}
