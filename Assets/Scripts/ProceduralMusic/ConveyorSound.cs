using System;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

namespace ProceduralMusic {
    [DisallowMultipleComponent]
    public class ConveyorSound : MonoBehaviour
    {
        [Header("FMOD")]
        [Tooltip("FMOD event path used by this conveyor (looping event).")]
        [SerializeField] private string fmodEventPath = "event:/Drone";

        [Tooltip("Parameter name that controls pitch in semitones (Conveyor uses 'Conveyor_Pitch' by convention).")]
        [SerializeField] private string pitchParameterName = "Conveyor_Pitch";

        [Tooltip("Override audible range (leave 0 to use ProceduralMusicManager soundReach).")]
        [SerializeField] private float audibleRangeOverride = 0f;

        // runtime
        private EventInstance? _instance = null;
        private bool _started = false;
        private ConveyorChordManager _chordManager;
        private ProceduralMusicManager _musicManager;
        private Camera _mainCamera;

        // Cached belt component so we only play when there's an item on the belt
        private ConveyorBelt _belt;

        // runtime flags to avoid repeated spam
        private bool _warnedNoMusicManager = false;
        private bool _warnedNoChordManager = false;
        private bool _warnedEmptyEventPath = false;
        private bool _warnedFmodErrorOnce = false;

        private void OnEnable()
        {
            _chordManager = FindFirstObjectByType<ConveyorChordManager>();
            _musicManager = FindFirstObjectByType<ProceduralMusicManager>();
            _mainCamera = Camera.main;
            _belt = GetComponent<ConveyorBelt>();

            if (_musicManager == null && !_warnedNoMusicManager)
            {
                Debug.LogWarning($"ConveyorSound ({name}): ProceduralMusicManager not found in scene. Using safe fallbacks (no clock ticks).");
                _warnedNoMusicManager = true;
            }

            if (_chordManager == null && !_warnedNoChordManager)
            {
                Debug.LogWarning($"ConveyorSound ({name}): ConveyorChordManager not found in scene. No chord data will be available.");
                _warnedNoChordManager = true;
            }

            // subscribe to beat notifications (safe even if manager is missing — events simply won't fire)
            try { ProceduralMusicManager.OnMeasureBeat += HandleMeasureBeat; } catch { }

            // subscribe to chord change notifications (safe)
            try { ConveyorChordManager.OnChordChanged += HandleChordChanged; } catch { }

            // ensure initial state
            UpdatePlayState();
        }

        private void OnDisable()
        {
            try { ProceduralMusicManager.OnMeasureBeat -= HandleMeasureBeat; } catch { }
            try { ConveyorChordManager.OnChordChanged -= HandleChordChanged; } catch { }
            StopAndReleaseInstance();
        }

        // Called when ConveyorChordManager generates/advances chord
        private void HandleChordChanged(int[] newChord)
        {
            // update immediately (will start/stop and set parameter as needed)
            UpdatePlayState();
        }

        private void Update()
        {
            // keep 3D position updated while playing
            if (_started && _instance.HasValue)
            {
                try
                {
                    var inst = _instance.Value;
                    if (inst.isValid())
                    {
                        inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                    }
                }
                catch { /* ignore transient FMOD errors */ }
            }
        }

        private void HandleMeasureBeat(int beatIndex)
        {
            // On every beat, refresh play state and pitch based on current chord
            UpdatePlayState();
        }

        private void UpdatePlayState()
        {
            // Only play if this belt currently has an item
            if (_belt != null && !_belt.HasItem)
            {
                if (_started) StopAndReleaseInstance();
                return;
            }

            // check audible range
            float reach = audibleRangeOverride > 0f ? audibleRangeOverride : (_musicManager != null ? _musicManager.SoundReach : 0f);
            bool inRange = false;
            if (_mainCamera != null && reach > 0f)
            {
                inRange = Vector3.Distance(transform.position, _mainCamera.transform.position) <= reach;
            }
            else
            {
                // if no camera or zero reach, default to playing
                inRange = true;
            }

            // fetch current chord
            int[] chord = null;
            if (_chordManager != null) chord = _chordManager.GetCurrentChordMidi();
            if (chord == null || chord.Length == 0) chord = null;

            bool shouldPlay = inRange && chord != null && chord.Length > 0;

            if (shouldPlay)
            {
                // ensure event path present
                if (string.IsNullOrWhiteSpace(fmodEventPath))
                {
                    if (!_warnedEmptyEventPath)
                    {
                        Debug.LogWarning($"ConveyorSound ({name}): fmodEventPath is empty. No audio will play. Set an FMOD event path on the component.");
                        _warnedEmptyEventPath = true;
                    }
                    return;
                }

                // determine deterministic voice index for this conveyor so multiple conveyors map across chord notes
                int voiceIndex = Mathf.Abs(transform.position.GetHashCode()) % chord.Length;
                int midi = chord[voiceIndex];
                float semitoneOffset = midi - NoteUtils.MidiA1;
                float paramValue = NoteUtils.ClampForFmodPitch(semitoneOffset);

                if (!_started || !_instance.HasValue || !_instance.Value.isValid())
                {
                    StartInstance(paramValue);
                }
                else
                {
                    // update pitch parameter only
                    try
                    {
                        var inst = _instance.Value;
                        inst.setParameterByName(pitchParameterName, paramValue);
                        // ensure attributes are fresh
                        inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                    }
                    catch (Exception ex)
                    {
                        if (!_warnedFmodErrorOnce)
                        {
                            Debug.LogWarning($"ConveyorSound: failed to update FMOD parameter on {gameObject.name}: {ex.Message}");
                            _warnedFmodErrorOnce = true;
                        }
                    }
                }
            }
            else
            {
                if (_started)
                    StopAndReleaseInstance();
            }
        }

        private void StartInstance(float pitchSemitone)
        {
            StopAndReleaseInstance();

            if (string.IsNullOrWhiteSpace(fmodEventPath))
            {
                if (!_warnedEmptyEventPath)
                {
                    Debug.LogWarning($"ConveyorSound ({name}): tried to start but fmodEventPath is empty.");
                    _warnedEmptyEventPath = true;
                }
                return;
            }

            try
            {
                var inst = RuntimeManager.CreateInstance(fmodEventPath);
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                inst.setParameterByName(pitchParameterName, pitchSemitone);
                inst.start();
                _instance = inst;
                _started = true;
            }
            catch (Exception ex)
            {
                if (!_warnedFmodErrorOnce)
                {
                    Debug.LogWarning($"ConveyorSound: failed to create/start FMOD event '{fmodEventPath}' on {gameObject.name}: {ex.Message}");
                    _warnedFmodErrorOnce = true;
                }
                _instance = null;
                _started = false;
            }
        }

        private void StopAndReleaseInstance()
        {
            if (_instance.HasValue)
            {
                try
                {
                    var inst = _instance.Value;
                    if (inst.isValid())
                    {
                        inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                        inst.release();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ConveyorSound: error stopping FMOD instance on {gameObject.name}: {ex.Message}");
                }
            }
            _instance = null;
            _started = false;
        }

        // Optional: public helper to force update (useful for debugging)
        public void RefreshNow()
        {
            UpdatePlayState();
        }
    }
}