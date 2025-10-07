using System;
using System.Collections.Generic;
using UnityEngine;

public interface IInventory
{
    int GetCount(int itemId);
    void Add(int itemId, int qty);
    bool TryRemove(int itemId, int qty);

    bool TryApply(IDictionary<int, int> change); //key=itemId, value=delta (+/-) change
    IReadOnlyDictionary<int, int> GetInventoryItems();
    event Action<IDictionary<int, int>, IReadOnlyDictionary<int, int>> OnChanged;


    ///
    //void GetMaterialData(MaterialData materialData);
}
