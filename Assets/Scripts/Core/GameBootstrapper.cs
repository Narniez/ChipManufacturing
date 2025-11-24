using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private string factorySceneName = "Demo";

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // Ensure persistence services BEFORE loading gameplay scenes
        SaveManager.Ensure();
        GameStateService.Ensure();

        // Load factory scene additively if not already
        var factory = SceneManager.GetSceneByName(factorySceneName);
        if (!factory.IsValid() || !factory.isLoaded)
        {
            SceneManager.sceneLoaded += OnSceneLoadedSetActiveFactory;
            SceneManager.LoadScene(factorySceneName, LoadSceneMode.Additive);
        }
        else
        {
            SceneManager.SetActiveScene(factory);
        }
    }

    private void OnSceneLoadedSetActiveFactory(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == factorySceneName)
        {
            SceneManager.SetActiveScene(scene);
            SceneManager.sceneLoaded -= OnSceneLoadedSetActiveFactory;
        }
    }
}