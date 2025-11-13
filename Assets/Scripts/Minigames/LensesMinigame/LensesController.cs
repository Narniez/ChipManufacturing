using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LensesController : MonoBehaviour
{
    [Header("Lens Settings")]
    [Tooltip("Auto-find all Lens components in the scene at Start.")]
    [SerializeField] private bool autoFindLenses = true;
    [SerializeField] private Transform emitter;
    [SerializeField] private LineRenderer line;

    [Header("Bounce Settings")]
    [Tooltip("Total path length the beam can travel across all bounces.")]
    [SerializeField] private float maxDistance = 5000f;
    [Tooltip("Max number of reflections before stopping.")]
    [SerializeField] private int maxBounces = 8;
    [Tooltip("Layers the beam can hit.")]
    [SerializeField] private LayerMask rayMask = ~0;
    [Tooltip("Reflect off any collider. If false, only reflect off Lens components with isReflective=true.")]
    [SerializeField] private bool reflectOnAnyCollider = true;
    [Tooltip("Start direction. If false, uses the emitter's forward.")]
    [SerializeField] private bool startDownward = false;

    [Header("Completion")]
    public UnityEvent onPuzzleComplete;

    [SerializeField] private List<GameObject> lenses = new();

    private bool isMinigameActive = false;
    private bool completed;

    private Lens currentSelected;

    const float SURFACE_OFFSET = 0.001f;
    const float MIN_SEGMENT = 0.0005f; // prevent micro-bounce loops

    private void Awake()
    {
        foreach (var lens in lenses)
            lens.SetActive(true);

        if (line != null)
            line.useWorldSpace = true;
    }

    private void Update()
    {
        CastAndRenderBeam();
    }

    public void RegisterLens(GameObject lens)
    {
        lenses.Add(lens);
        Debug.Log("Registered lens: " + lens.name);
    }

    public void ActivateMinigame()
    {
        isMinigameActive = true;
    }

    public void SelectLens(Lens lens)
    {
        if (lens == currentSelected)
        {
            if (currentSelected != null)
            {
                currentSelected.ToggleSelected();
                currentSelected = null;
            }
            return;
        }

        if (currentSelected != null)
            currentSelected.ToggleSelected();

        currentSelected = lens;
        if (currentSelected != null)
            currentSelected.ToggleSelected();
    }

    public void DeselectAll()
    {
        if (currentSelected != null)
        {
            currentSelected.Reset();
            currentSelected = null;
        }
    }

    // Robust bouncing laser
    private void CastAndRenderBeam()
    {
        if (emitter == null || line == null)
            return;

        Vector3 origin = emitter.position;
        Vector3 dir = (startDownward ? Vector3.down : emitter.forward).normalized;

        List<Vector3> points = new List<Vector3>(maxBounces);
        points.Add(origin);

        float remaining = Mathf.Max(0f, maxDistance);
        int bounces = 0;

        while (bounces <= maxBounces && remaining > 0f)
        {
            Ray ray = new Ray(origin, dir);

            if (Physics.Raycast(ray, out RaycastHit hit, remaining, rayMask, QueryTriggerInteraction.Ignore))
            {
                // guard tiny distances (avoid jitter when starting inside geometry)
                float traveled = Mathf.Max(hit.distance, 0f);
                if (traveled < MIN_SEGMENT)
                {
                    // Nudge forward a hair and keep going to escape surface
                    origin += dir * SURFACE_OFFSET;
                    remaining -= SURFACE_OFFSET;
                    bounces++;
                    continue;
                }

                points.Add(hit.point);
                remaining -= traveled;

                // If we hit the target, complete and stop
                if (hit.collider.CompareTag("Target"))
                {
                    CompletePuzzle();
                    break;
                }

                Lens lens = hit.collider.GetComponentInParent<Lens>();
                bool doReflect = lens != null && lens.isReflective;

                if (doReflect && remaining > 0f)
                {
                    dir = Vector3.Reflect(dir, hit.normal).normalized;
                    origin = hit.point + dir * SURFACE_OFFSET; // offset to avoid self-hit
                    bounces++;
                    continue;
                }

                // Not reflecting on this surface -> stop here
                break;
            }
            else
            {
                // Nothing hit within remaining distance, extend and stop
                points.Add(origin + dir * remaining);
                break;
            }
        }

        //Draw
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i]);
    }

    private void CompletePuzzle()
    {
        if (completed) return;
        completed = true;
        onPuzzleComplete?.Invoke();
        line.gameObject.SetActive(false);
    }

    //Call this to restart the puzzle
    public void ResetPuzzle()
    {
        // reset puzzle state here if needed
    }
}
