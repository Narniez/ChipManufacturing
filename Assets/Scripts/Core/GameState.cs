using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MachineState
{
    public string machineDataPath;
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
    public int turnKind; 

    public string itemMaterialKey;
    public int itemAmount;
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