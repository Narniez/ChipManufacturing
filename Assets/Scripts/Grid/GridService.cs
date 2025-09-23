using GridCellTypes;
using System.Collections.Generic;
using UnityEngine;

public class GridService : MonoBehaviour
{
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 origin = Vector3.zero;

    private Dictionary<Vector2Int, CellData> cells = new Dictionary<Vector2Int, CellData>();

    public float CellSize { get { return cellSize; } }

    public Vector3 Origin { get { return origin; } }

    public int OccupiedCount { get { return cells.Count; } }

    //Converts a world position to grid cell coordinates

    public Vector2Int WorldToCell(Vector3 world)
    {
        int cx = Mathf.FloorToInt((world.x - origin.x) / cellSize);
        int cy = Mathf.FloorToInt((world.z - origin.z) / cellSize);
        return new Vector2Int(cx, cy);
    }

    //Gets the world space center position of a cell

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

    public void CreateGridFromPlane(Transform plane, out int cols, out int rows, out Vector3 originMin)
    {
        const float UNITY_PLANE_SIZE = 10f;

        float planeWidth = plane.localScale.x * UNITY_PLANE_SIZE;
        float planeDepth = plane.localScale.z * UNITY_PLANE_SIZE;

        cols = Mathf.Max(1, Mathf.RoundToInt(planeWidth / cellSize));
        rows = Mathf.Max(1, Mathf.RoundToInt(planeDepth / cellSize));

        float targetWidth = cols * cellSize;
        float targetDepth = rows * cellSize;

        //Snaps the scale so the plane matches whole cells
        Vector3 local = plane.localScale;
        local.x = targetWidth / UNITY_PLANE_SIZE;
        local.z = targetDepth / UNITY_PLANE_SIZE;
        plane.localScale = local;

        Vector3 center = plane.position;
        originMin = center - Vector3.right * (targetWidth * 0.5f) - Vector3.forward * (targetDepth * 0.5f);
    }

    public bool IsCellOccupied(Vector2Int cell)
    {
        return cells.ContainsKey(cell);
    }


    public bool TryGetCell(Vector2Int cell, out CellData data)
    {
        if (cells.TryGetValue(cell, out data))
        {
            return true;
        }

        data = default(CellData);
        return false;
    }

    public bool Clear(Vector2Int cell)
    {
        return cells.Remove(cell);
    }

    //Marks a cell as occupied
    public bool SetOccupant(Vector2Int cell, Object occupant)
    {
        if (occupant == null)
        {
            return Clear(cell);
        }

        bool changed = true;
        CellData existing;
        if (cells.TryGetValue(cell, out existing))
        {
            changed = existing.occupant != occupant;
        }

        cells[cell] = new CellData { occupant = occupant };
        return changed;
    }

    //Checks if 2 cells are adjacent in 4 directions

    public bool AreAdjacent4(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    //Gets the 4 neighbours of a cell and their occupancy status

    public IEnumerable<Neighbor> GetNeighbors(Vector2Int cell)
    {
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
