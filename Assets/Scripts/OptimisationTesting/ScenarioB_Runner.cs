using System.Collections;
using UnityEngine;
using ProceduralMusic;

public class ScenarioB_Runner : MonoBehaviour
{
    [SerializeField] private int bpm = 240;
    [SerializeField] private float startDelaySeconds = 1f;

    private IEnumerator Start()
    {
        var music = FindFirstObjectByType<ProceduralMusicManager>();
        if (music != null) music.SetBpm(bpm);

        yield return new WaitForSecondsRealtime(startDelaySeconds);

        var belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
        Debug.Log($"[ScenarioB] Ready. belts={belts.Length} bpm={bpm}");
    }
}
