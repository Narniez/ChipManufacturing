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
    // Each entry represents a single inventory slot 
    public List<InventoryEntry> items = new();
}

[Serializable]
public struct InventoryEntry
{
    public MaterialType type;

    // id of MaterialData (preferred). If > 0, will use this to find the MaterialData asset.
    public int materialId;

    public int slotIndex;

    public int amount;
}

[Serializable]
public class GameState
{
    public int version = 1;
    public List<MachineState> machines = new();
    public List<BeltState> belts = new();
    public InventoryState inventory = new();
}