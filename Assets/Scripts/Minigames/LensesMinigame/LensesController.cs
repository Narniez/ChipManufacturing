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
    [SerializeField] private float emissionSpeed;
    [SerializeField] private int maxBounces = 8;
    [SerializeField] private int maxDistance = 5000;

    [SerializeField] private List<GameObject> lenses = new();

    [Header("Completion")]
    public UnityEvent onPuzzleComplete;

    private bool isMinigameActive = false;
    private bool isLenshit = false;

    const float SURFACE_OFFSET = 0.001f;
    bool completed;

    LayerMask gameMask;

    private void Awake()
    {
        foreach (var lens in lenses)
        {
            lens.SetActive(true);
        }
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

    void Update()
    {
       // if (emitter == null || line == null) return;

        //HandleLensInputs();
        //CastAndRenderBeam();
    }

    void HandleLensInputs()
    {
        float dt = Time.deltaTime;
        foreach (var lens in lenses)
        {
            if (lens == null) continue;

            float input = 0f;
            if (Input.GetKey(lens.GetComponent<Lens>().rotateRight)) input -= 1f;
            if (Input.GetKey(lens.GetComponent<Lens>().rotateLeft)) input += 1f;

           // if (Mathf.Abs(input) > 0f)
                lens.GetComponent<Lens>().DriveRotation(input, dt, Space.Self);
        }
    }

    void CastAndRenderBeam()
    {
        Vector3 origin = emitter.position;
        Vector3 dir = emitter.forward;

        // pre-allocate enough points: bounces + 2 endpoints worst-case
        Vector3[] points = new Vector3[maxBounces + 2];
        int count = 0;
        points[count++] = origin;


        for (int i = 0; i <= maxBounces; i++)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, gameMask, maxDistance, QueryTriggerInteraction.Ignore))
            {
                points[count++] = hit.point;
                /*
                                if (IsInMask(hit.collider.gameObject.layer, goalMask))
                                {
                                    if (!completed)
                                    {
                                        completed = true;
                                        onPuzzleComplete?.Invoke();
                                    }
                                    break;
                                }*/

                // 2) Lens (reflect)?
                Lens lens = hit.collider.GetComponent<Lens>();
                if (lens != null && lens.isReflective)
                {
                    // reflect and continue
                    dir = Vector3.Reflect(dir, hit.normal).normalized;
                    origin = hit.point + dir * SURFACE_OFFSET;
                    continue;
                }


                // If it's something in additionalHitMask but not goal/lens/blocker, stop.
                break;
            }
            else
            {
                // Nothing hit—draw to max distance and stop
                points[count++] = origin + dir * maxDistance;
                break;
            }
        }

        line.positionCount = count;
        for (int p = 0; p < count; p++)
            line.SetPosition(p, points[p]);
    }

    static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    /// <summary>Call this to restart the puzzle.</summary>
    public void ResetPuzzle()
    {
        completed = false;
        // (Optionally) re-center lenses, clear UI, etc.
    }


}
