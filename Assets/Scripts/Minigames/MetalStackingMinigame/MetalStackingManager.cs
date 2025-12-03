using System;
using System.Collections.Generic;
using UnityEngine;

public class MetalStackingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject clawObject;
    [SerializeField] private GameObject metalStackPrefab;
    [SerializeField] private MetalStackingEvaluator evaluator; // handles win/fail and layer target

    [Header("Spawn/Drop")]
    [SerializeField] private Vector3 localOffset = new(0f, -0.3f, 0f);
    [SerializeField] private float respawnDelay = 0.15f;

    private Transform clawTransform;
    private GameObject currentPiece;
    private MetalStack currentStack;

    private GameObject lastPiece; // last successfully placed piece
    private readonly List<GameObject> _placedPieces = new List<GameObject>(); // optional tracking

    private bool isHolding;
    private int level; // number of successfully placed layers
    private bool completed;

    // Track per-piece event handlers to avoid unsubscribing the wrong instance
    private readonly Dictionary<MetalStack, Action<MetalStack>> _stuckHandlers = new Dictionary<MetalStack, Action<MetalStack>>();

    // Buffer clicks during respawn so user can click anytime
    private bool _queuedDrop;

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

        if (Input.GetMouseButtonDown(0))
        {
            if (isHolding)
            {
                ReleaseCurrent();
            }
            else
            {
                _queuedDrop = true;
            }
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

        // Per-piece handler
        Action<MetalStack> handler = stuckTo => OnPieceStuck(currentStack, stuckTo);
        _stuckHandlers[currentStack] = handler;
        currentStack.StuckTo += handler;

        currentStack.AttachToClaw(clawTransform, localOffset, clawObject);
        isHolding = true;

        if (_queuedDrop)
        {
            _queuedDrop = false;
            ReleaseCurrent();
        }
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
    }

    private void OnPieceStuck(MetalStack piece, MetalStack stuckTo)
    {
        // Unsubscribe safely
        if (piece != null && _stuckHandlers.TryGetValue(piece, out var handler))
        {
            piece.StuckTo -= handler;
            _stuckHandlers.Remove(piece);
        }

        if (completed) return;

        // First layer: accept any target, set as lastPiece
        bool isFirstLayer = level == 0;
        if (isFirstLayer)
        {
            level++;
            lastPiece = piece != null ? piece.gameObject : null;
            if (lastPiece != null) _placedPieces.Add(lastPiece);

            if (ReferenceEquals(currentStack, piece))
            {
                currentPiece = null;
                currentStack = null;
            }

            evaluator?.OnLayerCompleted(level);
            return;
        }

        // From second layer onwards: must stick to exactly the lastPiece
        GameObject stuckToGO = stuckTo != null ? stuckTo.gameObject : null;
        bool stuckToLast = stuckToGO != null && ReferenceEquals(stuckToGO, lastPiece);

        if (!stuckToLast)
        {
            completed = true;
            if (evaluator != null) evaluator.Fail();
            return;
        }

        // Success: promote current as lastPiece
        level++;
        lastPiece = piece != null ? piece.gameObject : lastPiece;
        if (lastPiece != null && (_placedPieces.Count == 0 || !ReferenceEquals(_placedPieces[_placedPieces.Count - 1], lastPiece)))
        {
            _placedPieces.Add(lastPiece);
        }

        if (ReferenceEquals(currentStack, piece))
        {
            currentPiece = null;
            currentStack = null;
        }

        if (evaluator != null) evaluator.OnLayerCompleted(level);
    }
}