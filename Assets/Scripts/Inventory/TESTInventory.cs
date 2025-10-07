using UnityEngine;

public class TESTInventory : MonoBehaviour
{
    void Start()
    {
        var inv = GameServices.Inventory;

        inv.Add(1, 3);
        inv.TryRemove(1, 1);

       
    }
}
