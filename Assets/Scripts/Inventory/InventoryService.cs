using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryService : MonoBehaviour, IInventory
{
    public static InventoryService Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debug = true;

    private readonly Dictionary<int, int> _counts = new();
    public event Action<IDictionary<int, int>, IReadOnlyDictionary<int, int>> OnChanged;
    public IReadOnlyDictionary<int, int> GetInventoryItems() => _counts;

    private InventoryItem _inventoryItem;
    private List<InventorySlot> _inventorySlots = new List<InventorySlot>();
    //private List<InventoryItem> _inventoryItems = new List<InventoryItem>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).TryGetComponent<InventorySlot>(out var slot))
                _inventorySlots.Add(slot);
        }
            Debug.Log("[InventoryService] Found slots: " + _inventorySlots.Count);
    }

    public int GetCount(int itemId) =>
        _counts.TryGetValue(itemId, out var n) ? n : 0;

    private void Add(int itemId, int qty)
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

        foreach (var kv in change)
        {
            var have = GetCount(kv.Key);
            var delta = kv.Value;
            if (delta < 0 && have + delta < 0)
            {
                if (debug) Debug.LogWarning($"[InventoryService] Not enough '{kv.Key}' (have {have}, need {-delta}).");
                return false;
            }
        }

        //
        foreach (var kv in change)
        {
            var newCount = GetCount(kv.Key) + kv.Value;
            if (newCount <= 0) _counts.Remove(kv.Key);
            else _counts[kv.Key] = newCount;

            if (debug) Debug.Log($"[InventoryService] {kv.Key}: {(kv.Value >= 0 ? "+" : "")}{kv.Value} {GetCount(kv.Key)}");
        }

        OnChanged?.Invoke(change, _counts);
        return true;
    }

    public void AddOrStack(MaterialData mat, int amount)
    {
        if (mat == null || amount <= 0) return;

        // Try stack into existing slot with same material
        foreach (var slot in _inventorySlots)
        {
            if (!slot.IsEmpty && slot.Item == mat)
            {
                slot.AddAmount(amount);
                TryApply(new Dictionary<int, int> { { mat.id, amount } });
                return;
            }
        }

        // Find an empty slot
        foreach (var slot in _inventorySlots)
        {
            if (slot.IsEmpty)
            {
                slot.SetItem(mat, amount);
                TryApply(new Dictionary<int, int> { { mat.id, amount } });
                return;
            }
        }

        if (debug) Debug.LogWarning("[InventoryService] No free inventory slots available.");
    }
}
