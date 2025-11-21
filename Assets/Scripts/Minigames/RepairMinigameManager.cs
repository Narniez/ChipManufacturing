using UnityEngine;
using UnityEngine.SceneManagement;

public class RepairMinigameManager : MonoBehaviour
{
    private static RepairMinigameManager _inst;

    private int _machineInstanceID;
    private RepairMinigame _minigame;
    private string _returnScene;

    public static bool HasActive => _inst != null && _inst._minigame != null;
    public static RepairMinigame CurrentMinigame => _inst?._minigame;

    public static void Begin(Machine machine, RepairMinigame minigame, string returnScene)
    {
        if (machine == null || minigame == null) { Debug.LogWarning("Begin: invalid args"); return; }
        EnsureInstance();

        _inst._machineInstanceID = machine.GetInstanceID();
        _inst._minigame = minigame;
        _inst._returnScene = returnScene;

        switch (minigame.launchMode)
        {
            case MinigameLaunchMode.LoadScene:
                SceneManager.LoadScene(minigame.sceneName, LoadSceneMode.Single);
                break;
            case MinigameLaunchMode.AdditiveScene:
                SceneManager.LoadScene(minigame.sceneName, LoadSceneMode.Additive);
                break;
            case MinigameLaunchMode.PrefabOverlay:
                if (minigame.overlayPrefab != null)
                    Instantiate(minigame.overlayPrefab);
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

        // Return to main scene if using single scene switching
        if (_inst._minigame.launchMode == MinigameLaunchMode.LoadScene)
            SceneManager.LoadScene(_inst._returnScene, LoadSceneMode.Single);
        else if (_inst._minigame.launchMode == MinigameLaunchMode.AdditiveScene)
        {
            // Optionally unload additive
            SceneManager.UnloadSceneAsync(_inst._minigame.sceneName);
        }
        // Prefab overlay: destroy overlay manually (overlay can call ReportResult then destroy itself)

        // Find machine again (it exists in return scene)
        var machines = FindObjectsByType<Machine>(FindObjectsSortMode.None);
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
    }

    // Explicit early exit (fail)
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
        DontDestroyOnLoad(go);
    }
}
