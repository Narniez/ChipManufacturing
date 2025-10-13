using System.Collections.Generic;
using UnityEngine;

public class BeltSystemRuntime : MonoBehaviour
{
    public static BeltSystemRuntime Instance { get; private set; }

    [SerializeField] private float tickInterval = 0.25f; // 4 ticks/sec
    [SerializeField, Tooltip("Units per second for item visuals to slide between belts")]
    private float itemMoveSpeed = 3.0f;

    private float _accum;

    private readonly List<ConveyorBelt> _belts = new List<ConveyorBelt>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Safety: auto-register any belts already in scene (in case they enabled before Instance existed)
        var belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < belts.Length; i++)
            Register(belts[i]);
    }

    private void Update()
    {
        // Tick logic (discrete cell-to-cell movement)
        _accum += Time.deltaTime;
        if (_accum >= tickInterval)
        {
            _accum -= tickInterval;
            Tick();
        }

        // Smoothly animate item visuals toward their belt centers
        AnimateItemVisuals(Time.deltaTime);
    }

    private void Tick()
    {
        for (int i = _belts.Count - 1; i >= 0; i--)
            _belts[i].TickMoveAttempt();
    }

    private void AnimateItemVisuals(float dt)
    {
        if (_belts.Count == 0) return;

        for (int i = 0; i < _belts.Count; i++)
        {
            var belt = _belts[i];
            var item = belt.PeekItem();
            if (item?.Visual == null) continue;

            var target = belt.GetWorldCenter();
            var current = item.Visual.transform.position;

            // Move towards target at constant speed
            var next = Vector3.MoveTowards(current, target, itemMoveSpeed * dt);
            item.Visual.transform.position = next;
        }
    }

    public void Register(ConveyorBelt belt)
    {
        if (belt == null) return;
        if (!_belts.Contains(belt))
            _belts.Add(belt);
    }

    public void Unregister(ConveyorBelt belt)
    {
        _belts.Remove(belt);
    }
}