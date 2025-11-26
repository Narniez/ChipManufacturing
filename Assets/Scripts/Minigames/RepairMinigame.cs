using UnityEngine;

public enum MinigameLaunchMode { LoadScene, AdditiveScene, PrefabOverlay }

[CreateAssetMenu(fileName = "RepairMinigame", menuName = "Scriptable Objects/Minigames/RepairMinigame")]
public class RepairMinigame : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    public Sprite icon;

    [Header("Launch")]
    [Tooltip("How this minigame is started.")]
    public MinigameLaunchMode launchMode = MinigameLaunchMode.LoadScene;

    [Tooltip("Scene name if using scene-based modes.")]
    public string sceneName;

    [Tooltip("Prefab overlay if launchMode == PrefabOverlay (instantiated additive).")]
    public GameObject overlayPrefab;

    [Header("Evaluation")]
    [Tooltip("Evaluator that decides if the minigame attempt repairs the machine.")]
     public MinigameEvaluator evaluator;
}
