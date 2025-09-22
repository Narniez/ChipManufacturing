using UnityEngine;
using UnityEngine.EventSystems;
public class InteractionManager : MonoBehaviour
{
    [SerializeField] private float holdTime = 1;

    private Camera _camera;
    private float touchTimer;
    private bool isTouching;
    private IInteractable currentInteractable;

    void Start()
    {
        _camera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartInteraction();
        }

        if (Input.GetMouseButton(0) && isTouching)
        {
            touchTimer += Time.deltaTime;
            if (touchTimer >= holdTime)
            {
                currentInteractable?.OnHold();
                isTouching = false; 
            }
        }

        if (Input.GetMouseButtonUp(0) && isTouching)
        {
            if (touchTimer < holdTime)
                currentInteractable?.OnTap();

            EndInteraction();
        }
    }

    private void StartInteraction()
    {
        touchTimer = 0;
        isTouching = true;

        currentInteractable = RaycastInteractable();
    }

    private void EndInteraction()
    {
        touchTimer = 0;
        isTouching = false;
        currentInteractable = null;
    }

    private IInteractable RaycastInteractable()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.collider.GetComponent<IInteractable>();
        }
        return null;
    }
}
