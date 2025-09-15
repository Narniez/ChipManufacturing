using System.Collections.Generic;
using UnityEngine;

public class InventoryService : IInventory
{
    [Header("Debug")]
    [SerializeField] private bool debug = true;


    private readonly Dictionary<string, int> _counts = new();

    public int GetCount(string itemId) =>
        _counts.TryGetValue(itemId, out var n) ? n : 0;

    public void Add(string itemId, int qty)
    {
        if (qty <= 0) return;
        _counts[itemId] = GetCount(itemId) + qty;
        if (debug) Debug.Log($"[Inventory] +{qty} {itemId} (total {GetCount(itemId)})");
    }

    public bool TryRemove(string itemId, int qty)
    {
        if (qty <= 0 || GetCount(itemId) < qty) return false;

        var left = GetCount(itemId) - qty;
        if (left > 0) _counts[itemId] = left;
        else _counts.Remove(itemId);

        if (debug) Debug.Log($"[Inventory] -{qty} {itemId} (left {GetCount(itemId)})");
        return true;
    }
}
