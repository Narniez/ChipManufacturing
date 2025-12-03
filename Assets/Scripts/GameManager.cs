using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Main Scene")]
    [SerializeField] private string mainSceneName = "Demo";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        //creates a new inventory instance
        var inventory = new InventoryService();

        //registers it globally so the rest of the game can use it
        GameServices.Init(inventory);
    }

    // Existing: loads additively
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }

    // Utility: is scene loaded?
    public static bool IsSceneLoaded(string sceneName)
    {
        var s = SceneManager.GetSceneByName(sceneName);
        return s.IsValid() && s.isLoaded;
    }

    // Load a minigame scene additively and enter isolation
    public void LoadMinigameScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (!IsSceneLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }

        var s = SceneManager.GetSceneByName(sceneName);
        if (s.IsValid() && s.isLoaded)
            SceneManager.SetActiveScene(s);

        // Ensure factory roots are hidden while a minigame is open
        SceneIsolationController.Instance?.EnterMinigameIsolation();
    }

    // Unload a minigame scene and optionally return to main
    public void UnloadMinigameScene(string sceneName, bool returnToMain = true)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        var s = SceneManager.GetSceneByName(sceneName);
        if (s.IsValid() && s.isLoaded)
        {
            SceneManager.UnloadSceneAsync(s);
        }

        if (returnToMain)
            ReturnToMain();
    }

    // Toggle a scene: load if not loaded, else unload
    public void ToggleScene(string sceneName)
    {
        if (IsSceneLoaded(sceneName))
            UnloadMinigameScene(sceneName, true);
        else
            LoadMinigameScene(sceneName);
    }

    // Return to the main scene (unload all non-main loaded scenes)
    public void ReturnToMain()
    {
        // Make sure main is present (optional, if your bootstrap always loads it)
        var main = SceneManager.GetSceneByName(mainSceneName);
        if (!main.IsValid() || !main.isLoaded)
        {
            SceneManager.LoadScene(mainSceneName, LoadSceneMode.Additive);
            main = SceneManager.GetSceneByName(mainSceneName);
        }

        // Set main as active
        if (main.IsValid())
            SceneManager.SetActiveScene(main);

        // Unload all other loaded scenes (minigames/overlays)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s == main) continue;
            if (s.isLoaded)
                SceneManager.UnloadSceneAsync(s);
        }

        // Restore factory roots and UI
        SceneIsolationController.Instance?.ExitMinigameIsolation();
    }
}
