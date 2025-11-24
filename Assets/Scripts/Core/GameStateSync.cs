using UnityEngine;

public static class GameStateSync
{
    // Utility: derive a Resources path or fallback to asset name 
    private static string GetMachineDataPath(MachineData data)
    {
        if (data == null) return string.Empty;
        // If stored under Resources, you can compute relative path. For now use name.
        return data.name;
    }

    public static void TryAddOrUpdateMachine(Machine m)
    {
        var svc = GameStateService.Instance;
        if (svc == null || m == null || m.Data == null) return;

        var list = svc.State.machines;
        var entry = list.Find(x => x.anchor == m.Anchor);
        if (entry == null)
        {
            list.Add(new MachineState
            {
                machineDataPath = GetMachineDataPath(m.Data),
                anchor = m.Anchor,
                orientation = m.Orientation,
                isBroken = m.IsBroken
            });
        }
        else
        {
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
        if (entryIndex < 0)
        {
            list.Add(new BeltState
            {
                anchor = b.Anchor,
                orientation = b.Orientation,
                isTurn = b.IsTurnPrefab,
                turnKind = (int)(b.IsCorner ? GetTurnKind(b) : ConveyorBelt.BeltTurnKind.None)
            });
        }
        else
        {
            var e = list[entryIndex];
            e.orientation = b.Orientation;
            e.isTurn = b.IsTurnPrefab;
            e.turnKind = (int)(b.IsCorner ? GetTurnKind(b) : ConveyorBelt.BeltTurnKind.None);
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

    // Extract current turn kind if needed (reflection avoided for simplicity)
    private static ConveyorBelt.BeltTurnKind GetTurnKind(ConveyorBelt b)
    {
        // Since _turnKind is private we infer from rotation if needed.
        // For now assume corner orientation flagged externally; return Left/Right based on transform yaw delta.
        // If you expose a public TurnKind property, use that instead.
        return b.TurnKind;
    }
}