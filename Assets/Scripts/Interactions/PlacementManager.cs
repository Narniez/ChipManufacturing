using UnityEngine;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Factory")]
    [SerializeField] private MachineFactory factory;

    [Header("Edge Scroll While Dragging")]
    [SerializeField] private float edgeZonePixels = 48f;
    [SerializeField] private float edgeScrollSpeed = 12f; 

    // [Header("Grid")]
    // [SerializeField] private GridSnapper grid; 

    private CameraController _camCtrl; 
    private IDraggable dragging;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        _camCtrl = FindFirstObjectByType<CameraController>(); 
    }

    private void OnEnable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart += OnHoldStart;
            InteractionManager.Instance.OnHoldMove += OnHoldMove;
            InteractionManager.Instance.OnHoldEnd  += OnHoldEnd;
            InteractionManager.Instance.OnTap      += OnTap;
        }
    }

    private void OnDisable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart -= OnHoldStart;
            InteractionManager.Instance.OnHoldMove  -= OnHoldMove;
            InteractionManager.Instance.OnHoldEnd   -= OnHoldEnd;
            InteractionManager.Instance.OnTap       -= OnTap;
        }
    }

    // Buy spawns immediately at screen center (no confirm)
    public void StartPlacement(MachineData machineData)
    {
        if (factory == null || machineData == null || machineData.prefab == null)
        {
            Debug.LogError("PlacementManager.StartPlacement: Missing factory or machine data/prefab.");
            return;
        }

        var cam = Camera.main;
        Vector3 centerPos = ScreenToGround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), cam);
        var created = factory.CreateMachine(machineData, centerPos);
        // If created also implements IDraggable, it will be drag-enabled automatically by interactions.
    }

    private void OnTap(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        // Optional global tap handling (selection/UI)
    }

    private void OnHoldStart(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        dragging = interactable as IDraggable;
        if (dragging == null || !dragging.CanDrag) return;

        if (_camCtrl != null) _camCtrl.SetInputLocked(true);

        //world = ApplySnap(world); // Snap disabled for now
        dragging.OnDragStart();
        dragging.OnDrag(world);
    }

    private void OnHoldMove(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        if (dragging == null || dragging != interactable as IDraggable) return;

        EdgeScrollCamera(screen);

        // world = ApplySnap(world); 
        dragging.OnDrag(world);
    }

    private void OnHoldEnd(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        if (dragging == null || dragging != interactable as IDraggable) return;

        // world = ApplySnap(world);
        dragging.OnDrag(world);
        dragging.OnDragEnd();

        if (_camCtrl != null) _camCtrl.SetInputLocked(false);
        dragging = null;
    }

    // private Vector3 ApplySnap(Vector3 world)
    // {
    //     return grid != null ? grid.Snap(world) : world;
    // }

    private void EdgeScrollCamera(Vector2 screenPos)
    {
        if (_camCtrl == null) return;

        float w = Screen.width;
        float h = Screen.height;

        float xDir = 0f;
        if (screenPos.x < edgeZonePixels)
            xDir = -(1f - Mathf.Clamp01(screenPos.x / edgeZonePixels));
        else if (screenPos.x > w - edgeZonePixels)
            xDir = (1f - Mathf.Clamp01((w - screenPos.x) / edgeZonePixels));

        float yDir = 0f;
        if (screenPos.y < edgeZonePixels)
            yDir = -(1f - Mathf.Clamp01(screenPos.y / edgeZonePixels));
        else if (screenPos.y > h - edgeZonePixels)
            yDir = (1f - Mathf.Clamp01((h - screenPos.y) / edgeZonePixels));

        if (Mathf.Approximately(xDir, 0f) && Mathf.Approximately(yDir, 0f)) return;

        Transform camT = _camCtrl.transform;
        Vector3 right = camT.right;
        Vector3 forward = camT.forward; forward.y = 0f; forward.Normalize();

        Vector3 move = (right * xDir + forward * yDir) * edgeScrollSpeed * Time.unscaledDeltaTime;
        _camCtrl.NudgeWorld(move);
    }

    private static Vector3 ScreenToGround(Vector2 screenPos, Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : Vector3.zero;
    }
}
