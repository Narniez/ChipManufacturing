using System;
using System.Collections;
using FMODUnity;
using FMOD.Studio;
using UnityEngine;

namespace ProceduralMusic {
    [DisallowMultipleComponent]
    public class MachineSound : MonoBehaviour
    {
        // MachineSound is responsible for managing FMOD event instances attached
        // to machines. It listens to production events, measure beats and chord/bass
        // changes and adjusts pitch parameters or triggers one-shot events.
        //
        // Responsibilities:
        // - Start/stop FMOD instances for continuous sounds (WhileProducing)
        // - Play one-shot percussion when production finishes (OnProduce)
        // - Respond to ConveyorChordManager events to update melodic/bass pitches
        // - Keep audio 3D attributes in sync with the GameObject

        [Tooltip("Optional override to the MachineSoundData (if set manually).")]
        public MachineSoundData soundData;

        private Machine _machine;
        private EventInstance? _instance;
        private bool _started = false;
        private bool _warnedFmod = false;
        private bool _warnedNoChordManager = false;

        // Track production progress to detect start/stop transitions
        private float _lastProductionProgress = 0f;
        // Current selected MIDI note (for lead/chord instruments) while producing
        private int? _currentSelectedMidi = null;

        private void Awake()
        {
            _machine = GetComponent<Machine>();
        }

        private void OnEnable()
        {
            if (_machine != null)
            {
                _machine.ProductionProgressChanged += OnProductionProgressChanged;
            }

            try { Machine.OnMaterialProduced += OnAnyMaterialProduced; } catch { }
            try { ConveyorChordManager.OnBassChanged += HandleBassChanged; } catch { }
            try { ConveyorChordManager.OnChordChanged += HandleChordChanged; } catch { }

            try { ProceduralMusicManager.OnMeasureBeat += HandleMeasureBeat; } catch { }
        }

        private void OnDisable()
        {
            if (_machine != null)
            {
                _machine.ProductionProgressChanged -= OnProductionProgressChanged;
            }
            try { Machine.OnMaterialProduced -= OnAnyMaterialProduced; } catch { }
            try { ConveyorChordManager.OnBassChanged -= HandleBassChanged; } catch { }
            try { ConveyorChordManager.OnChordChanged -= HandleChordChanged; } catch { }
            try { ProceduralMusicManager.OnMeasureBeat -= HandleMeasureBeat; } catch { }

            StopAndReleaseInstance();
            _lastProductionProgress = 0f;
            _currentSelectedMidi = null;
        }

        // New: lead instrument responds to chord changes to produce melodic movement
        private void HandleChordChanged(int[] chord)
        {
            if (soundData == null) return;
            if (soundData.Instrument != MachineSoundData.InstrumentType.Lead &&
                soundData.Instrument != MachineSoundData.InstrumentType.Chord) return;

            // Only update pitch if this machine is currently producing; don't start ambient loops.
            if (_machine == null || !_machine.IsProducing) return;

            if (chord == null || chord.Length == 0) return;

            // Select and apply a new pitch based on chord + optional recipe bias
            UpdateLeadPitchForChord(chord);
        }

        // Handler for production progress changes; signature assumes a single float progress parameter (0..1).
        // This will start the loop when production begins and stop it (with fade) when production ends.
        private void OnProductionProgressChanged(float progress)
        {
            // If there's no sound profile or it's not a while-producing type, ignore.
            if (soundData == null) { _lastProductionProgress = progress; return; }

            // Only machines with WhileProducing timing should manage continuous loops here
            if (soundData.Timing != MachineSoundData.PlayTiming.WhileProducing)
            {
                _lastProductionProgress = progress;
                return;
            }

            // Detect transition: start (0 -> >0)
            bool startedNow = progress > 0f && _lastProductionProgress <= 0f;
            // Detect transition: stopped (>0 -> 0)
            bool stoppedNow = progress <= 0f && _lastProductionProgress > 0f;

            _lastProductionProgress = progress;

            try
            {
                if (startedNow)
                {
                    // Choose an initial pitch from the current chord (if available) and ensure loop runs
                    var chordMgr = FindFirstObjectByType<ConveyorChordManager>();
                    int[] chord = chordMgr != null ? chordMgr.GetCurrentChordMidi() : null;
                    if (chord != null && chord.Length > 0)
                    {
                        UpdateLeadPitchForChord(chord);
                    }
                    // Start loop (if not already) - StartLoop will set initial pitch if _currentSelectedMidi present
                    StartLoop();
                }
                else if (stoppedNow)
                {
                    // Stop with fadeout (ALLOWFADEOUT) and release
                    StopAndReleaseInstance();
                    _currentSelectedMidi = null;
                }
                else
                {
                    // mid-progress updates: keep running but don't restart instance
                    // nothing to do here; chord events will mutate pitch via HandleChordChanged
                }
            }
            catch (Exception ex)
            {
                if (!_warnedFmod)
                {
                    Debug.LogWarning($"MachineSound: error handling production progress on {name}: {ex.Message}");
                    _warnedFmod = true;
                }
            }
        }

        // Called when any material is produced anywhere; check positional match and OnProduce timing for matching MaterialSoundData
        private void OnAnyMaterialProduced(MaterialData mat, Vector3 pos, MachineRecipe recipe)
        {
            if (soundData == null) return;
            if (Vector3.Distance(transform.position, pos) > 0.01f) return; // small positional match

            // Prefer recipe-specific sound data if available
            var registry = DataRegistry.Instance ?? DataRegistry.FindOrLoad();
            RecipeSoundData rsd = null;
            if (registry != null && recipe != null)
                rsd = registry.GetRecipeSoundDataForRecipe(recipe);

            if (rsd != null && rsd.playTiming == MachineSoundData.PlayTiming.OnProduce)
            {
                var profile = rsd.overrideMachineSoundProfile ?? soundData;
                if (profile != null)
                {
                    string path = profile.GetSelectedEventPath();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        StartCoroutine(PlayOneShotAndStop(path, profile.PitchParameterName, null, 0.6f));
                        return;
                    }
                }
            }

            // fallback to previous behavior
            if (soundData != null && soundData.Timing == MachineSoundData.PlayTiming.OnProduce)
            {
                string path = soundData.GetSelectedEventPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    StartCoroutine(PlayOneShotAndStop(path, soundData.PitchParameterName, null, 0.6f));
                }
            }
        }

        // Called every measure beat (beatIndex is 0-based inside measure)
        private void HandleMeasureBeat(int beatIndex)
        {
            // Only trigger percussion while producing
            if (_machine == null || !_machine.IsProducing) return;

            // Find registry (safe)
            var registry = DataRegistry.Instance ?? DataRegistry.FindOrLoad();
            if (registry == null) return;

            // Try to get recipe sound data for the current recipe
            RecipeSoundData rsd = null;
            // 'Machine' does not define a CurrentRecipe property in this project; rely on the fallback below
            // which scans recipe sound datas by producing machine instead.

            if (rsd != null && rsd.playTiming == MachineSoundData.PlayTiming.WhileProducing)
            {
                // Only respond to pattern if matches
                var pm = FindFirstObjectByType<ProceduralMusicManager>();
                var measure = pm != null ? pm.CurrentMeasure : null;
                int subdivisionInBeat = 0; // currently only beat resolution
                if (rsd.MatchesPattern(beatIndex, subdivisionInBeat, measure))
                {
                    var profile = rsd.overrideMachineSoundProfile ?? soundData;
                    if (profile != null)
                    {
                        string path = profile.GetSelectedEventPath();
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            StartCoroutine(PlayOneShotAndStop(path, profile.PitchParameterName, null, 0.5f));
                        }
                    }
                }
                return;
            }

            // Fallback: previous behaviour scanning registry for recipe data by producingMachine if needed
            var msds = registry.GetRecipeSoundDatasForProducingMachine(_machine.Data);
            if (msds == null || msds.Count == 0) return;

            var pm2 = FindFirstObjectByType<ProceduralMusicManager>();
            var measure2 = pm2 != null ? pm2.CurrentMeasure : null;
            int subdivision = 0;
            foreach (var m in msds)
            {
                if (m == null) continue;
                if (m.playTiming != MachineSoundData.PlayTiming.WhileProducing) continue;
                if (m.MatchesPattern(beatIndex, subdivision, measure2))
                {
                    MachineSoundData profile = m.overrideMachineSoundProfile ?? soundData;
                    string path = profile != null ? profile.GetSelectedEventPath() : null;
                    if (!string.IsNullOrWhiteSpace(path))
                        StartCoroutine(PlayOneShotAndStop(path, profile.PitchParameterName, null, 0.5f));
                }
            }
        }

        // Play a looping event briefly for one-shot/percussion use: start, wait durationSeconds, stop+release.
    /// <summary>
    /// Play a (possibly looping) FMOD event instance temporarily, then stop and release it.
    /// Used for percussion and one-shot machine sounds triggered on production.
    /// </summary>
    /// <param name="fmodPath">FMOD event path</param>
    /// <param name="pitchParamName">FMOD parameter name for pitch (semitone offset)</param>
    /// <param name="semitoneOffset">Optional semitone offset to apply before starting</param>
    /// <param name="durationSeconds">How long to let the event play before stopping</param>
    private IEnumerator PlayOneShotAndStop(string fmodPath, string pitchParamName, float? semitoneOffset = null, float durationSeconds = 0.5f)
        {
            if (string.IsNullOrWhiteSpace(fmodPath)) yield break;

            EventInstance inst = default;
            try
            {
                inst = RuntimeManager.CreateInstance(fmodPath);
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                if (semitoneOffset.HasValue)
                {
                    inst.setParameterByName(pitchParamName, NoteUtils.ClampForFmodPitch(semitoneOffset.Value));
                }
                inst.start();
            }
            catch (Exception ex)
            {
                if (!_warnedFmod)
                {
                    Debug.LogWarning($"MachineSound: failed to start one-shot FMOD event '{fmodPath}' on {name}: {ex.Message}");
                    _warnedFmod = true;
                }
                yield break;
            }

            yield return new WaitForSeconds(durationSeconds);

            try
            {
                if (inst.isValid())
                {
                    inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    inst.release();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MachineSound: error stopping one-shot FMOD event on {name}: {ex.Message}");
            }
        }

        // Bass handler: only relevant for MachineSoundData.Instrument == Bass
        private void HandleBassChanged(int midi)
        {
            if (soundData == null) return;
            if (soundData.Instrument != MachineSoundData.InstrumentType.Bass) return;

            // Check audible distance: if out of range, stop; if in range, ensure loop started and update param
            float reach = soundData.AudibleDistance;
            var cam = Camera.main;
            bool inRange = cam == null || Vector3.Distance(transform.position, cam.transform.position) <= reach;
            if (!inRange)
            {
                StopAndReleaseInstance();
                return;
            }

            // start loop if not started
            if (!_instance.HasValue || !_instance.Value.isValid())
            {
                StartLoop();
            }

            // update pitch parameter to semitone offset relative to A1
            try
            {
                if (_instance.HasValue && _instance.Value.isValid())
                {
                    float semitoneOffset = midi - NoteUtils.MidiA1;
                    _instance.Value.setParameterByName(soundData.PitchParameterName, NoteUtils.ClampForFmodPitch(semitoneOffset));
                }
            }
            catch (Exception ex)
            {
                if (!_warnedFmod)
                {
                    Debug.LogWarning($"MachineSound: failed to set pitch parameter on {name}: {ex.Message}");
                    _warnedFmod = true;
                }
            }
        }

        // Choose a lead pitch from the chord, optionally influenced by recipe's chordBias, and apply to running instance.
        private void UpdateLeadPitchForChord(int[] chord)
        {
            if (chord == null || chord.Length == 0) return;

            // Determine chordBias from recipe if available
            float chordBias = 0.5f;
            try
            {
                var registry = DataRegistry.Instance ?? DataRegistry.FindOrLoad();
                if (_machine != null && registry != null)
                {
                    var recipe = _machine.CurrentRecipe;
                    if (recipe != null)
                    {
                        var rsd = registry.GetRecipeSoundDataForRecipe(recipe);
                        if (rsd != null)
                            chordBias = Mathf.Clamp01(rsd.chordBias);
                    }
                }
            }
            catch { }

            // Improved selection logic: pick a chord tone or nearby tone depending on chordBias
            // chordBias 0 -> prefer tones inside the chord; 1 -> prefer outside/upper tones.
            int pickIndex = Mathf.Clamp((int)(UnityEngine.Random.value * chord.Length), 0, chord.Length - 1);
            if (soundData.Instrument == MachineSoundData.InstrumentType.Lead)
            {
                // bias the pickIndex toward chord tones based on chordBias
                float bias = Mathf.Clamp01(1f - chordBias); // 1 -> strongly inside chord
                if (UnityEngine.Random.value < bias)
                {
                    // choose a middle chord tone to avoid always picking the top tone
                    int low = Mathf.Max(0, chord.Length - 3);
                    int high = chord.Length;
                    pickIndex = UnityEngine.Random.Range(low, high);
                }
                else
                {
                    // sometimes choose outside the main voicing for color: pick top or add octave
                    if (UnityEngine.Random.value < 0.5f)
                        pickIndex = chord.Length - 1;
                    else
                        pickIndex = Mathf.Clamp(pickIndex + 1, 0, chord.Length - 1);
                }
            }
            else
            {
                // For chord instruments, pick a mid/high voicing but avoid always the same index
                pickIndex = Mathf.Clamp((int)(UnityEngine.Random.value * chord.Length), 0, chord.Length - 1);
            }

            int selectedMidi = chord[Mathf.Clamp(pickIndex, 0, chord.Length - 1)];
            // sometimes raise an octave for leads
            if (soundData.Instrument == MachineSoundData.InstrumentType.Lead && UnityEngine.Random.value < 0.28f)
                selectedMidi += 12;

            _currentSelectedMidi = selectedMidi;

            // Apply to instance if running; do not start a loop here if not producing
            try
            {
                if (_instance.HasValue && _instance.Value.isValid())
                {
                    float semitoneOffset = _currentSelectedMidi.Value - NoteUtils.MidiA1;
                    _instance.Value.setParameterByName(soundData.PitchParameterName, NoteUtils.ClampForFmodPitch(semitoneOffset));
                }
            }
            catch (Exception ex)
            {
                if (!_warnedFmod)
                {
                    Debug.LogWarning($"MachineSound: failed to set lead pitch on {name}: {ex.Message}");
                    _warnedFmod = true;
                }
            }
        }

        private void StartLoop()
        {
            if (soundData == null) return;
            string path = soundData.GetSelectedEventPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Already started: ensure parameter is updated if necessary
            if (_instance.HasValue && _instance.Value.isValid())
            {
                // nothing else to change currently; update 3D attrs and ensure pitch from _currentSelectedMidi
                try
                {
                    _instance.Value.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                    if (_currentSelectedMidi.HasValue)
                    {
                        float semitoneOffset = _currentSelectedMidi.Value - NoteUtils.MidiA1;
                        _instance.Value.setParameterByName(soundData.PitchParameterName, NoteUtils.ClampForFmodPitch(semitoneOffset));
                    }
                }
                catch { }
                return;
            }

            try
            {
                var inst = RuntimeManager.CreateInstance(path);
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                // If this is a bass/lead instrument and we already have a selected midi, set initial pitch parameter
                if (soundData != null && (soundData.Instrument == MachineSoundData.InstrumentType.Bass || soundData.Instrument == MachineSoundData.InstrumentType.Lead || soundData.Instrument == MachineSoundData.InstrumentType.Chord))
                {
                    try
                    {
                        if (_currentSelectedMidi.HasValue)
                        {
                            float semitoneOffset = _currentSelectedMidi.Value - NoteUtils.MidiA1;
                            inst.setParameterByName(soundData.PitchParameterName, NoteUtils.ClampForFmodPitch(semitoneOffset));
                        }
                        else
                        {
                            // fallback to chord/bass manager initial value
                            var chordMgr = FindFirstObjectByType<ConveyorChordManager>();
                            if (chordMgr != null)
                            {
                                if (soundData.Instrument == MachineSoundData.InstrumentType.Bass)
                                {
                                    int bassMidi = chordMgr.GetCurrentBassMidi();
                                    if (bassMidi >= 0)
                                    {
                                        float semitone = bassMidi - NoteUtils.MidiA1;
                                        inst.setParameterByName(soundData.PitchParameterName, NoteUtils.ClampForFmodPitch(semitone));
                                    }
                                }
                                else
                                {
                                    var chord = chordMgr.GetCurrentChordMidi();
                                    if (chord != null && chord.Length > 0)
                                    {
                                        int attempt = chord[chord.Length - 1];
                                        float semitone = attempt - NoteUtils.MidiA1;
                                        inst.setParameterByName(soundData.PitchParameterName, NoteUtils.ClampForFmodPitch(semitone));
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                inst.start();
                _instance = inst;
                _started = true;
            }
            catch (Exception ex)
            {
                if (!_warnedFmod)
                {
                    Debug.LogWarning($"MachineSound: failed to start FMOD event '{path}' on {name}: {ex.Message}");
                    _warnedFmod = true;
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
                        inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                        inst.release();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"MachineSound: error stopping FMOD instance on {name}: {ex.Message}");
                }
            }
            _instance = null;
            _started = false;
        }

        private void Update()
        {
            if (_started && _instance.HasValue)
            {
                try
                {
                    var inst = _instance.Value;
                    if (inst.isValid())
                        inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                }
                catch { }
            }
        }
    }
}