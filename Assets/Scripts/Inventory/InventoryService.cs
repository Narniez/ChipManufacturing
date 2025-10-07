using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryService : IInventory
{
    public static InventoryService Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debug = true;

    private readonly Dictionary<int, int> _counts = new();
    public event Action<IDictionary<int, int>, IReadOnlyDictionary<int, int>> OnChanged;
    public IReadOnlyDictionary<int, int> GetInventoryItems() => _counts;

    public int GetCount(int itemId) =>
        _counts.TryGetValue(itemId, out var n) ? n : 0;

    public void Add(int itemId, int qty)
    {
        if (qty <= 0) return;
        TryApply(new Dictionary<int, int> { { itemId, qty } });
    }

    public bool TryRemove(int itemId, int qty)
    {
        if (qty <= 0) return false;
        return TryApply(new Dictionary<int, int> { { itemId, -qty } });
    }

    public bool TryApply(IDictionary<int, int> change)
    {
        if (change == null || change.Count == 0) return true;

        //check if we have enough items
        foreach (var kv in change)
        {
            var have = GetCount(kv.Key);
            var delta = kv.Value;
            if (delta < 0 && have + delta < 0)
            {
                if (debug) Debug.LogWarning($"[Inventory] Not enough '{kv.Key}' (have {have}, need {-delta}).");
                return false;
            }
        }

        //
        foreach (var kv in change)
        {
            var newCount = GetCount(kv.Key) + kv.Value;
            if (newCount <= 0) _counts.Remove(kv.Key);
            else _counts[kv.Key] = newCount;

            if (debug) Debug.Log($"[Inventory] {kv.Key}: {(kv.Value >= 0 ? "+" : "")}{kv.Value} {GetCount(kv.Key)}");
        }

        OnChanged?.Invoke(change, _counts);
        return true;
    }

}
