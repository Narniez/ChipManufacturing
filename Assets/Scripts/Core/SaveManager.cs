using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // Single file + optional backup
    private static string SaveDir => Application.persistentDataPath;
    private static string SavePath => Path.Combine(SaveDir, "save.json");
    private static string BackupPath => Path.Combine(SaveDir, "save.bak");

    // Persistence toggle
    [SerializeField, Tooltip("Enable or disable saving/loading. When disabled, nothing is read or written.")]
    private bool persistenceEnabled = true;
    private static bool _persistenceEnabled = true;
    public static bool PersistenceEnabled
    {
        get => _persistenceEnabled;
        set
        {
            _persistenceEnabled = value;
#if UNITY_EDITOR
            Debug.Log($"[SaveManager] Persistence {(value ? "ENABLED" : "DISABLED")}");
#endif
            if (Instance != null)
                Instance.persistenceEnabled = value; // keep inspector in sync
        }
    }

    // Autosave throttle
    private static bool _dirty;
    private static float _lastWriteTime;
    [SerializeField] private float autosaveIntervalSeconds = 5f; // periodic flush

    public static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("SaveManager");
        Instance = go.AddComponent<SaveManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Directory.CreateDirectory(SaveDir); // ensure directory exists

        // sync static toggle with inspector value on this instance
        _persistenceEnabled = persistenceEnabled;
    }

    void OnValidate()
    {
        // keep static flag in sync while editing/playing
        if (Instance == null || Instance == this)
            _persistenceEnabled = persistenceEnabled;
    }

    void Update()
    {
        //if (_dirty && Time.unscaledTime - _lastWriteTime >= autosaveIntervalSeconds)
        //{
        //    Flush();
        //}
    }

    // Called by GameStateService.MarkDirty()
    public static void ScheduleAutosave()
    {
        _dirty = true;
    }

    public static GameState Load()
    {
        if (!_persistenceEnabled)
        {
#if UNITY_EDITOR
            Debug.Log("[SaveManager] Load skipped: persistence disabled.");
#endif
            return new GameState();
        }

        try
        {
            Directory.CreateDirectory(SaveDir);
            if (!File.Exists(SavePath))
            {
                // No file yet � return a fresh state (NOT null) so the game can proceed and later save
                return new GameState();
            }

            var json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
                return new GameState(); // blank file fallback

            var state = JsonUtility.FromJson<GameState>(json);
            return state ?? new GameState();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SaveManager.Load: failed ({ex.Message}). Starting with empty state.");
            return new GameState();
        }
    }

    public static void Save(GameState state)
    {
        if (!_persistenceEnabled)
        {
#if UNITY_EDITOR
            Debug.Log("[SaveManager] Save skipped: persistence disabled.");
#endif
            return;
        }

        if (state == null) return;
        try
        {
            Directory.CreateDirectory(SaveDir);
            var json = JsonUtility.ToJson(state, prettyPrint: true);

            // Atomic-ish write: write temp then replace
            var tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, json);
            // backup previous
            if (File.Exists(SavePath))
                File.Copy(SavePath, BackupPath, overwrite: true);
            File.Delete(SavePath);
            File.Move(tmp, SavePath);

#if UNITY_EDITOR
            Debug.Log($"[SaveManager] Saved ({json.Length} bytes) at {SavePath}");
#endif
            _dirty = false;
            _lastWriteTime = Time.unscaledTime;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
        }
    }

    private void Flush()
    {
        if (!_persistenceEnabled) return;
        Save(GameStateService.Instance?.State);
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) Flush(); // mobile background
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) Flush(); // lost focus (e.g. home button)
    }

    void OnApplicationQuit()
    {
        Flush(); // final attempt
    }
}