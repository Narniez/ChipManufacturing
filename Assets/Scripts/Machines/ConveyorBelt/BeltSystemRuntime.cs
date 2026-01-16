using System.Collections.Generic;
using UnityEngine;
using ProceduralMusic;

public class BeltSystemRuntime : MonoBehaviour
{
    public static BeltSystemRuntime Instance { get; private set; }

    [SerializeField] private float itemMoveDuration = 0.2f;
    [Tooltip("Set to true if there are any conveyor belts already in the scene.")]
    [SerializeField] private bool registerBeltsOnStart = false;
    public float ItemMoveDuration => itemMoveDuration;

    private readonly List<ConveyorBelt> _belts = new List<ConveyorBelt>();

    //  GC-optimised buffers
    private readonly Queue<int> _queue = new Queue<int>(256);
    private bool[] _enqueued = new bool[256];
    private readonly Dictionary<ConveyorBelt, int> _index = new Dictionary<ConveyorBelt, int>(256);

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
        ProceduralMusicManager.OnClockTick_Belts += HandleClockTick;
    }

    private void OnDisable()
    {
        ProceduralMusicManager.OnClockTick_Belts -= HandleClockTick;
    }

    // Called on each clock tick; attempts moves for all belts that are not currently animating.
    private void HandleClockTick()
    {
        if (GameStateService.IsLoading) return;

        int n = _belts.Count;
        if (n == 0) return;

        EnsureBuffers(n);

        _queue.Clear();
        System.Array.Clear(_enqueued, 0, n);

        // enqueuing belts that can move
        for (int i = 0; i < n; i++)
        {
            var b = _belts[i];
            if (b == null) continue;
            if (b.HasItem && !b.IsItemAnimating())
            {
                _queue.Enqueue(i);
                _enqueued[i] = true;
            }
        }

        // Process queue: when a belt moves forward, its predecessor may become eligible,
        // so enqueue predecessor to propagate chain moves within same tick.
        while (_queue.Count > 0)
        {
            int idx = _queue.Dequeue();
            _enqueued[idx] = false;

            var belt = _belts[idx];
            if (belt == null) continue;
            if (!belt.HasItem || belt.IsItemAnimating()) continue;

            bool moved;
            try { moved = belt.TickMoveAttempt(); }
            catch (System.Exception ex)
            {
                Debug.LogError($"Belt TickMoveAttempt exception on {belt.name}: {ex}");
                moved = false;
            }

            if (moved)
            {
                var prev = belt.PreviousInChain;
                if (prev != null && _index.TryGetValue(prev, out int prevIdx))
                {
                    if (prevIdx >= 0 && prevIdx < n && !_enqueued[prevIdx] && prev.HasItem && !prev.IsItemAnimating())
                    {
                        _queue.Enqueue(prevIdx);
                        _enqueued[prevIdx] = true;
                    }
                }
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
            var b = _belts[i];
            if (b == null) continue;

            var item = b.PeekItem();
            if (item == null) continue;

            item.Animate(dt);
        }
    }

    private void EnsureBuffers(int n)
    {
        if (_enqueued == null || _enqueued.Length < n)
            _enqueued = new bool[Mathf.NextPowerOfTwo(n)];
    }


    public void Register(ConveyorBelt belt)
    {
        if (belt == null) return;
        if (_index.ContainsKey(belt)) return;

        int idx = _belts.Count;
        _belts.Add(belt);
        _index[belt] = idx;
    }

    public void Unregister(ConveyorBelt belt)
    {
        if (belt == null) return;
        if (!_index.TryGetValue(belt, out int idx)) return;

        int last = _belts.Count - 1;
        if (last < 0)
        {
            // List is already empty; just drop the index entry.
            _index.Remove(belt);
            return;
        }

        if (idx < 0 || idx > last)
        {
            // Index is stale/out of sync; remove mapping and rebuild.
            _index.Remove(belt);
            RebuildIndexMap();
            return;
        }

        var lastBelt = _belts[last];

        // swap-remove to keep list compact, update index map
        _belts[idx] = lastBelt;
        _belts.RemoveAt(last);

        _index.Remove(belt);
        if (lastBelt != null && !ReferenceEquals(lastBelt, belt))
            _index[lastBelt] = idx;
    }

    private void RebuildIndexMap()
    {
        _index.Clear();
        for (int i = 0; i < _belts.Count; i++)
        {
            var b = _belts[i];
            if (b != null)
                _index[b] = i;
        }
    }
}