using System.Collections.Generic;
using UnityEngine;

public class BeltSystemRuntime : MonoBehaviour
{
    public static BeltSystemRuntime Instance { get; private set; }

    [SerializeField] private float itemMoveDuration = 0.2f;
    [Tooltip("Set to true if there are any conveyor belts already in the scene.")]
    [SerializeField] private bool registerBeltsOnStart = false;
    public float ItemMoveDuration => itemMoveDuration;

    private readonly List<ConveyorBelt> _belts = new List<ConveyorBelt>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (registerBeltsOnStart)
        {
            var belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < belts.Length; i++)
                Register(belts[i]);
        }
    }

    private void OnEnable()
    {
        AudioManager.OnClockTick_Belts += HandleClockTick;
    }

    private void OnDisable()
    {
        AudioManager.OnClockTick_Belts -= HandleClockTick;
    }

    // Called on each clock tick; attempts moves for all belts that are not currently animating.
    private void HandleClockTick()
    {
        if (GameStateService.IsLoading) return;

        int n = _belts.Count;
        if (n == 0) return;

        // Build initial queue of indices for belts that currently have a non-animating item.
        var queue = new System.Collections.Generic.Queue<int>(Mathf.Max(16, n));
        var enqueued = new bool[n]; // mark to avoid duplicate enqueue

        for (int i = 0; i < n; i++)
        {
            var b = _belts[i];
            if (b == null) continue;
            if (b.HasItem && !b.IsItemAnimating())
            {
                queue.Enqueue(i);
                enqueued[i] = true;
            }
        }

        // Process queue: when a belt moves forward, its predecessor may become eligible,
        // so enqueue predecessor to propagate chain moves within same tick.
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            enqueued[idx] = false;

            var belt = _belts[idx];
            if (belt == null) continue;
            if (!belt.HasItem || belt.IsItemAnimating()) continue;

            bool moved = false;
            try
            {
                moved = belt.TickMoveAttempt();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Belt TickMoveAttempt exception on {belt.name}: {ex}");
                // skip this belt further this tick
                moved = false;
            }

            if (moved)
            {
                // If this belt moved an item forward, its predecessor (if any) may now be able to move.
                var prev = belt.PreviousInChain;
                if (prev != null)
                {
                    int prevIdx = _belts.IndexOf(prev);
                    if (prevIdx >= 0 && !enqueued[prevIdx] && prev.HasItem && !prev.IsItemAnimating())
                    {
                        queue.Enqueue(prevIdx);
                        enqueued[prevIdx] = true;
                    }
                }
                else
                {
                    // If not linked by chain, consider scanning nearby belts (optional)
                }
            }
            else
            {
                // If couldn't move now, it might become able later in the same tick after others move;
                // but to avoid busy looping we rely on predecessors enqueuing successors when they move.
                // Optionally re-enqueue after some other moves if desired (omitted for perf).
            }
        }
    }

    private void Update()
    {
        // Pause belt processing while loading to avoid items moving during restore
        if (GameStateService.IsLoading) return;

        // Animate every frame
        AnimateItemVisuals(Time.deltaTime);
    }

    private void AnimateItemVisuals(float dt)
    {
        for (int i = 0; i < _belts.Count; i++)
        {
            var item = _belts[i].PeekItem();
            if (item == null) continue;
            item.Animate(dt);
        }
    }

    public void Register(ConveyorBelt belt)
    {
        if (belt == null) return;
        if (!_belts.Contains(belt))
        {
            _belts.Add(belt);
        }
    }

    public void Unregister(ConveyorBelt belt)
    {
        _belts.Remove(belt);
    }
}