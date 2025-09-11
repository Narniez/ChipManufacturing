using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Awake()
    {
        //creates a new inventory instance
        var inventory = new InventoryService();

        //registers it globally so the rest of the game can use it
        GameServices.Init(inventory);

    }
}
