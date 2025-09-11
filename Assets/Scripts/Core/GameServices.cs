using UnityEngine;

//Stores references to all game systems
public static class GameServices
{
    public static IInventory Inventory { get; private set; }

    public static void Init(IInventory inventory)
    {
        Inventory = inventory;
    }
}

