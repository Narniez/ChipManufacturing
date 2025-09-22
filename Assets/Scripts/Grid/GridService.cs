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
        int cy = Mathf.FloorToInt((world.y - origin.y) / cellSize);
        return new Vector2Int(cx, cy);
    }

    //Gets the world space center position of a cell

    public Vector3 CellToWorldCenter(Vector2Int cell, float y)
    {
        float wx = origin.x + (cell.x + 0.5f) * cellSize;
        float wz = origin.z + (cell.y + 0.5f) * cellSize;
        return new Vector3(wx,y, wz);

    }

    //Checks if a cell is currently occupied

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

    //Checks if 2 cells are adjacent in 4 directions

    public bool AreAdjacent4(Vector2Int a,  Vector2Int b)
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
