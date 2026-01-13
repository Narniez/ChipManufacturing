using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralMusic {
    public class ProceduralMusicManager : MonoBehaviour
    {
        // ProceduralMusicManager maintains the global musical state and clock.
        // It exposes events for beats/measures and acts as a central place to
        // configure global parameters (BPM, reverb, master volume, audible range)
        // that other components (MachineSound, ConveyorSound, ConveyorChordManager)
        // can read from.
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
        private WaitForSecondsRealtime _wait;
        private float _waitInterval = -1f;

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

        // Validate serialized fields in editor/when values change
        private void OnValidate()
        {
            bpm = Mathf.Clamp(bpm, 30, 240);
            if (measure == null) measure = new Measure();
            measure.beatsPerMeasure = Mathf.Max(1, measure.beatsPerMeasure);
            reverbAmount = Mathf.Clamp01(reverbAmount);
            soundReach = Mathf.Max(0f, soundReach);
            masterVolume = Mathf.Clamp01(masterVolume);
        }





        // Called by a UI Button (hook in the Inspector) to manually trigger a single tick.
        public void TriggerClockTick()
        {
            AdvanceBeat();

            // Direct multicast invoke (no GetInvocationList allocation)
            try { OnClockTick_Machines?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"Clock tick (machines) handler exception: {ex}"); }

            try { OnMeasureBeat?.Invoke(_currentBeatIndex); }
            catch (Exception ex) { Debug.LogError($"Measure beat handler exception: {ex}"); }

            try { OnClockTick_Belts?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"Clock tick (belts) handler exception: {ex}"); }
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

            // caching yield instruction
            _waitInterval = interval;
            _wait = new WaitForSecondsRealtime(interval);

            while (true)
            {
                TriggerClockTick();
                // use real-time wait so timeScale changes don't break tempo
                interval = 60f / Mathf.Max(1, bpm);

                // only recreate yield instruction if bpm/interval changed
                if (_wait == null || !Mathf.Approximately(interval, _waitInterval))
                {
                    _waitInterval = interval;
                    _wait = new WaitForSecondsRealtime(interval);
                }

                yield return _wait;
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

        // Event fired when the current key is changed programmatically
        public event Action<Key> OnKeyChanged;

        // Set the current key (will replace values on the serialized Key or create one)
        public void SetCurrentKey(Key newKey)
        {
            if (newKey == null) return;
            if (currentKey == null)
            {
                currentKey = new Key();
            }

            // copy relevant fields so inspector shows the change
            currentKey.root = newKey.root;
            currentKey.scale = newKey.scale;
            // copy custom intervals if the scale is custom
            currentKey.customIntervals = new List<int>(newKey.customIntervals ?? new List<int>());

            OnKeyChanged?.Invoke(currentKey);

    #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
    #endif
        }
    }
}