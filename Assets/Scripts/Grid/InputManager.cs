using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private LayerMask clickableLayerMask;

    private Vector3 lastPosition;

    public Vector3 GetSelectedMousePositon()
    {
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = sceneCamera.nearClipPlane;
        Ray ray = sceneCamera.ScreenPointToRay(mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100, clickableLayerMask))
        {
            lastPosition =  hit.point;
        }
        return lastPosition;
    }
}
