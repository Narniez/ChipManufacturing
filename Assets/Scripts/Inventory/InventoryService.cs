using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryService : MonoBehaviour, IInventory
{
    public static InventoryService Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debug = true;

    [Header("Inventory Settings")]
    [SerializeField] private GameObject slotRootGO;

    private readonly Dictionary<int, int> _counts = new();
    public event Action<IDictionary<int, int>, IReadOnlyDictionary<int, int>> OnChanged;
    public IReadOnlyDictionary<int, int> GetInventoryItems() => _counts;

    private List<InventorySlot> _inventorySlots = new List<InventorySlot>();

    // runtime cache
    private Dictionary<int, MaterialData> _materialById;

    // while loading we don't want to mark dirty or re-save repeatedly
    private bool _suppressSave;

    // expose loading/suppress flag so UI slots can avoid double-applying deltas
    public bool IsLoading => _suppressSave;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (slotRootGO == null)
            Debug.LogError("[InventoryService] slotRootGO not assigned in inspector.");

        int slotCount = slotRootGO.transform.childCount;
        for (int i = 0; i < slotCount; i++)
        {
            var inventorySlot = slotRootGO.transform.GetChild(i).GetComponent<InventorySlot>();
            if (inventorySlot != null) _inventorySlots.Add(inventorySlot);
        }
        Debug.Log("[InventoryService] Found slots: " + _inventorySlots.Count);

        // persist snapshot to GameState on changes (single place)
        OnChanged += (delta, all) =>
        {
            if (_suppressSave) return;
            if (GameStateService.Instance != null && GameStateService.Instance.State != null)
            {
                GameStateService.Instance.State.inventory = ExportState();
                GameStateService.MarkDirty();
            }
        };
    }

    private void Start()
    {
        try { GameServices.Init(this); } catch { /* ignore */ }

        // building material caches by id
        EnsureMaterialLookup();

        // restoring saved inventory
        if (GameStateService.Instance != null && GameStateService.Instance.State != null && GameStateService.Instance.State.inventory != null)
        {
            StartCoroutine(DeferredLoadState());
        }
    }

    private System.Collections.IEnumerator DeferredLoadState()
    {
        yield return null; // wait one frame for all OnEnable() subscriptions
        LoadState(GameStateService.Instance.State.inventory);
    }

    public int GetCount(int itemId) =>
        _counts.TryGetValue(itemId, out var n) ? n : 0;


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

        // trying to stack into existing slot with same material
        foreach (var slot in _inventorySlots)
        {
            if (!slot.IsEmpty && slot.Item == mat)
            {
                slot.AddAmount(amount);
                TryApply(new Dictionary<int, int> { { mat.id, amount } });
                return;
            }
        }

        // finding an empty slot
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

    // ----- Persistence helpers -----

    // Exports exact per-slot snapshot
    public InventoryState ExportState()
    {
        var state = new InventoryState();
        state.items = new List<InventoryEntry>();

        for (int i = 0; i < _inventorySlots.Count; i++)
        {
            var slot = _inventorySlots[i];
            if (slot != null && !slot.IsEmpty && slot.Item != null && slot.Amount > 0)
            {
                var entry = new InventoryEntry
                {
                    slotIndex = i,
                    materialId = slot.Item.id,
                    amount = slot.Amount
                };
                state.items.Add(entry);
            }
        }

        return state;
    }

    // Restores the exact layout by slotIndex => SetItem
    public void LoadState(InventoryState state)
    {
        if (state == null) return;

        EnsureMaterialLookup();

        _suppressSave = true;

        // snapshots current slots so we can correctly maintain _counts while overriding specific slots.
        int slotCount = _inventorySlots.Count;
        var prev = new (int materialId, int amount)?[slotCount];

        _counts.Clear();
        for (int i = 0; i < slotCount; i++)
        {
            var s = _inventorySlots[i];
            if (s != null && !s.IsEmpty && s.Item != null && s.Amount > 0)
            {
                prev[i] = (s.Item.id, s.Amount);
                _counts[s.Item.id] = _counts.TryGetValue(s.Item.id, out var cur) ? cur + s.Amount : s.Amount;
            }
            else
            {
                prev[i] = null;
            }
        }

        var pending = new List<InventoryEntry>();

        // entries with valid slotIndex override that slot
        foreach (var entry in state.items)
        {
            MaterialData mat = null;
            if (entry.materialId != 0) mat = FindMaterialById(entry.materialId);
            if (mat == null)
            {
                if (debug) Debug.LogWarning($"InventoryService.LoadState: missing material for entry (id={entry.materialId}).");
                continue;
            }

            int idx = entry.slotIndex;
            if (idx >= 0 && idx < slotCount)
            {
                // subtracting previous content of this slot (if any)
                if (prev[idx] != null)
                {
                    var (prevId, prevAmt) = prev[idx].Value;
                    if (_counts.TryGetValue(prevId, out var prevTotal))
                    {
                        prevTotal -= prevAmt;
                        if (prevTotal <= 0) _counts.Remove(prevId);
                        else _counts[prevId] = prevTotal;
                    }
                }

                var slot = _inventorySlots[idx];
                slot.SetItem(mat, Math.Max(0, entry.amount));
                _counts[mat.id] = _counts.TryGetValue(mat.id, out var cur) ? cur + entry.amount : entry.amount;

                prev[idx] = (mat.id, Math.Max(0, entry.amount));
            }
            else
            {
                pending.Add(entry);
            }
        }

        // stacking into same-material slots, else put in first empty slot
        foreach (var e in pending)
        {
            MaterialData mat = null;
            if (e.materialId != 0) mat = FindMaterialById(e.materialId);
            if (mat == null) continue;
            int amt = Math.Max(0, e.amount);
            if (amt <= 0) continue;

            bool stacked = false;
            for (int i = 0; i < _inventorySlots.Count; i++)
            {
                var slot = _inventorySlots[i];
                if (!slot.IsEmpty && slot.Item == mat)
                {
                    slot.AddAmount(amt);
                    _counts[mat.id] = _counts.TryGetValue(mat.id, out var cur) ? cur + amt : amt;
                    stacked = true;
                    break;
                }
            }
            if (stacked) continue;

            bool placed = false;
            for (int i = 0; i < _inventorySlots.Count; i++)
            {
                var slot = _inventorySlots[i];
                if (slot.IsEmpty)
                {
                    slot.SetItem(mat, amt);
                    _counts[mat.id] = _counts.TryGetValue(mat.id, out var cur) ? cur + amt : amt;
                    placed = true;
                    break;
                }
            }
            if (!placed && debug)
            {
                Debug.LogWarning("[InventoryService] Not enough UI slots to restore inventory for " + (mat != null ? mat.name : $"id={e.materialId}"));
            }
        }

        // notifying listeners with the resulting counts
        OnChanged?.Invoke(null, _counts);

        _suppressSave = false;
    }

    // Builds a lookup of materials by id without using MaterialRegistry or MaterialType
    private void EnsureMaterialLookup()
    {
        if (_materialById != null && _materialById.Count > 0) return;

        _materialById = new Dictionary<int, MaterialData>();

        // gathering from current UI slots (runtime-safe)
        foreach (var slot in _inventorySlots)
        {
            if (slot != null && !slot.IsEmpty && slot.Item != null)
            {
                var m = slot.Item;
                if (!_materialById.ContainsKey(m.id)) _materialById[m.id] = m;
            }
        }

        // editor-only: index all MaterialData assets in project (for reliable load in editor)
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets("t:MaterialData");
        foreach (var g in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<MaterialData>(path);
            if (m == null) continue;
            if (!_materialById.ContainsKey(m.id)) _materialById[m.id] = m;
        }
#endif
    }

    private MaterialData FindMaterialById(int id)
    {
        EnsureMaterialLookup();
        return (_materialById != null && _materialById.TryGetValue(id, out var material)) ? material : null;
    }
}
