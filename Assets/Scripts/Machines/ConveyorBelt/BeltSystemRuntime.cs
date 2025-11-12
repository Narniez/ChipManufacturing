using System.Collections.Generic;
using UnityEngine;

public class BeltSystemRuntime : MonoBehaviour
{
    public static BeltSystemRuntime Instance { get; private set; }

    [SerializeField] private float itemMoveDuration = 0.2f;
    public float ItemMoveDuration => itemMoveDuration;

    private readonly List<ConveyorBelt> _belts = new List<ConveyorBelt>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < belts.Length; i++)
            Register(belts[i]);
    }

    private void Update()
    {
        // Animate every frame
        AnimateItemVisuals(Time.deltaTime);
        // Continuous movement: attempt moves immediately when item not animating
        for (int i = _belts.Count - 1; i >= 0; i--)
        {
            var b = _belts[i];
            if (b == null) continue;
            if (b.HasItem && !b.IsItemAnimating())
                b.TickMoveAttempt();
        }
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
        if (!_belts.Contains(belt)) _belts.Add(belt);
    }

    public void Unregister(ConveyorBelt belt)
    {
        _belts.Remove(belt);
    }
}