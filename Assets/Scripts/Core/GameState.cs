using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MachineState
{
    public string machineDataPath;           // Resources/Addressables path or GUID to MachineData
    public Vector2Int anchor;
    public GridOrientation orientation;
    public bool isBroken;
}

[Serializable]
public class BeltState
{
    public Vector2Int anchor;
    public GridOrientation orientation;
    public bool isTurn;
    public int turnKind; // cast to ConveyorBelt.BeltTurnKind
}

[Serializable]
public class InventoryState
{
    public List<InventoryEntry> items = new();
    public int currency; // money
}

[Serializable]
public struct InventoryEntry
{
    public MaterialType type;
    public int amount;
}

[Serializable]
public class GameState
{
    public int version = 1;
    public List<MachineState> machines = new();
    public List<BeltState> belts = new();
    public InventoryState inventory = new();
    // Add other systems (research, upgrades) as needed
}