using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-100)] 
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Detection")]
    [SerializeField] private float holdSeconds = 0.5f; 
    [SerializeField] private float moveTolerancePixels = 40f;
    [SerializeField] private LayerMask interactableMask = ~0;

    [Header("Mouse (Editor/Standalone)")]
    [SerializeField] private bool enableMouse = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public event Action<IInteractable, Vector2, Vector3> OnTap;
    public event Action<IInteractable, Vector2, Vector3> OnHoldStart;
    public event Action<IInteractable, Vector2, Vector3> OnHoldMove;
    public event Action<IInteractable, Vector2, Vector3> OnHoldEnd;

    private Camera _cam;

    // Touch tracking
    private int activeFingerId = -1;
    private Vector2 fingerDownPos;
    private float fingerDownTime;
    private bool isHolding;
    private IInteractable activeTarget;

    // Mouse tracking
    private bool mouseDown;
    private Vector2 mouseDownPos;
    private float mouseDownTime;
    private bool mouseHolding;
    private IInteractable mouseTarget;

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        Instance = this;
        _cam = Camera.main;
        if (debugLogs) Debug.Log("[IM] Awake -> Instance set");
    }

    private void Update()
    {
#if (UNITY_EDITOR || UNITY_STANDALONE) && !UNITY_IOS && !UNITY_ANDROID
        if (enableMouse) UpdateMouse();
#endif
        UpdateTouch();
    }

    private void UpdateTouch()
    {
        if (Input.touchCount == 0)
        {
            ResetTouch();
            return;
        }

        // Start tracking
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase != TouchPhase.Began) continue;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                continue;

            if (activeFingerId != -1) continue; // track only one finger

            activeFingerId = t.fingerId;
            fingerDownPos = t.position;
            fingerDownTime = Time.unscaledTime;
            isHolding = false;
            activeTarget = RaycastInteractable(t.position);
            if (debugLogs) Debug.Log($"[IM] Touch Began -> finger {activeFingerId}, target={activeTarget}");
        }

        // Update tracked finger
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId != activeFingerId) continue;

            float moved = (t.position - fingerDownPos).magnitude;

            // Start hold after time threshold, regardless of drift (tap still respects moveTolerance)
            if (!isHolding && activeTarget != null &&
                (Time.unscaledTime - fingerDownTime) >= holdSeconds)
            {
                isHolding = true;
                Vector3 world = ScreenToGround(t.position);
                if (debugLogs) Debug.Log($"[IM] HoldStart -> {activeTarget} @ {world} (moved={moved}px)");
                activeTarget.OnHold();
                OnHoldStart?.Invoke(activeTarget, t.position, world);
            }

            if (isHolding)
            {
                Vector3 world = ScreenToGround(t.position);
                OnHoldMove?.Invoke(activeTarget, t.position, world);
            }

            if (t.phase == TouchPhase.Canceled || t.phase == TouchPhase.Ended)
            {
                if (isHolding)
                {
                    Vector3 world = ScreenToGround(t.position);
                    if (debugLogs) Debug.Log($"[IM] HoldEnd -> {activeTarget} @ {world}");
                    OnHoldEnd?.Invoke(activeTarget, t.position, world);
                }
                else
                {
                    // Tap only if it didn’t drift too far
                    if (activeTarget != null && moved <= moveTolerancePixels)
                    {
                        Vector3 world = ScreenToGround(t.position);
                        if (debugLogs) Debug.Log($"[IM] Tap -> {activeTarget} @ {world} (moved={moved}px)");
                        activeTarget.OnTap();
                        OnTap?.Invoke(activeTarget, t.position, world);
                    }
                }

                ResetTouch();
            }
        }
    }

    private void UpdateMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            mouseDown = true;
            mouseDownPos = Input.mousePosition;
            mouseDownTime = Time.unscaledTime;
            mouseHolding = false;
            mouseTarget = RaycastInteractable(mouseDownPos);
            if (debugLogs) Debug.Log($"[IM] Mouse Down -> target={mouseTarget}");
        }

        if (mouseDown && !mouseHolding && mouseTarget != null)
        {
            // Start hold after time threshold (ignore drift here)
            if ((Time.unscaledTime - mouseDownTime) >= holdSeconds)
            {
                mouseHolding = true;
                Vector3 world = ScreenToGround(Input.mousePosition);
                if (debugLogs) Debug.Log($"[IM] Mouse HoldStart -> {mouseTarget} @ {world}");
                mouseTarget.OnHold();
                OnHoldStart?.Invoke(mouseTarget, (Vector2)Input.mousePosition, world);
            }
        }

        if (mouseDown && mouseHolding)
        {
            Vector3 world = ScreenToGround(Input.mousePosition);
            OnHoldMove?.Invoke(mouseTarget, (Vector2)Input.mousePosition, world);
        }

        if (Input.GetMouseButtonUp(0) && mouseDown)
        {
            if (mouseHolding)
            {
                Vector3 world = ScreenToGround(Input.mousePosition);
                if (debugLogs) Debug.Log($"[IM] Mouse HoldEnd -> {mouseTarget} @ {world}");
                OnHoldEnd?.Invoke(mouseTarget, (Vector2)Input.mousePosition, world);
            }
            else
            {
                float moved = ((Vector2)Input.mousePosition - mouseDownPos).magnitude;
                if (mouseTarget != null && moved <= moveTolerancePixels)
                {
                    Vector3 world = ScreenToGround(Input.mousePosition);
                    if (debugLogs) Debug.Log($"[IM] Mouse Tap -> {mouseTarget} @ {world} (moved={moved}px)");
                    mouseTarget.OnTap();
                    OnTap?.Invoke(mouseTarget, (Vector2)Input.mousePosition, world);
                }
            }

            mouseDown = false;
            mouseHolding = false;
            mouseTarget = null;
        }
    }

    private void ResetTouch()
    {
        if (debugLogs && activeFingerId != -1) Debug.Log("[IM] ResetTouch");
        activeFingerId = -1;
        isHolding = false;
        activeTarget = null;
    }

    private IInteractable RaycastInteractable(Vector2 screenPos)
    {
        Ray ray = _cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, interactableMask))
            return hit.collider.GetComponentInParent<IInteractable>();
        return null;
    }

    private Vector3 ScreenToGround(Vector2 screenPos)
    {
        Ray ray = _cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : Vector3.zero;
    }
}
