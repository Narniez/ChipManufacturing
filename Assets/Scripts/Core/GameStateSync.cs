using UnityEngine;

public static class GameStateSync
{
    // Prefer registry-based id (stable in builds). Fallbacks keep previous behavior.
    private static string ResolveAssetId(Object asset)
    {
        if (asset == null) return string.Empty;

        // Try registry in Resources (works in Editor + builds)
        var registry = Resources.Load<DataRegistry>("DataRegistry");
        if (registry != null)
        {
            // Match by exact ScriptableObject reference first (preferred stable id).
            foreach (var me in registry.machines)
            {
                if (me.data == asset)
                    return me.id;
            }
            foreach (var ma in registry.materials)
            {
                if (ma.data == asset)
                    return ma.id;
            }

            // Fallback: if registry contains an entry whose id or data.name matches the asset name, use that id/name.
            var byName = registry.GetMachine(asset.name);
            if (byName != null) return asset.name;

            var matByName = registry.GetMaterial(asset.name);
            if (matByName != null) return asset.name;
        }

        // Runtime fallback: use asset name (ScriptableObject name) so builds remain deterministic.
        return (asset as ScriptableObject)?.name ?? string.Empty;
    }

    public static void TryAddOrUpdateMachine(Machine m)
    {
        var svc = GameStateService.Instance;
        if (svc == null || m == null) return;

        var list = svc.State.machines;
        var entry = list.Find(x => x.anchor == m.Anchor);
        if (entry == null)
        {
            list.Add(new MachineState
            {
                machineDataPath = m.Data != null ? ResolveAssetId(m.Data) : string.Empty,
                anchor = m.Anchor,
                orientation = m.Orientation,
                isBroken = m.IsBroken
            });
        }
        else
        {
            if (m.Data != null && string.IsNullOrEmpty(entry.machineDataPath))
                entry.machineDataPath = ResolveAssetId(m.Data);
            entry.orientation = m.Orientation;
            entry.isBroken = m.IsBroken;
        }
        GameStateService.MarkDirty();
    }

    public static void TryUpdateMachineOrientation(Machine m)
    {
        var svc = GameStateService.Instance;
        if (svc == null || m == null) return;
        var entry = svc.State.machines.Find(x => x.anchor == m.Anchor);
        if (entry != null)
        {
            entry.orientation = m.Orientation;
            GameStateService.MarkDirty();
        }
    }

    public static void TrySetMachineBroken(Machine m, bool broken)
    {
        var svc = GameStateService.Instance;
        if (svc == null || m == null) return;
        var entry = svc.State.machines.Find(x => x.anchor == m.Anchor);
        if (entry != null)
        {
            entry.isBroken = broken;
            GameStateService.MarkDirty();
        }
    }

    public static void TryRemoveMachine(Machine m)
    {
        var svc = GameStateService.Instance;
        if (svc == null || m == null) return;
        svc.State.machines.RemoveAll(x => x.anchor == m.Anchor);
        GameStateService.MarkDirty();
    }

    public static void TryAddOrUpdateBelt(ConveyorBelt b)
    {
        var svc = GameStateService.Instance;
        if (svc == null || b == null) return;

        // if belt has an item, capture its material key + amount (amount = 1 for single item)
        string itemKey = string.Empty;
        int itemAmount = 0;
        var item = b.PeekItem();
        if (item != null && item.materialData != null)
        {
            itemKey = ResolveAssetId(item.materialData);
            itemAmount = 1;
        }

        // Defensive check: avoid recording belt state before the belt is actually registered
        // in the GridService at the expected anchor — but only skip when there's nothing to save.
        var grid = Object.FindFirstObjectByType<GridService>();
        if (grid != null && grid.HasGrid)
        {
            if (!grid.TryGetCell(b.Anchor, out var cell) || cell.occupant == null)
            {
                if (string.IsNullOrEmpty(itemKey))
                {
                    // No occupant and nothing to save -> skip to avoid orphan entries
                    Debug.LogWarning($"GameStateSync: skipping belt save for anchor {b.Anchor} (GridService has no occupant and no item).");
                    return;
                }
                else
                {
                    // No occupant but we *have* an item to persist: allow write but warn
                    Debug.LogWarning($"GameStateSync: Grid missing occupant at {b.Anchor} but belt has item; persisting item state anyway.");
                }
            }
            else
            {
                // Validate that the occupant actually points to the same belt gameobject
                GameObject occGO = cell.occupant as GameObject ?? (cell.occupant as Component)?.gameObject;
                if (occGO != b.gameObject)
                {
                    if (string.IsNullOrEmpty(itemKey))
                    {
                        Debug.LogWarning($"GameStateSync: skipping belt save for anchor {b.Anchor} (occupant mismatch and no item).");
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"GameStateSync: occupant mismatch at {b.Anchor} but belt has item; persisting item state anyway.");
                    }
                }
            }
        }
        // If GridService isn't available yet, allow the write — it may be added later.
        var list = svc.State.belts;
        var entryIndex = list.FindIndex(x => x.anchor == b.Anchor);
        int turnKind = (int)(b.IsCorner ? b.TurnKind : ConveyorBelt.BeltTurnKind.None);

        if (entryIndex < 0)
        {
            list.Add(new BeltState
            {
                anchor = b.Anchor,
                orientation = b.Orientation,
                isTurn = b.IsTurnPrefab,
                turnKind = turnKind,
                itemMaterialKey = itemKey,
                itemAmount = itemAmount
            });
        }
        else
        {
            var e = list[entryIndex];
            e.orientation = b.Orientation;
            e.isTurn = b.IsTurnPrefab;
            e.turnKind = turnKind;
            e.itemMaterialKey = itemKey;
            e.itemAmount = itemAmount;
            list[entryIndex] = e;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(itemKey))
            Debug.Log($"GameStateSync: saved belt[{b.Anchor}] itemKey='{itemKey}'");
#endif

        GameStateService.MarkDirty();
    }

    public static void TryUpdateBeltOrientation(ConveyorBelt b)
    {
        var svc = GameStateService.Instance;
        if (svc == null || b == null) return;
        var idx = svc.State.belts.FindIndex(x => x.anchor == b.Anchor);
        if (idx >= 0)
        {
            var e = svc.State.belts[idx];
            e.orientation = b.Orientation;
            e.isTurn = b.IsTurnPrefab;
            e.turnKind = (int)(b.IsCorner ? b.TurnKind : ConveyorBelt.BeltTurnKind.None);
            // update item presence if any
            var item = b.PeekItem();
            e.itemMaterialKey = item != null && item.materialData != null ? ResolveAssetId(item.materialData) : string.Empty;
            e.itemAmount = item != null ? 1 : 0;
            svc.State.belts[idx] = e;
            GameStateService.MarkDirty();
        }
    }

    public static void TryReplaceBelt(ConveyorBelt oldBelt, ConveyorBelt newBelt)
    {
        var svc = GameStateService.Instance;
        if (svc == null || oldBelt == null || newBelt == null) return;
        svc.State.belts.RemoveAll(x => x.anchor == oldBelt.Anchor);
        TryAddOrUpdateBelt(newBelt);
    }

    public static void TryRemoveBelt(ConveyorBelt b)
    {
        var svc = GameStateService.Instance;
        if (svc == null || b == null) return;
        svc.State.belts.RemoveAll(x => x.anchor == b.Anchor);
        GameStateService.MarkDirty();
    }
}