using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SceneIsolationController : MonoBehaviour
{
    public static SceneIsolationController Instance { get; private set; }

    [Header("Minigame Scene Names (exact)")]
    [SerializeField] private List<string> minigameSceneNames = new List<string>();

    [Header("Root Name / Tag Whitelist (Factory stays active)")]
    [SerializeField] private List<string> persistRootNames = new List<string>(); 
    [SerializeField] private List<string> persistTags = new List<string>();      

    private readonly List<GameObject> _disabledRoots = new List<GameObject>();
    private bool _isIsolated;
    private Scene _factoryScene;

    // Track a single global EventSystem we always keep active
    private EventSystem _globalEventSystem;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _factoryScene = SceneManager.GetSceneByName("Demo");
        EnsureGlobalEventSystem();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsMinigameScene(scene.name))
            EnterMinigameIsolation();
        else
            PruneDuplicateEventSystems(); // keep global ES active when new scenes load
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (IsMinigameScene(scene.name))
            ExitMinigameIsolation();
        else
            PruneDuplicateEventSystems();
    }

    public void EnterMinigameIsolation()
    {
        if (_isIsolated) return;
        _disabledRoots.Clear();

        if (!_factoryScene.IsValid() || !_factoryScene.isLoaded)
            _factoryScene = FindFactoryScene();

        if (!_factoryScene.IsValid()) return;

        foreach (var go in _factoryScene.GetRootGameObjects())
        {
            if (ShouldPersist(go)) continue;
            if (go.activeSelf)
            {
                go.SetActive(false);
                _disabledRoots.Add(go);
            }
        }

        PruneDuplicateEventSystems();
        _isIsolated = true;
    }

    public void ExitMinigameIsolation()
    {
        if (!_isIsolated) return;

        foreach (var go in _disabledRoots)
            if (go) go.SetActive(true);

        _disabledRoots.Clear();
        _isIsolated = false;
        PruneDuplicateEventSystems();
    }

    private Scene FindFactoryScene()
    {
        // Factory scene = first loaded non-minigame scene
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && !IsMinigameScene(s.name))
                return s;
        }
        return SceneManager.GetActiveScene();
    }

    private bool IsMinigameScene(string sceneName) =>
        minigameSceneNames.Contains(sceneName);

    private bool ShouldPersist(GameObject root)
    {
        if (root.GetComponent<MinigamePersist>() != null) return true;
        if (persistRootNames.Contains(root.name)) return true;
        if (persistTags.Contains(root.tag)) return true;
        return false;
    }

    private void EnsureGlobalEventSystem()
    {
        // If we already have one, make sure it's active and persistent
        if (_globalEventSystem != null)
        {
            if (_globalEventSystem != null && _globalEventSystem.gameObject != null)
            {
                _globalEventSystem.gameObject.SetActive(true);
                DontDestroyOnLoad(_globalEventSystem.gameObject);
            }
        }

        var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);

        if (_globalEventSystem == null)
        {
            // Prefer an existing explicitly named GlobalEventSystem
            foreach (var es in systems)
            {
                if (es != null && es.gameObject.name == "GlobalEventSystem")
                {
                    _globalEventSystem = es;
                    break;
                }
            }

            // Otherwise take the first available
            if (_globalEventSystem == null && systems.Length > 0)
                _globalEventSystem = systems[0];

            // Or create one if none exist
            if (_globalEventSystem == null)
            {
                var esGO = new GameObject("GlobalEventSystem");
                _globalEventSystem = esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            DontDestroyOnLoad(_globalEventSystem.gameObject);
        }

        // Disable any other EventSystems so only one stays active
        foreach (var es in systems)
        {
            if (es == null || es == _globalEventSystem) continue;
            es.gameObject.SetActive(false);
        }

        // Ensure the global is enabled
        if (_globalEventSystem != null && !_globalEventSystem.gameObject.activeSelf)
            _globalEventSystem.gameObject.SetActive(true);
    }

    private void PruneDuplicateEventSystems()
    {
        EnsureGlobalEventSystem();
        var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);

        foreach (var es in systems)
        {
            if (es == null) continue;
            if (es == _globalEventSystem)
            {
                if (!es.gameObject.activeSelf)
                    es.gameObject.SetActive(true);
                continue;
            }

            // Keep extras disabled to avoid conflicts
            es.gameObject.SetActive(false);
        }
    }
}

// Marker component to keep a factory root active during minigame isolation
public class MinigamePersist : MonoBehaviour { }