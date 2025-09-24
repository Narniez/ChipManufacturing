using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private GameObject placementObj;
    [SerializeField] private GameObject tileIndicator;

    [SerializeField] private InputManager inputManager;

    [SerializeField] private Grid grid;

    private void Update()
    {
        Vector3 mousePosition = inputManager.GetSelectedMousePositon();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);
        placementObj.transform.position = mousePosition;
        tileIndicator.transform.position = grid.CellToWorld(gridPosition);
    }
}
