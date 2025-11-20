using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    void Awake()
    {
        //creates a new inventory instance
        var inventory = new InventoryService();

        //registers it globally so the rest of the game can use it
        GameServices.Init(inventory);

    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
