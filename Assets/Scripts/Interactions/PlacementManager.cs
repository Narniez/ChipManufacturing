using UnityEngine;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [Header("Factory")]
    [SerializeField] private MachineFactory factory;

    [Header("Grid Snapping")]
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private GridService gridService;
    [SerializeField] private float snapYOffset = 0f; 

    [Header("Edge Scroll While Dragging")]
    [SerializeField] private float edgeZonePixels = 48f;
    [SerializeField] private float edgeScrollSpeed = 12f;

    [Header("Input")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;

    private CameraController _camCtrl;
    private IGridOccupant dragging;
    private Vector2Int currentAnchor;
    private GridOrientation currentOrientation = GridOrientation.North;

    private Vector3 lastValidWorld;

    // Original footprint (restore if placement invalid)
    private bool hadOriginalArea;
    private Vector2Int originalAnchor;
    private GridOrientation originalOrientation;
    private Vector2Int originalSize;
    private Vector3 originalWorld;

    // Vertical placement
    private float draggingHeightOffset = 0f; 

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        _camCtrl = FindFirstObjectByType<CameraController>();
    }

    void Update()
    {
        if (dragging != null && Input.GetKeyDown(rotateKey))
        {
            ApplyRotation();
        }
    }

    private void ApplyRotation()
    {
        if (dragging == null) return;

        currentOrientation = currentOrientation.RotatedCW();
        var size = dragging.BaseSize.OrientedSize(currentOrientation);
        currentAnchor = gridService.ClampAnchor(currentAnchor, size);

        Vector3 world = AnchorToWorldCenter(currentAnchor, size);
        lastValidWorld = world;

        dragging.SetPlacement(currentAnchor, currentOrientation);
        dragging.OnDrag(world);
    }

    private void OnEnable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnHoldStart += OnHoldStart;
            InteractionManager.Instance.OnHoldMove  += OnHoldMove;
            InteractionManager.Instance.OnHoldEnd   += OnHoldEnd;
            InteractionManager.Instance.OnTap       += OnTap;
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
            Debug.LogError("PlacementManager.StartPlacement: Missing factory or data/prefab.");
            return;
        }

        var cam = Camera.main;
        Vector3 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector3 world = ScreenToGround(screenCenter, cam);

        Machine machine = factory.CreateMachine(machineData, world);
        if (snapToGrid && gridService != null && machine.TryGetComponent<IGridOccupant>(out var occ))
        {
            var size = occ.BaseSize.OrientedSize(GridOrientation.North);
            Vector2Int cell = gridService.WorldToCell(world);
            Vector2Int anchor = gridService.ClampAnchor(cell, size);
            world = AnchorToWorldCenter(anchor, size, ComputePivotBottomOffset(machine.transform));
            occ.SetPlacement(anchor, GridOrientation.North);
            machine.transform.position = world;
            // Occupy immediately
            gridService.SetAreaOccupant(anchor, size, machine.gameObject);
        }
    }

    private void OnTap(IInteractable interactable, Vector2 screen, Vector3 world) { }

    private void OnHoldStart(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        dragging = interactable as IGridOccupant;
        if (dragging == null || !dragging.CanDrag || gridService == null || !gridService.HasGrid) return;

        if (_camCtrl != null) _camCtrl.SetInputLocked(true);
        currentOrientation = dragging.Orientation;

        // Cache original occupancy & clear it
        var placedSize = dragging.BaseSize.OrientedSize(dragging.Orientation);
        if (gridService.IsAreaInside(dragging.Anchor, placedSize))
        {
            hadOriginalArea = true;
            originalAnchor = dragging.Anchor;
            originalOrientation = dragging.Orientation;
            originalSize = placedSize;
            originalWorld = dragging.DragTransform.position;
            // Free its cells so it can move & revisit them
            gridService.SetAreaOccupant(originalAnchor, originalSize, null);
        }
        else
        {
            hadOriginalArea = false;
        }

        // Compute vertical offset once
        draggingHeightOffset = ComputePivotBottomOffset(dragging.DragTransform);

        world = ApplySnap(world);
        lastValidWorld = world;
        dragging.OnDragStart();
        dragging.OnDrag(world);
    }

    private void OnHoldMove(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        if (dragging == null || dragging != interactable as IGridOccupant) return;

        EdgeScrollCamera(screen);
        Vector3 snapped = ApplySnap(world);
        dragging.OnDrag(snapped);
    }

    private void OnHoldEnd(IInteractable interactable, Vector2 screen, Vector3 world)
    {
        if (dragging == null || dragging != interactable as IGridOccupant) return;

        Vector3 snapped = ApplySnap(world);
        var size = dragging.BaseSize.OrientedSize(currentOrientation);

        bool ok = dragging.CanPlace(gridService, currentAnchor, currentOrientation);
        if (ok)
        {
            gridService.SetAreaOccupant(currentAnchor, size, (dragging as Component).gameObject);
            dragging.SetPlacement(currentAnchor, currentOrientation);
        }
        else
        {
            // Restore previous footprint if any
            if (hadOriginalArea)
            {
                gridService.SetAreaOccupant(originalAnchor, originalSize, (dragging as Component).gameObject);
                dragging.SetPlacement(originalAnchor, originalOrientation);
                snapped = originalWorld;
            }
            else
            {
                Debug.LogWarning("Invalid placement and no original area to restore.");
            }
        }

        dragging.OnDrag(snapped);
        dragging.OnDragEnd();

        if (_camCtrl != null) _camCtrl.SetInputLocked(false);
        dragging = null;
        hadOriginalArea = false;
    }

    private Vector3 ApplySnap(Vector3 world)
    {
        if (!snapToGrid || gridService == null || dragging == null || !gridService.HasGrid)
            return world;

        var size = dragging.BaseSize.OrientedSize(currentOrientation);
        Vector2Int cell = gridService.WorldToCell(world);

        if (cell.x < 0 || cell.y < 0 || cell.x >= gridService.Cols || cell.y >= gridService.Rows)
            return lastValidWorld;

        Vector2Int anchor = gridService.ClampAnchor(cell, size);
        currentAnchor = anchor;
        dragging.SetPlacement(anchor, currentOrientation);

        Vector3 snappedWorld = AnchorToWorldCenter(anchor, size);
        lastValidWorld = snappedWorld;
        return snappedWorld;
    }

    private Vector3 AnchorToWorldCenter(Vector2Int anchor, Vector2Int size)
    {
        return AnchorToWorldCenter(anchor, size, draggingHeightOffset);
    }

    private Vector3 AnchorToWorldCenter(Vector2Int anchor, Vector2Int size, float heightOffset)
    {
        float y = gridService.Origin.y + heightOffset + snapYOffset;
        float wx = gridService.Origin.x + (anchor.x + size.x * 0.5f) * gridService.CellSize;
        float wz = gridService.Origin.z + (anchor.y + size.y * 0.5f) * gridService.CellSize;
        return new Vector3(wx, y, wz);
    }

    private float ComputePivotBottomOffset(Transform root)
    {
        // Gather all renderers; if none, assume pivot already at base.
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return 0f;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        float pivotY = root.position.y;
        float bottomY = b.min.y;

        // Distance from pivot to bottom so we can raise object so bottom sits at grid plane.
        return pivotY - bottomY;
    }

    private void EdgeScrollCamera(Vector2 screenPos)
    {
        if (_camCtrl == null) return;

        float w = Screen.width;
        float h = Screen.height;

        float xDir = 0f;
        if (screenPos.x < edgeZonePixels) xDir = -(1f - Mathf.Clamp01(screenPos.x / edgeZonePixels));
        else if (screenPos.x > w - edgeZonePixels) xDir = (1f - Mathf.Clamp01((w - screenPos.x) / edgeZonePixels));

        float yDir = 0f;
        if (screenPos.y < edgeZonePixels) yDir = -(1f - Mathf.Clamp01(screenPos.y / edgeZonePixels));
        else if (screenPos.y > h - edgeZonePixels) yDir = (1f - Mathf.Clamp01((h - screenPos.y) / edgeZonePixels));

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
