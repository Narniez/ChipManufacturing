using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GridService grid;
    [SerializeField] private Transform plane;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Transform cellParent;

    [Header("Behaviour")]
    [SerializeField] private bool autoRebuild = true; //regenerate when plane scale or cell size changes
    [SerializeField] private float yOffset = 0.01f;

    private Dictionary<Vector2Int, GameObject> spawned = new Dictionary<Vector2Int, GameObject>();

    private Vector3 lastPlaneScale;
    private Vector3 lastPlanePos;
    private float lastCellSize;

    private void OnEnable()
    {
        lastPlaneScale = Vector3.negativeInfinity;
        lastPlanePos = Vector3.negativeInfinity;
        lastCellSize = -1f;
    }

    private void Start()
    {
        if (grid == null)
        {
            Debug.LogWarning("GridService reference is missing");
            return;
        }
        if (plane == null)
        {
            Debug.LogWarning("Plane reference is missing");
            return;
        }
        if (cellPrefab == null)
        {
            Debug.LogWarning("Cell Prefab reference is missing");
            return;
        }
        InitializeGrid();
    }

    private void Update()
    {
        Rebuild(autoRebuild);
    }

    private void Rebuild(bool force)
    {
        if (grid == null || plane == null || cellPrefab == null) return;

        bool planeChanged = plane.localScale != lastPlaneScale || plane.position != lastPlanePos;

        bool cellSizeChanged = Mathf.Abs(grid.CellSize - lastCellSize) > Mathf.Epsilon;

        if (force || planeChanged || cellSizeChanged)
        {
            InitializeGrid();

            lastPlaneScale = plane.localScale;
            lastPlanePos = plane.position;
            lastCellSize = grid.CellSize;
        }
    }

    private void InitializeGrid()
    {
        //snaps the plane to whole cells
        grid.CreateGridFromPlane(plane, out int cols, out int rows, out Vector3 originMin);

        if (cellParent == null)
        {
            cellParent = this.transform;
        }

        //clears previous cells
        for (int i = cellParent.childCount - 1; i >= 0; i--)
        {
            GameObject c = cellParent.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(c);
            else Destroy(c);
#else
            Destroy(c);
#endif
        }
        spawned.Clear();

        //spawns cells
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Vector3 center = grid.CellToWorldCenter(originMin, x, y, yOffset);
                GameObject go = Instantiate(cellPrefab, center, Quaternion.identity, cellParent);
                go.name = "Cell_" + x + "_" + y;

                spawned[new Vector2Int(x, y)] = go;
            }
        }

    }

}

