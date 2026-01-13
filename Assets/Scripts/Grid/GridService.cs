using GridCellTypes;
using System.Collections.Generic;
using UnityEngine;

public class GridService : MonoBehaviour
{
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 origin = Vector3.zero;

    private Dictionary<Vector2Int, CellData> cells = new Dictionary<Vector2Int, CellData>(); // ocupancy map

    [SerializeField, Tooltip("Grid width (columns). Runtime-set by CreateGridFromPlane.")]
    private int cols = -1;
    [SerializeField, Tooltip("Grid height (rows). Runtime-set by CreateGridFromPlane.")]
    private int rows = -1;

    public float CellSize => cellSize;
    public Vector3 Origin => origin;
    public int Cols => cols;
    public int Rows => rows;

    public bool HasGrid => cols > 0 && rows > 0;
    public int OccupiedCount => cells.Count;

    public Vector2Int WorldToCell(Vector3 world)
    {
        // converting world position to grid indices (x/z plane)
        int cx = Mathf.FloorToInt((world.x - origin.x) / cellSize);
        int cy = Mathf.FloorToInt((world.z - origin.z) / cellSize);
        return new Vector2Int(cx, cy);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell, float y)
    {
        float wx = origin.x + (cell.x + 0.5f) * cellSize;
        float wz = origin.z + (cell.y + 0.5f) * cellSize;
        return new Vector3(wx, y, wz);
    }

    public Vector3 CellToWorldCenter(Vector3 originMin, int x, int y, float yLift = 0f)
    {
        float wx = originMin.x + (x + 0.5f) * cellSize;
        float wz = originMin.z + (y + 0.5f) * cellSize;
        return new Vector3(wx, yLift, wz);
    }

    public void CreateGridFromPlane(Transform plane, out int outCols, out int outRows, out Vector3 originMin)
    {
        const float UNITY_PLANE_SIZE = 10f;

        // deriving real-world plane size from Unity plane scaling
        float planeWidth = plane.localScale.x * UNITY_PLANE_SIZE;
        float planeDepth = plane.localScale.z * UNITY_PLANE_SIZE;

        outCols = Mathf.Max(1, Mathf.RoundToInt(planeWidth / cellSize));
        outRows = Mathf.Max(1, Mathf.RoundToInt(planeDepth / cellSize));

        float targetWidth = outCols * cellSize;
        float targetDepth = outRows * cellSize;

        // snapping plane scale so its edges align with the grid
        Vector3 local = plane.localScale;
        local.x = targetWidth / UNITY_PLANE_SIZE;
        local.z = targetDepth / UNITY_PLANE_SIZE;
        plane.localScale = local;

        Vector3 center = plane.position;
        originMin = center - Vector3.right * (targetWidth * 0.5f) - Vector3.forward * (targetDepth * 0.5f);

        origin = originMin;
        cols = outCols;
        rows = outRows;
    }

    // Bounds / Area Helpers

    public bool IsInside(Vector2Int cell) =>
        HasGrid && cell.x >= 0 && cell.y >= 0 && cell.x < cols && cell.y < rows;

    public Vector2Int ClampAnchor(Vector2Int anchor, Vector2Int size)
    {
        // clamping anchor so the full area stays inside the grid
        if (!HasGrid) return anchor;
        int maxX = Mathf.Max(0, cols - size.x);
        int maxY = Mathf.Max(0, rows - size.y);
        return new Vector2Int(
            Mathf.Clamp(anchor.x, 0, maxX),
            Mathf.Clamp(anchor.y, 0, maxY));
    }

    public bool IsAreaInside(Vector2Int anchor, Vector2Int size)
    {
        if (!HasGrid) return false;
        return anchor.x >= 0 && anchor.y >= 0 &&
               anchor.x + size.x <= cols &&
               anchor.y + size.y <= rows;
    }

    public IEnumerable<Vector2Int> EnumerateArea(Vector2Int anchor, Vector2Int size)
    {
        // iterating all cells inside a rectangle footprint
        for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                yield return new Vector2Int(anchor.x + x, anchor.y + y);
    }

    public bool IsAreaFree(Vector2Int anchor, Vector2Int size, Object ignore = null)
    {
        foreach (var c in EnumerateArea(anchor, size))
        {
            if (cells.TryGetValue(c, out var d) && d.occupant != null && d.occupant != ignore)
                return false;
        }
        return true;
    }

    public void SetAreaOccupant(Vector2Int anchor, Vector2Int size, Object occupant)
    {
        foreach (var c in EnumerateArea(anchor, size))
        {
            if (occupant == null) cells.Remove(c);
            else cells[c] = new CellData { occupant = occupant };
        }
    }

    public bool IsCellOccupied(Vector2Int cell) => cells.ContainsKey(cell);

    public bool TryGetCell(Vector2Int cell, out CellData data)
    {
        if (cells.TryGetValue(cell, out data)) return true;
        data = default;
        return false;
    }

    public bool Clear(Vector2Int cell) => cells.Remove(cell);

    public bool SetOccupant(Vector2Int cell, Object occupant)
    {
        if (occupant == null) return Clear(cell);

        bool changed = true;
        if (cells.TryGetValue(cell, out CellData existing))
            changed = existing.occupant != occupant;

        cells[cell] = new CellData { occupant = occupant };
        return changed;
    }

    public bool AreAdjacent4(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

    public IEnumerable<Neighbor> GetNeighbors(Vector2Int cell)
    {
        // 4-directional neighbors
        Vector2Int n = cell + Vector2Int.up;
        yield return new Neighbor(n, Direction.North, IsCellOccupied(n));

        Vector2Int s = cell + Vector2Int.down;
        yield return new Neighbor(s, Direction.South, IsCellOccupied(s));

        Vector2Int e = cell + Vector2Int.right;
        yield return new Neighbor(e, Direction.East, IsCellOccupied(e));

        Vector2Int w = cell + Vector2Int.left;
        yield return new Neighbor(w, Direction.West, IsCellOccupied(w));
    }
}
