using UnityEngine;
using UnityEngine.Events;

public class MetalStackingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject clawObject;
    [SerializeField] private GameObject metalStackPrefab;

    [Header("Spawn/Drop")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.3f, 0f);
    [SerializeField] private float respawnDelay = 0.15f;

    private Transform clawTransform;
    private GameObject currentPiece;
    private GameObject lastPiece;

    private MetalStack currentStack;
    private bool isHolding;

    [Header("Completion")]
    private UnityEvent onPuzzleComplete;

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
        if (isHolding && Input.GetMouseButtonDown(0))
        {
            ReleaseCurrent();
        }
        if (completed)
        {
            return;
        }
    }

    private void SpawnAndAttach()
    {
        if (metalStackPrefab == null || clawTransform == null)
        {
            Debug.LogWarning("MetalStackingManager: Assign both metalStackPrefab and clawTransform in the Inspector.");
            return;
        }

        currentPiece = Instantiate(metalStackPrefab, clawTransform);
        currentStack = currentPiece.GetComponent<MetalStack>();
        if (currentStack == null)
        {
            currentStack = currentPiece.AddComponent<MetalStack>();
        }

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

        Invoke(nameof(SpawnAndAttach), respawnDelay);

        lastPiece = currentPiece;
        currentPiece = null;
    }
    
}