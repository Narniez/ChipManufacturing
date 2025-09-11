using UnityEngine;

public interface IInventory
{
    int GetCount(string itemId);
    void Add(string itemId, int qty);
    bool TryRemove(string itemId, int qty);
}
