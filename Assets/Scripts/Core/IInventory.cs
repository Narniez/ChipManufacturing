using System;
using System.Collections.Generic;
using UnityEngine;

public interface IInventory
{
    int GetCount(string itemId);
    void Add(string itemId, int qty);
    bool TryRemove(string itemId, int qty);

    bool TryApply(IDictionary<string, int> change); //key=itemId, value=delta (+/-) change
    IReadOnlyDictionary<string, int> GetInventoryItems();
    event Action<IDictionary<string, int>, IReadOnlyDictionary<string, int>> OnChanged;


    ///
    //void GetMaterialData(MaterialData materialData);
}
