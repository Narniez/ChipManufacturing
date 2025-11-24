using UnityEngine;

public class GameStateService : MonoBehaviour
{
    public static GameStateService Instance { get; private set; }
    public GameState State { get; private set; } = new GameState();

    public static void Ensure()
    {
        if (Instance != null) return;

        var go = new GameObject("GameStateService");
        Instance = go.AddComponent<GameStateService>();
        DontDestroyOnLoad(go);

        // Load (never null). SaveManager.Load creates fresh if file missing/corrupt.
        var loaded = SaveManager.Load();
        Instance.State = loaded ?? new GameState();

    }

    public static void MarkDirty() => SaveManager.ScheduleAutosave();
}