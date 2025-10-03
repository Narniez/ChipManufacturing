using UnityEngine;

public class TESTInventory : MonoBehaviour
{
    void Start()
    {
        var inv = GameServices.Inventory;

        inv.Add("chip", 3);
        inv.TryRemove("chip", 1);

       
    }
}
