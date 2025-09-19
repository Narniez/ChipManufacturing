using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [SerializeField] private MachineFactory factory;

    private MachineData selectedMachine;
    private GameObject previewObject;
    [SerializeField] private bool isPlacing;

    // Tap filtering (prevents preview jumping when pressing UI like Confirm)
    [SerializeField] private float tapMaxMovePixels = 20f;
    private Vector2 mouseDownPos;
    private bool mouseDownActive;

    // Robust UI guard: block world taps if a click/touch began on UI, until it is released
    private bool blockMouseTapUntilRelease = false;
    private readonly HashSet<int> blockedFingerIds = new HashSet<int>();
    private bool blockAllTapsUntilNoTouches = false; 

    private int activeFingerId = -1;
    private Vector2 touchDownPos;

    public bool IsPlacing => isPlacing;
    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
    }

    // Called by Buy button
    public void StartPlacement(MachineData machineData)
    {
        CancelPlacement();

        selectedMachine = machineData;

        // Create a transparent preview object
        previewObject = Instantiate(machineData.prefab);
        SetPreviewMaterial(previewObject, true);

        isPlacing = true;

        // Spawn at screen center
        Vector3 centerPos = GetWorldFromScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        previewObject.transform.position = centerPos;
    }

    // Called by Confirm button
    public void ConfirmPlacement()
    {
        if (!isPlacing) return;

        // Guard: block any tap processing caused by this click/tap
        blockMouseTapUntilRelease = true;
        blockAllTapsUntilNoTouches = true;

        // Place at current preview position (do NOT move on confirm)
        PlaceMachine(previewObject.transform.position);
        CancelPlacement();

       Debug.Log("Machinde placed isPlacing = " + isPlacing);
    }

    // Optional: hook to a "Cancel" button or ESC/right-click
    public void CancelPlacement()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }

        selectedMachine = null;
        isPlacing = false;

        // reset tap state
        mouseDownActive = false;
        activeFingerId = -1;
    }

    private void Update()
    {
        if (!isPlacing) return;

        // Move preview on discrete click/tap (mouse or touch)
        if (TryGetTapWorldPosition(out Vector3 worldPos))
        {
            previewObject.transform.position = worldPos;
        }

        //// Convenience: right-click cancels while testing with mouse
        //if (Input.GetMouseButtonDown(1))
        //{
        //    CancelPlacement();
        //}
    }

    private void PlaceMachine(Vector3 position)
    {
        factory.CreateMachine(selectedMachine, position);
    }

    // Only treat a "tap" when pointer goes down and up outside UI, and didn't move too far.
    private bool TryGetTapWorldPosition(out Vector3 worldPos)
    {
        // If we are swallowing remaining touch ups after Confirm
        if (blockAllTapsUntilNoTouches && Input.touchCount > 0)
        {
            worldPos = default;
            return false;
        }
        if (blockAllTapsUntilNoTouches && Input.touchCount == 0)
        {
            blockAllTapsUntilNoTouches = false;
        }

        // TOUCH
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase == TouchPhase.Began)
                {
                    // If began over UI, block this finger until it ends
                    bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId);
                    if (overUI) blockedFingerIds.Add(t.fingerId);

                    // Track the first active finger that is not blocked
                    if (activeFingerId == -1 && !blockedFingerIds.Contains(t.fingerId))
                    {
                        activeFingerId = t.fingerId;
                        touchDownPos = t.position;
                    }
                }

                // If this finger was UI-blocked, ignore until it ends
                if (blockedFingerIds.Contains(t.fingerId))
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        blockedFingerIds.Remove(t.fingerId);
                    continue;
                }

                if (t.fingerId == activeFingerId && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
                {
                    float moved = (t.position - touchDownPos).magnitude;
                    bool validTap = moved <= tapMaxMovePixels;

                    activeFingerId = -1;

                    if (validTap)
                    {
                        worldPos = GetWorldFromScreen(t.position);
                        return true;
                    }
                }
            }
        }
        else
        {
            // No touches -> clear active finger
            activeFingerId = -1;
            blockedFingerIds.Clear();
        }

        // MOUSE
        if (Input.GetMouseButtonDown(0))
        {
            mouseDownActive = true;
            mouseDownPos = Input.mousePosition;

            // If mouse down starts over UI, block until release
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                blockMouseTapUntilRelease = true;
        }
        else if (mouseDownActive && Input.GetMouseButtonUp(0))
        {
            mouseDownActive = false;

            // If a UI interaction began, swallow this up and clear the block
            if (blockMouseTapUntilRelease)
            {
                blockMouseTapUntilRelease = false;
                worldPos = default;
                return false;
            }

            float moved = ((Vector2)Input.mousePosition - mouseDownPos).magnitude;
            bool validClick = moved <= tapMaxMovePixels;

            if (validClick)
            {
                worldPos = GetWorldFromScreen(Input.mousePosition);
                return true;
            }
        }

        worldPos = default;
        return false;
    }

    private Vector3 GetWorldFromScreen(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero); // Ground plane at y=0
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return Vector3.zero;
    }

    private void SetPreviewMaterial(GameObject obj, bool isPreview)
    {
        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        var mat = renderer.materials[0];
        Color c = mat.color;
        c.a = isPreview ? 0.5f : 1f;
        mat.color = c;
    }
}
