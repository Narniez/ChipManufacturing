using UnityEngine;
using UnityEngine.SceneManagement;

public class RepairMinigameManager : MonoBehaviour
{
    private static RepairMinigameManager _inst;

    private int _machineInstanceID;
    private RepairMinigame _minigame;
    private string _returnScene;
    private bool _loadedAdditive;

    public static bool HasActive => _inst != null && _inst._minigame != null;
    public static RepairMinigame CurrentMinigame => _inst?._minigame;

    public static void Begin(Machine machine, RepairMinigame minigame, string returnScene)
    {
        if (machine == null || minigame == null) { Debug.LogWarning("Begin: invalid args"); return; }
        EnsureInstance();

        _inst._machineInstanceID = machine.GetInstanceID();
        _inst._minigame = minigame;
        _inst._returnScene = returnScene;

        // Isolate factory scene roots so minigame scene doesn't visually/UX overlap
        SceneIsolationController.Instance?.EnterMinigameIsolation();

        switch (minigame.launchMode)
        {
            case MinigameLaunchMode.LoadScene:
                // Convert to additive to preserve factory state
                Debug.LogWarning("RepairMinigame: LoadScene would unload the factory. Loading Additive instead.");
                SceneManager.LoadScene(minigame.sceneName, LoadSceneMode.Additive);
                _inst._loadedAdditive = true;
                break;
            case MinigameLaunchMode.AdditiveScene:
                SceneManager.LoadScene(minigame.sceneName, LoadSceneMode.Additive);
                _inst._loadedAdditive = true;
                break;
            case MinigameLaunchMode.PrefabOverlay:
                if (minigame.overlayPrefab != null)
                    Instantiate(minigame.overlayPrefab);
                _inst._loadedAdditive = false;
                break;
        }
    }

    public static void ReportResult(MinigameResult result)
    {
        if (_inst == null || _inst._minigame == null)
        {
            Debug.LogWarning("ReportResult: no active session.");
            return;
        }

        bool repair = _inst._minigame.evaluator != null &&
                      _inst._minigame.evaluator.Evaluate(result);

        // Unload the additive minigame scene
        if (_inst._loadedAdditive)
        {
            var scene = SceneManager.GetSceneByName(_inst._minigame.sceneName);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.UnloadSceneAsync(scene);
        }

        // Restore factory roots and global UI
        SceneIsolationController.Instance?.ExitMinigameIsolation();

        // Repair machine if succeeded
        var machines = Object.FindObjectsByType<Machine>(FindObjectsSortMode.None);
        foreach (var m in machines)
        {
            if (m.GetInstanceID() == _inst._machineInstanceID)
            {
                if (repair) m.Repair();
                break;
            }
        }

        _inst._machineInstanceID = 0;
        _inst._minigame = null;
        _inst._loadedAdditive = false;
    }

    public static void ExitWithoutRepair(float currentScore = 0f)
    {
        if (!HasActive) return;
        ReportResult(new MinigameResult { completed = false, scoreNormalized = Mathf.Clamp01(currentScore) });
    }

    private static void EnsureInstance()
    {
        if (_inst != null) return;
        var go = new GameObject("RepairMinigameSession");
        _inst = go.AddComponent<RepairMinigameManager>();
        Object.DontDestroyOnLoad(go);

        if (SceneIsolationController.Instance == null)
        {
            var iso = new GameObject("SceneIsolationController");
            iso.AddComponent<SceneIsolationController>();
        }
    }
}
