using System;
using System.Collections.Generic;
using UnityEngine;
// FMOD usage (optional in projects with FMOD)
// #if FMOD
using FMODUnity;
using FMOD.Studio;
// #endif

public class ConveyorChordManager : MonoBehaviour
{
    [Header("Progression")]
    [Tooltip("Number of chords in the progression timeline")]
    [SerializeField, Min(1)] private int progressionLength = 8;

    [Tooltip("Chance (0..1) to repeat previous chord / introduce repetition pattern when generating progression")]
    [SerializeField, Range(0f, 1f)] private float repetitionChance = 0.5f;

    [Tooltip("How many beats each chord lasts (uses AudioManager beats).")]
    [SerializeField, Min(1)] private int chordDurationInBeats = 4;

    [Tooltip("Octave used when creating MIDI notes for chords (middle C = octave 4).")]
    [SerializeField] private int chordOctave = 4;

    [Header("Visual (non-interactive)")]
    [Tooltip("Width in pixels for timeline boxes when drawing the timeline via OnGUI")]
    [SerializeField] private int timelineBoxWidth = 24;

    [Tooltip("Height in pixels for timeline boxes when drawing the timeline via OnGUI")]
    [SerializeField] private int timelineBoxHeight = 24;

    [Header("FMOD (drone playback)")]
    [Tooltip("FMOD event path used for drone/monophonic sound. Instance per chord note will be created.")]
    [SerializeField] private string fmodEventPath = "event:/Drone";

    [Tooltip("If true, play the current chord once on enable/generation.")]
    [SerializeField] private bool playCurrentChordOnGenerate = true;

    // The generated progression: each chord represented as a list of MIDI notes (integers)
    private List<int[]> _progression = new List<int[]>();

    // index of currently active chord in progression
    private int _currentIndex = 0;

    // cached reference to the ProceduralMusicManager in scene
    private ProceduralMusicManager _proceduralMusicManager;

    private void OnEnable()
    {
        _proceduralMusicManager = FindObjectOfType<ProceduralMusicManager>();
        if (_proceduralMusicManager == null)
        {
            Debug.LogWarning("ConveyorChordManager: No AudioManager found in scene.");
        }

        // generate initial progression
        GenerateProgression();

        // subscribe to measure/beat notifications if available
        ProceduralMusicManager.OnMeasureBeat += OnMeasureBeat;
    }

    private void OnDisable()
    {
        ProceduralMusicManager.OnMeasureBeat -= OnMeasureBeat;
    }

    // Public API: get the currently active chord as MIDI notes (may be null)
    public int[] GetCurrentChordMidi()
    {
        if (_progression == null || _progression.Count == 0) return Array.Empty<int>();
        int idx = Mathf.Clamp(_currentIndex, 0, _progression.Count - 1);
        return _progression[idx];
    }

    // Public API: get chord at a given progression index
    public int[] GetChordAt(int index)
    {
        if (_progression == null || _progression.Count == 0) return Array.Empty<int>();
        index = Mathf.Clamp(index, 0, _progression.Count - 1);
        return _progression[index];
    }

    // Regenerate progression (callable from editor or at runtime)
    public void GenerateProgression()
    {
        _progression.Clear();
        if (_proceduralMusicManager == null) _proceduralMusicManager = FindObjectOfType<ProceduralMusicManager>();

        // Determine current key from ProceduralMusicManager
        var keyData = ResolveKeyData();

        // If no keyData, fallback to C major pattern using basic triads
        if (keyData == null)
        {
            GenerateFallbackProgression();
        }
        else
        {
            // Build progression using degrees from the key's scale
            var scaleIntervals = keyData.GetScaleIntervals(); // semitone intervals
            int scaleLen = Math.Max(1, scaleIntervals.Length);

            System.Random rand = new System.Random(Guid.NewGuid().GetHashCode());
            int lastDegree = -1;

            for (int i = 0; i < progressionLength; i++)
            {
                int degree;
                // Possibly repeat previous degree based on repetitionChance
                if (i > 0 && lastDegree >= 1 && (float)rand.NextDouble() < repetitionChance)
                {
                    degree = lastDegree; // repeat
                }
                else
                {
                    degree = rand.Next(1, scaleLen + 1); // 1..scaleLen
                    lastDegree = degree;
                }

                // create a simple triad: degree, degree+2, degree+4 (wrapped)
                int thirdDegree = ((degree - 1) + 2) % scaleLen + 1;
                int fifthDegree = ((degree - 1) + 4) % scaleLen + 1;

                int rootMidi = keyData.MidiNoteForDegree(degree, chordOctave);
                int thirdMidi = keyData.MidiNoteForDegree(thirdDegree, chordOctave);
                int fifthMidi = keyData.MidiNoteForDegree(fifthDegree, chordOctave);

                // Normalize so chord is sorted ascending
                var chord = new[] { rootMidi, thirdMidi, fifthMidi };
                Array.Sort(chord);
                _progression.Add(chord);
            }
        }

        // reset index safely
        _currentIndex = Mathf.Clamp(_currentIndex, 0, Math.Max(0, _progression.Count - 1));

        // Play the generated chord once if requested (useful when no conveyors present)
        if (playCurrentChordOnGenerate && _progression.Count > 0)
        {
            PlayCurrentChord();
        }
    }

    // Play current chord using FMOD drone event; sets "Pitch" parameter per instance in semitones relative to A1.
    public void PlayCurrentChord()
    {
        if (_progression == null || _progression.Count == 0) return;
        var chord = GetCurrentChordMidi();
        if (chord == null || chord.Length == 0) return;

        for (int i = 0; i < chord.Length; i++)
        {
            int midi = chord[i];
            float semitoneOffset = midi - NoteUtils.MidiA1;
            float paramValue = NoteUtils.ClampForFmodPitch(semitoneOffset);

            try
            {
// #if FMOD
                var inst = RuntimeManager.CreateInstance(fmodEventPath);
                inst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
                // set Pitch parameter (expects float semitones)
                inst.setParameterByName("Pitch", paramValue);
                inst.start();
                inst.release(); // allow FMOD to clean up when finished
// #else
                // Debug.Log($"(FMOD missing) Would play {midi} (st={paramValue}) using {fmodEventPath}");
// #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error playing FMOD event '{fmodEventPath}': {ex}");
            }
        }
    }

    private void GenerateFallbackProgression()
    {
        // Very simple C major fallback triads (C, F, G, Am)
        int[][] fallback =
        {
            new[] {60, 64, 67}, // C
            new[] {65, 69, 72}, // F
            new[] {67, 71, 74}, // G
            new[] {69, 72, 76}  // Am
        };

        System.Random rand = new System.Random();
        for (int i = 0; i < progressionLength; i++)
        {
            int idx = rand.Next(0, fallback.Length);
            _progression.Add((int[])fallback[idx].Clone());
        }
    }

    // Called by AudioManager each beat (pass-through from AudioManager.TriggerClockTick)
    private void OnMeasureBeat(int beatIndex)
    {
        // advance chord index when the beat index is the start of a chord (every chordDurationInBeats)
        if (beatIndex % Mathf.Max(1, chordDurationInBeats) == 0)
        {
            AdvanceChord();
        }
    }

    private void AdvanceChord()
    {
        if (_progression == null || _progression.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _progression.Count;
    }

    // Attempt to locate Key/KeyData from ProceduralMusicManager safely
    private Key ResolveKeyData()
    {
        if (_proceduralMusicManager == null) _proceduralMusicManager = FindObjectOfType<ProceduralMusicManager>();
        if (_proceduralMusicManager == null) return null;

        try
        {
            var prop = _proceduralMusicManager.GetType().GetProperty("CurrentKey");
            if (prop != null)
            {
                var val = prop.GetValue(_proceduralMusicManager);
                if (val is Key k) return k;
                // If the project also has a KeyData ScriptableObject type by name, attempt a best-effort mapping
                if (val != null && val.GetType().Name == "KeyData")
                {
                    // try to read methods via reflection (GetScaleIntervals and MidiNoteForDegree) and wrap in a small adapter
                    // For simplicity, attempt to call MidiNoteForDegree via reflection when needed in GenerateProgression.
                    // But here we return null so GenerateProgression will fallback if unsupported.
                    return null;
                }
            }

            var field = _proceduralMusicManager.GetType().GetField("currentKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                var val2 = field.GetValue(_proceduralMusicManager);
                if (val2 is Key k2) return k2;
                if (val2 != null && val2.GetType().Name == "KeyData") return null;
            }
        }
        catch { /* ignore reflection errors, fallback */ }

        return null;
    }

    // Add these public accessors so editor can query state without accessing private fields
    public int ProgressionCount => _progression != null ? _progression.Count : 0;
    public int CurrentIndex => _currentIndex;

    // Optionally expose a helper to get the root midi of a chord (or "-" if none)
    public int GetChordRootMidi(int index)
    {
        var chord = GetChordAt(index);
        if (chord == null || chord.Length == 0) return -1;
        return chord[0];
    }
}