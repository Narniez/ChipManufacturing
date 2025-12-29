using System;
using System.Collections;
using UnityEngine;

public class ProceduralMusicManager : MonoBehaviour
{
    #region Music Structure (inspector)
    [Header("Music Structure")]

    [Tooltip("Displayed current key (read-only for now)")]
    [SerializeField] public Key currentKey;

    [Tooltip("Beats per minute — controls the clock speed")]
    [SerializeField, Range(30, 240)] private int bpm = 80;

    [Tooltip("Measure / time signature (beats per measure etc.)")]
    [SerializeField] private Measure measure = new Measure();
    #endregion

    #region Global Controls (inspector)
    [Header("Global Controls")]

    [Tooltip("Reverb amount (apply in FMOD/Audiosystem integration)")]
    [SerializeField, Range(0f, 1f)] private float reverbAmount = 0.2f;

    [Tooltip("How far machine audio can be heard (world units)")]
    [SerializeField] private float soundReach = 10f;

    [Tooltip("Overall master volume (0..1)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    #endregion

    // Global clock event — UI button can call TriggerClockTick() to emit a tick.
    public static event Action OnClockTick;

    // Phase events: machines run first, belts run after
    public static event Action OnClockTick_Machines;
    public static event Action OnClockTick_Belts;

    // Optional: event fired when a measure completes (first beat)
    public static event Action<int> OnMeasureBeat; // passes 0-based beat index

    private Coroutine _clockCoroutine;

    // internal beat tracking
    private int _currentBeatIndex = 0;
    public int CurrentBeatIndex => _currentBeatIndex;
    public int BeatsPerMeasure => Mathf.Max(1, measure != null ? measure.beatsPerMeasure : 4);




    private void Awake()
    {
        // clamp defaults
        bpm = Mathf.Clamp(bpm, 30, 240);
        if (measure == null) measure = new Measure();
        measure.beatsPerMeasure = Mathf.Max(1, measure.beatsPerMeasure);

        // Start clock automatically on startup
        StartClock();
    }





    // Called by a UI Button (hook in the Inspector) to manually trigger a single tick.
    public void TriggerClockTick()
    {
        AdvanceBeat();

        // Machines first (release outputs / production logic)
        var machineHandlers = OnClockTick_Machines;
        if (machineHandlers != null)
        {
            foreach (Action h in machineHandlers.GetInvocationList())
            {
                try { h(); }
                catch (Exception ex) { Debug.LogError($"Clock tick (machines) handler exception: {ex}"); }
            }
        }

        // Measure beat notifications (for UI / patterns)
        var measureHandlers = OnMeasureBeat;
        if (measureHandlers != null)
        {
            foreach (Action<int> h in measureHandlers.GetInvocationList())
            {
                try { h(_currentBeatIndex); }
                catch (Exception ex) { Debug.LogError($"Measure beat handler exception: {ex}"); }
            }
        }

        // Belts second (consume outputs placed this tick)
        var beltHandlers = OnClockTick_Belts;
        if (beltHandlers != null)
        {
            foreach (Action h in beltHandlers.GetInvocationList())
            {
                try { h(); }
                catch (Exception ex) { Debug.LogError($"Clock tick (belts) handler exception: {ex}"); }
            }
        }
    }

    // Start the repeating clock using current BPM (one tick per beat).
    public void StartClock()
    {
        if (_clockCoroutine != null) return;
        _clockCoroutine = StartCoroutine(ClockLoop(60f / Mathf.Max(1, bpm)));
    }

    // Stop the repeating clock if running.
    public void StopClock()
    {
        if (_clockCoroutine != null)
        {
            try { StopCoroutine(_clockCoroutine); } catch { }
            _clockCoroutine = null;
        }
    }

    // Toggle clock on/off (callable from UI)
    public void ToggleClock()
    {
        if (_clockCoroutine == null) StartClock(); else StopClock();
    }

    private IEnumerator ClockLoop(float initialInterval)
    {
        // Use the provided initial interval for the first wait, then compute interval
        // from bpm each subsequent loop so BPM changes take effect without restarting.
        float interval = initialInterval > 0f ? initialInterval : (60f / Mathf.Max(1, bpm));
        Debug.Log("Clockloop started");
        while (true)
        {
            Debug.Log("Clockloop running and outputting tick");
            TriggerClockTick();
            // use real-time wait so timeScale changes don't break tempo
            yield return new WaitForSecondsRealtime(interval);
            interval = 60f / Mathf.Max(1, bpm);
        }
    }

    private void OnDisable()
    {
        StopClock();
    }







    // Public setters for audio control values (apply integration in audio system as needed)
    public void SetReverbAmount(float amount)
    {
        reverbAmount = Mathf.Clamp01(amount);
        // TODO: apply to FMOD or audio engine here
    }

    public void SetSoundReach(float reach)
    {
        soundReach = Mathf.Max(0f, reach);
        // TODO: apply to spatialization settings used by machine audio emitters
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        // TODO: apply to master bus / AudioListener volume or FMOD master bus
    }

    // Change BPM at runtime; no coroutine restart required — ClockLoop reads bpm each tick.
    public void SetBpm(int newBpm)
    {
        bpm = Mathf.Clamp(newBpm, 30, 240);
        // no restart needed; running loop will use the new bpm on next iteration
    }

    private void AdvanceBeat()
    {
        if (measure == null) measure = new Measure();
        _currentBeatIndex = (_currentBeatIndex + 1) % BeatsPerMeasure;
    }

    // Expose read-only properties for UI
    public Key CurrentKey => currentKey;
    public float ReverbAmount => reverbAmount;
    public float SoundReach => soundReach;
    public float MasterVolume => masterVolume;
    public int Bpm => bpm;
    public Measure CurrentMeasure => measure;
}
