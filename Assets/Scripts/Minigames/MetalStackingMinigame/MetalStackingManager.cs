using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MetalStackingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject clawObject;
    [SerializeField] private GameObject metalStackPrefab;
    [SerializeField] private MetalStackingEvaluator evaluator;

    [Header("Spawn/Drop")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.3f, 0f);
    [SerializeField] private float respawnDelay = 0.15f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI layerCountText;
    [SerializeField] private TextMeshProUGUI targetLayerText;

    private Transform clawTransform;
    private GameObject currentPiece;
    private MetalStack currentStack;

    private GameObject lastPiece;
    private bool isHolding;
    private int level;
    private bool completed;

    private readonly Dictionary<MetalStack, Action<MetalStack>> _stuckHandlers = new Dictionary<MetalStack, Action<MetalStack>>();
    private bool _queuedDrop;

    private void Awake()
    {
        if (clawObject != null)
        {
            clawTransform = clawObject.transform;
        }
        targetLayerText.text = evaluator != null ? evaluator.targetLayers.ToString() : "N/A";
    }

    private void Start()
    {
        SpawnAndAttach();
    }

    private void Update()
    {
        // Hard gate: ignore input entirely if finished or completed
        if (completed || (evaluator != null && evaluator.IsFinished))
        {
            _queuedDrop = false; // purge any buffered click
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
        // Do not spawn when finished
        if (completed || (evaluator != null && evaluator.IsFinished))
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
        // Do not release when finished
        if (completed || (evaluator != null && evaluator.IsFinished))
        {
            return;
        }

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
        if (piece != null && _stuckHandlers.TryGetValue(piece, out var handler))
        {
            piece.StuckTo -= handler;
            _stuckHandlers.Remove(piece);
        }

        if (completed || (evaluator != null && evaluator.IsFinished))
        {
            return;
        }

        bool isFirstLayer = level == 0;
        if (isFirstLayer)
        {
            level++;
            layerCountText.text = level.ToString();
            lastPiece = piece != null ? piece.gameObject : null;
            if (ReferenceEquals(currentStack, piece))
            {
                currentPiece = null;
                currentStack = null;
            }

            evaluator?.OnLayerCompleted(level);
            // If evaluator finished due to reaching target, mark completed to stop any pending actions
            if (evaluator != null && evaluator.IsFinished)
            {
                completed = true;
                _queuedDrop = false;
            }
            return;
        }

        GameObject stuckToGO = stuckTo != null ? stuckTo.gameObject : null;
        bool stuckToLast = stuckToGO != null && ReferenceEquals(stuckToGO, lastPiece);

        if (!stuckToLast)
        {
            completed = true;
            _queuedDrop = false;
            evaluator?.Fail();
            return;
        }

        level++;
        layerCountText.text = level.ToString();
        lastPiece = piece != null ? piece.gameObject : lastPiece;
        if (ReferenceEquals(currentStack, piece))
        {
            currentPiece = null;
            currentStack = null;
        }

        evaluator?.OnLayerCompleted(level);
        if (evaluator != null && evaluator.IsFinished)
        {
            completed = true;
            _queuedDrop = false;
        }
    }

    public void ResetMinigame()
    {
        // Cancel any pending spawns
        CancelInvoke(nameof(SpawnAndAttach));

        // Unsubscribe all handlers to avoid dangling refs
        foreach (var kvp in _stuckHandlers)
        {
            var stack = kvp.Key;
            var handler = kvp.Value;
            if (stack != null)
            {
                stack.StuckTo -= handler;
            }
        }
        _stuckHandlers.Clear();

        // Destroy all stacks in the scene
        var allStacks = FindObjectsOfType<MetalStack>(true);
        for (int i = 0; i < allStacks.Length; i++)
        {
            var stack = allStacks[i];
            if (stack != null)
            {
                var go = stack.gameObject;
                if (go != null)
                {
                    Destroy(go);
                }
            }
        }

        // Clear state
        currentPiece = null;
        currentStack = null;
        lastPiece = null;
        isHolding = false;
        _queuedDrop = false;
        completed = false;

        // Reset level and UI
        level = 0;
        if (layerCountText != null)
        {
            layerCountText.text = "0";
        }

        // Reset evaluator run state
        if(evaluator != null) evaluator.ResetRun();

        // Respawn new piece to start fresh
        SpawnAndAttach();
    }
}