using UnityEngine;

public class MetalStackingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject clawObject;
    [SerializeField] private GameObject metalStackPrefab;
    [SerializeField] private MetalStackingEvaluator evaluator; // handles win/fail and layer target

    [Header("Spawn/Drop")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.3f, 0f);
    [SerializeField] private float respawnDelay = 0.15f;

    [Header("Validation")]
    [SerializeField] private float yStackTolerance = 0.05f; // acceptable vertical adjacency (more forgiving)
    [SerializeField] private float minZOverlap = 0.001f;     // minimal Z overlap to count as stacked

    private Transform clawTransform;
    private GameObject currentPiece;
    private MetalStack currentStack;

    private GameObject lastPiece;
    private bool isHolding;
    private int level; // number of successfully placed layers
    private bool completed;

    private void Awake()
    {
        if (clawObject != null)
        {
            clawTransform = clawObject.transform;
        }
    }

    private void Start()
    {
        SpawnAndAttach();
    }

    private void Update()
    {
        if (completed)
        {
            return;
        }

        if (isHolding && Input.GetMouseButtonDown(0))
        {
            ReleaseCurrent();
        }
    }

    private void SpawnAndAttach()
    {
        if (completed)
        {
            return;
        }

        if (metalStackPrefab == null || clawTransform == null)
        {
            Debug.LogWarning("MetalStackingManager: Assign both metalStackPrefab and clawTransform in the Inspector.");
            return;
        }

        currentPiece = Instantiate(metalStackPrefab, clawTransform);
        if (lastPiece != null)
        {
            currentPiece.transform.localScale = lastPiece.transform.localScale;
        }

        currentStack = currentPiece.GetComponent<MetalStack>();
        if (currentStack == null)
        {
            currentStack = currentPiece.AddComponent<MetalStack>();
        }

        // Subscribe to stick event to validate collision target
        currentStack.StuckTo += OnCurrentStuckTo;

        // Pass the clawObject so MetalStack can ignore collisions with it
        currentStack.AttachToClaw(clawTransform, localOffset, clawObject);
        isHolding = true;
    }

    private void ReleaseCurrent()
    {
        if (currentPiece == null || currentStack == null)
        {
            return;
        }

        isHolding = false;

        currentStack.ReleaseFromClaw();

        // Respawn next piece with a delay
        Invoke(nameof(SpawnAndAttach), respawnDelay);

        lastPiece = currentPiece;
        currentPiece = null;
    }

    private void OnCurrentStuckTo(MetalStack stuckTo)
    {
        // Detach subscription from the piece once it has stuck
        if (currentStack != null)
        {
            currentStack.StuckTo -= OnCurrentStuckTo;
        }

        if (completed)
        {
            return;
        }

        // First layer always succeeds
        bool isFirstLayer = level == 0;
        if (isFirstLayer)
        {
            level++;
            if (evaluator != null)
            {
                evaluator.OnLayerCompleted(level);
            }
            return;
        }

        // From the second piece onward, validate overlap/stacking against lastPiece
        var lastStackTransform = lastPiece != null ? lastPiece.transform : null;
        var currentTransform = currentStack != null ? currentStack.transform : null;

        bool validStack = lastStackTransform != null &&
                          BoundsOverlapZAndStacked(currentTransform, lastStackTransform, yStackTolerance, minZOverlap);

        if (!validStack)
        {
            completed = true;
            if (evaluator != null)
            {
                evaluator.Fail();
            }
            else
            {
                Debug.Log("MetalStacking: Game Over (missed last piece).");
            }
            return;
        }

        // Count a completed layer
        level++;
        if (evaluator != null)
        {
            evaluator.OnLayerCompleted(level);
        }
    }

    private static bool BoundsOverlapZAndStacked(Transform top, Transform bottom, float yTolerance, float minZOverlap)
    {
        if (top == null || bottom == null) return false;

        if (!TryGetCombinedBounds(top, out var topBounds)) return false;
        if (!TryGetCombinedBounds(bottom, out var bottomBounds)) return false;

        // Top piece should sit at/above bottom's top face within tolerance
        float bottomTopY = bottomBounds.max.y;
        float topBottomY = topBounds.min.y;

        // Inclusive check allows tiny penetration/solver nudges
        bool verticallyStacked = topBottomY >= bottomTopY - yTolerance;
        if (!verticallyStacked) return false;

        // Z overlap length
        float zOverlap = Mathf.Max(0f, Mathf.Min(topBounds.max.z, bottomBounds.max.z) - Mathf.Max(topBounds.min.z, bottomBounds.min.z));
        return zOverlap >= minZOverlap;
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }

        var colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                if (colliders[i] != null) bounds.Encapsulate(colliders[i].bounds);
            }
            return true;
        }

        bounds = new Bounds();
        return false;
    }
}