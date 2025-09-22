using UnityEditor;
using UnityEngine;

public class GizmosDisplay : MonoBehaviour
{
    [SerializeField] private GridService gridService;
    [SerializeField] private int gridExtent = 50;
    [SerializeField] private Color line;

    [SerializeField] private bool displayGrid = true;

    private void OnDrawGizmos()
    {
        if(gridService == null || !displayGrid) return;

        Gizmos.color = line;

        float cellSize = gridService.CellSize;
        Vector3 origin = gridService.Origin;

        //vertical lines
        for(int x= -gridExtent; x <= gridExtent; x++)
        {
            float wx = origin.x + x * cellSize;
            Gizmos.DrawLine(new Vector3(wx, 0, origin.z - gridExtent * cellSize),
                            new Vector3(wx, 0, origin.z + gridExtent * cellSize));
        }

        //horizontal lines
        for (int y = -gridExtent; y <= gridExtent; y++)
        {
            float wz = origin.z + y * cellSize;
            Gizmos.DrawLine(new Vector3(origin.z - gridExtent * cellSize, 0, wz),
                            new Vector3(origin.z + gridExtent * cellSize, 0, wz));
        }
    }
}
