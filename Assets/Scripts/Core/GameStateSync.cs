using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

public static class GameStateSync
{
    // Editor/runtime helpers to derive an address/key for MachineData and MaterialData
    private static string ResolveAssetAddress(Object asset)
    {
        if (asset == null) return string.Empty;

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) return string.Empty;
        string guid = AssetDatabase.AssetPathToGUID(path);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            var entry = settings.FindAssetEntry(guid);
            if (entry != null && !string.IsNullOrEmpty(entry.address))
                return entry.address;
        }
        // fallback to GUID (stable if you set entry.address = guid) or asset name
        return guid;
#else
        // At runtime fallback to name (or saved address produced in editor)
        return (asset as ScriptableObject)?.name ?? string.Empty;
#endif
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
                machineDataPath = m.Data != null ? ResolveAssetAddress(m.Data) : string.Empty,
                anchor = m.Anchor,
                orientation = m.Orientation,
                isBroken = m.IsBroken
            });
        }
        else
        {
            if (m.Data != null && string.IsNullOrEmpty(entry.machineDataPath))
                entry.machineDataPath = ResolveAssetAddress(m.Data);
            entry.orientation = m.Orientation;
            entry.isBroken = m.IsBroken;
        }
        GameStateService.MarkDirty();
    }

    public static void ForceUpdateMachineDataPath(Machine m)
    {
        // Called after Initialize when machine.Data is guaranteed
        var svc = GameStateService.Instance;
        if (svc == null || m == null || m.Data == null) return;

        var list = svc.State.machines;
        var entry = list.Find(x => x.anchor == m.Anchor);
        string key = ResolveAssetAddress(m.Data);

        if (entry == null)
        {
            list.Add(new MachineState
            {
                machineDataPath = key,
                anchor = m.Anchor,
                orientation = m.Orientation,
                isBroken = m.IsBroken
            });
        }
        else
        {
            if (string.IsNullOrEmpty(entry.machineDataPath) || entry.machineDataPath != key)
                entry.machineDataPath = key;
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

        var list = svc.State.belts;
        var entryIndex = list.FindIndex(x => x.anchor == b.Anchor);
        int turnKind = (int)(b.IsCorner ? b.TurnKind : ConveyorBelt.BeltTurnKind.None);

        // if belt has an item, capture its material key + amount (amount = 1 for single item)
        string itemKey = string.Empty;
        int itemAmount = 0;
        var item = b.PeekItem();
        if (item != null && item.materialData != null)
        {
            itemKey = ResolveAssetAddress(item.materialData);
            itemAmount = 1;
        }

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
            e.itemMaterialKey = item != null && item.materialData != null ? ResolveAssetAddress(item.materialData) : string.Empty;
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