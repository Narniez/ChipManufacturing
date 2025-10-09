using System.Collections.Generic;
using UnityEngine;

public class BeltSystemRuntime : MonoBehaviour
{
    public static BeltSystemRuntime Instance { get; private set; }

    [SerializeField] private float tickInterval = 0.25f; // 4 ticks/sec
    private float _accum;

    private readonly List<ConveyorBelt> _belts = new List<ConveyorBelt>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        _accum += Time.deltaTime;
        if (_accum >= tickInterval)
        {
            _accum -= tickInterval;
            Tick();
        }
    }

    private void Tick()
    {
        // Reverse order so downstream belts try moving first (prevent overwrite)
        for (int i = _belts.Count - 1; i >= 0; i--)
        {
            _belts[i].TickMoveAttempt();
        }
    }

    public void Register(ConveyorBelt belt)
    {
        if (!_belts.Contains(belt))
            _belts.Add(belt);
    }

    public void Unregister(ConveyorBelt belt)
    {
        _belts.Remove(belt);
    }
}