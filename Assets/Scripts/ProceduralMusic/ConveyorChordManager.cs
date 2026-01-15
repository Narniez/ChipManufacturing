using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralMusic {
    public class ConveyorChordManager : MonoBehaviour
    {
        [Header("Progression")]
        [Tooltip("Number of previous chords shown before the current chord")]
        [SerializeField, Min(0)] private int preChordCount = 5;

        [Tooltip("Number of future chords shown after the current chord")]
        [SerializeField, Min(0)] private int postChordCount = 5;

        [Tooltip("Chance (0..1) to repeat previous chord / introduce repetition pattern when generating progression")]
    [SerializeField, Range(0f, 1f)] private float repetitionChance = 0.35f;

        [Tooltip("Chance (0..1) that the global key will change when generating/advancing the progression")]
        [SerializeField, Range(0f, 1f)] private float keyChangeChance = 0.02f;

        [Tooltip("How many beats each chord lasts (uses ProceduralMusicManager beats).")]
        [SerializeField, Min(1)] private int chordDurationInBeats = 4;

        [Tooltip("Base octave used when creating MIDI notes for chords (middle C = octave 4). Raised to avoid low drone.")]
        [SerializeField] private int chordOctave = 5;

        [Header("Voicing & extensions")]
        [Tooltip("Chance to add a 7th to chords")]
        [SerializeField, Range(0f,1f)] private float addSeventhChance = 0.25f;
        [Tooltip("Chance to add a 9th to chords")]
    [SerializeField, Range(0f,1f)] private float addNinthChance = 0.25f;
        [Tooltip("Chance to spread chord tones across adjacent octaves (gives airiness)")]
            [SerializeField, Range(0f,1f)] private float spreadVoicingChance = 0.7f;

        [Header("Bass groove")]
        [Tooltip("Chance to octave-fill the bass on non-accent beats (adds movement)")]
        [SerializeField, Range(0f,1f)] private float bassOctaveVariationChance = 0.25f;
        [Tooltip("Chance to play an upper-octave passing bass note on off-beats")]
        [SerializeField, Range(0f,1f)] private float bassUpperPassingChance = 0.12f;

        // The generated progression: each chord represented as a list of MIDI notes (integers)
        private List<int[]> _progression = new List<int[]>();

        // Parallel list of bass notes (one MIDI note per progression slot)
        private List<int> _bassNotes = new List<int>();

        // index of currently active chord in progression
        // (will be set to preChordCount so current is centered in the timeline)
        private int _currentIndex = 0;

        // PRNG for progression generation (persistent so repetition patterns behave naturally)
        private System.Random _rand = new System.Random();

        // last chosen scale degree used for repetition pattern generation (-1 = none)
        private int _lastDegree = -1;

        // cached reference to the ProceduralMusicManager in scene
        private ProceduralMusicManager _proceduralMusicManager;

        // computed total slots in timeline (pre + current + post)
        private int TotalSlots => preChordCount + 1 + postChordCount;

        // Event: conveyors subscribe to this to be notified when the active chord changes
        public static event Action<int[]> OnChordChanged;
        // Event: machines subscribe to this to be notified when the active bass note changes (MIDI)
        public static event Action<int> OnBassChanged;

        private void OnEnable()
        {
            // ConveyorChordManager generates a short moving window of chords used by
            // conveyors and machines. It keeps a timeline of previous/current/future
            // chords and advances on measure beats. The progression generation aims
            // to be musically useful by adding occasional sevenths/9ths and spreading
            // voicings to reduce low-frequency masking.

            _proceduralMusicManager = FindFirstObjectByType<ProceduralMusicManager>();
            if (_proceduralMusicManager == null)
            {
                Debug.LogWarning("ConveyorChordManager: No ProceduralMusicManager found in scene. Progression will use safe fallbacks (C major triads).");
            }

            // generate initial progression and center the current index
            GenerateProgression();

            // subscribe to measure/beat notifications if available
            try { ProceduralMusicManager.OnMeasureBeat += OnMeasureBeat; } catch { }
        }

        private void OnDisable()
        {
            try { ProceduralMusicManager.OnMeasureBeat -= OnMeasureBeat; } catch { }
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

        // Generate a single chord using the current key. Returns MIDI chord (sorted).
        private int[] GenerateOneChord(Key keyData)
        {
            if (keyData == null)
            {
                // fallback C major triad
                return new[] { 60, 64, 67 };
            }

            var scaleIntervals = keyData.GetScaleIntervals();
            int scaleLen = Math.Max(1, scaleIntervals.Length);

            int degree;
            // repetition logic uses _lastDegree
            if (_lastDegree >= 1 && _rand.NextDouble() < repetitionChance)
            {
                degree = _lastDegree;
            }
            else
            {
                degree = _rand.Next(1, scaleLen + 1);
                _lastDegree = degree;
            }

            // basic triad degrees
            int thirdDegree = ((degree - 1) + 2) % scaleLen + 1;
            int fifthDegree = ((degree - 1) + 4) % scaleLen + 1;

            // optionally add 7th / 9th degrees
            int seventhDegree = ((degree - 1) + 6) % scaleLen + 1;
            int ninthDegree = ((degree - 1) + 8) % scaleLen + 1;

            int rootMidi = keyData.MidiNoteForDegree(degree, chordOctave);
            int thirdMidi = keyData.MidiNoteForDegree(thirdDegree, chordOctave);
            int fifthMidi = keyData.MidiNoteForDegree(fifthDegree, chordOctave);

            var notes = new List<int> { rootMidi, thirdMidi, fifthMidi };

            if (_rand.NextDouble() < addSeventhChance)
            if (_rand.NextDouble() < addSeventhChance)
                notes.Add(keyData.MidiNoteForDegree(seventhDegree, chordOctave));
            if (_rand.NextDouble() < addNinthChance)
                notes.Add(keyData.MidiNoteForDegree(ninthDegree, chordOctave + 1)); // ninth often sounds higher

            // Optionally spread voicing across octaves to avoid low drone
            if (_rand.NextDouble() < spreadVoicingChance)
            // spreading voicing helps avoid a muddy low-frequency cluster by moving
            // some chord tones up an octave occasionally
            if (_rand.NextDouble() < spreadVoicingChance)
            {
                // push some chord tones up an octave to create air
                for (int i = 0; i < notes.Count; i++)
                {
                    if (_rand.NextDouble() < 0.5)
                        notes[i] += 12; // up one octave
                }
            }

            // Inversion: rotate notes (root position / 1st inversion / 2nd inversion)
            int inv = _rand.Next(0, Math.Max(1, Math.Min(3, notes.Count)));
            for (int i = 0; i < inv; i++)
            {
                int n = notes[0];
                notes.RemoveAt(0);
                notes.Add(n + 12); // move to top
            }

            // Sort so we keep low-to-high order (but preserve wide spread)
            notes.Sort();
            return notes.ToArray();
        }

        // Regenerate progression (callable from editor or at runtime)
        public void GenerateProgression()
        {
            _progression.Clear();
            _bassNotes.Clear();

            if (_proceduralMusicManager == null) _proceduralMusicManager = FindFirstObjectByType<ProceduralMusicManager>();

            var keyData = ResolveKeyData();
            if (keyData == null)
            {
                Debug.Log("ConveyorChordManager: no key available, generating fallback progression (C major triads).");
            }

            // reset last degree so pattern generation starts fresh
            _lastDegree = -1;

            int slots = Math.Max(1, TotalSlots);
            for (int i = 0; i < slots; i++)
            {
                int[] chord = GenerateOneChord(keyData);
                _progression.Add(chord);

                // compute a bass note for this chord: use the chord root lowered one octave by default
                int bassMidi = 60; // default C4
                if (chord != null && chord.Length > 0)
                {
                    int root = chord[0];
                    // Prefer one octave below the chord's lowest tone; ensures bass sits
                    // below the harmony and reduces low-frequency masking.
                    bassMidi = Mathf.Max(0, root - 12);
                }
                _bassNotes.Add(bassMidi);
            }

            // center current index on the designated preChordCount
            _currentIndex = Mathf.Clamp(preChordCount, 0, _progression.Count - 1);

            // notify listeners (conveyors) about the current chord (send a copy)
            try
            {
                var current = GetCurrentChordMidi();
                var copy = (int[])current.Clone();
                OnChordChanged?.Invoke(copy);

                // notify bass listeners with a copy of the current bass note
                if (_bassNotes != null && _bassNotes.Count > 0)
                {
                    int bass = Mathf.Clamp(_currentIndex, 0, _bassNotes.Count - 1) >= 0 ? _bassNotes[_currentIndex] : -1;
                    if (bass >= 0) OnBassChanged?.Invoke(bass);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ConveyorChordManager: exception while invoking OnChordChanged/OnBassChanged: {ex.Message}");
            }

    #if UNITY_EDITOR
            try
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneView.RepaintAll();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
            catch { }
    #endif
        }

        // Called by ProceduralMusicManager each beat (pass-through from TriggerClockTick -> OnMeasureBeat)
        private void OnMeasureBeat(int beatIndex)
        {
            // advance chord index when the beat index is the start of a chord (every chordDurationInBeats)
            if (beatIndex % Mathf.Max(1, chordDurationInBeats) == 0)
            {
                AdvanceChord();
            }

            // emit a bass rhythmic event each beat so bass machines can respond rhythmically
            try
            {
                int baseBass = GetCurrentBassMidi();
                if (baseBass >= 0)
                {
                    int variant = baseBass;
                    // Accent on first beat of measure
                    if (beatIndex == 0)
                    {
                        variant = baseBass;
                    }
                    else
                    {
                        double r = _rand.NextDouble();
                        if (r < bassUpperPassingChance)
                        {
                            variant = baseBass + 12; // upper passing
                        }
                        else if (r < bassUpperPassingChance + bassOctaveVariationChance)
                        {
                            variant = baseBass - 12; // low fill
                        }
                        else
                        {
                            variant = baseBass;
                        }
                    }

                    OnBassChanged?.Invoke(Mathf.Max(0, variant));
                }
            }
            catch { /* swallow to avoid clock instability */ }
        }

        // Advance the progression: timeline shifts left, new chord appended at end, current stays centered (preChordCount)
        private void AdvanceChord()
        {
            if (_progression == null || _progression.Count == 0)
            {
                GenerateProgression();
                return;
            }

            // Remove the oldest (left-most) item so timeline scrolls left
            if (_progression.Count > 0)
                _progression.RemoveAt(0);
            if (_bassNotes.Count > 0)
                _bassNotes.RemoveAt(0);

            // Possibly change key before generating the new chord
            TryMaybeChangeKey();

            // Resolve current key (may have changed)
            var keyData = ResolveKeyData();

            // Append a newly generated chord on the right
            int[] newChord = GenerateOneChord(keyData);
            _progression.Add(newChord);

            // compute and append corresponding bass note
            int newBass = 60;
            if (newChord != null && newChord.Length > 0)
                newBass = Mathf.Max(0, newChord[0] - 12);
            _bassNotes.Add(newBass);

            // Ensure current index stays at the center slot (preChordCount)
            _currentIndex = Mathf.Clamp(preChordCount, 0, _progression.Count - 1);

            // notify listeners (conveyors) about the new current chord (send a copy)
            try
            {
                var current = GetCurrentChordMidi();
                var copy = (int[])current.Clone();
                OnChordChanged?.Invoke(copy);

                // notify bass listeners
                int bass = GetCurrentBassMidi();
                if (bass >= 0) OnBassChanged?.Invoke(bass);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ConveyorChordManager: exception while invoking OnChordChanged: {ex.Message}");
            }

    #if UNITY_EDITOR
            try
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneView.RepaintAll();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
            catch { }
    #endif
        }

        // Attempt to change the global key with probability keyChangeChance.
        // Chooses a related key (fifth, relative minor, etc.) only if it has sufficient overlap of pitch classes.
        private void TryMaybeChangeKey()
        {
            if (_proceduralMusicManager == null) _proceduralMusicManager = FindFirstObjectByType<ProceduralMusicManager>();
            if (_proceduralMusicManager == null) return;
            if (_rand.NextDouble() >= keyChangeChance) return;

            var current = _proceduralMusicManager.CurrentKey;
            if (current == null) return;

            // candidate root offsets to try (common related keys)
            int[] offsets = new int[] { 7, -7, 9, -9, 5, -5, 4, -4, 2, -2 };

            HashSet<int> currentPcs = current.GetPitchClasses();
            int currentScaleLen = current.GetScaleIntervals().Length;
            int minOverlap = Math.Max(2, Mathf.CeilToInt(currentScaleLen * 0.5f)); // require at least half overlap (or 2)

            Key best = null;
            int bestOverlap = -1;

            foreach (int off in offsets)
            {
                int newRoot = (((int)current.root + off) % 12 + 12) % 12;
                var candidate = new Key()
                {
                    root = (Key.NoteName)newRoot,
                    scale = current.scale,
                    customIntervals = new List<int>(current.customIntervals) // keep same intervals for scale types
                };

                var candidatePcs = candidate.GetPitchClasses();
                int overlap = 0;
                foreach (var pc in candidatePcs) if (currentPcs.Contains(pc)) overlap++;

                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    best = candidate;
                }
            }

            if (best != null && bestOverlap >= minOverlap)
            {
                // Apply the key change via ProceduralMusicManager so inspector updates and other systems are notified
                _proceduralMusicManager.SetCurrentKey(best);
               // Debug.Log($"ConveyorChordManager: changed key to {best.root} {best.scale} (overlap {bestOverlap})");
            }
        }

        // Resolve Key directly from ProceduralMusicManager (simpler & reliable)
        private Key ResolveKeyData()
        {
            if (_proceduralMusicManager == null) _proceduralMusicManager = FindFirstObjectByType<ProceduralMusicManager>();
            if (_proceduralMusicManager == null) return null;
            return _proceduralMusicManager.CurrentKey;
        }

        // Public accessors for editor
        public int ProgressionCount => _progression != null ? _progression.Count : 0;
        public int CurrentIndex => _currentIndex;

        // Optionally expose a helper to get the root midi of a chord (or -1 if none)
        public int GetChordRootMidi(int index)
        {
            var chord = GetChordAt(index);
            if (chord == null || chord.Length == 0) return -1;
            return chord[0];
        }

        // Public API: get current bass MIDI note (or -1 if not available)
        public int GetCurrentBassMidi()
        {
            if (_bassNotes == null || _bassNotes.Count == 0) return -1;
            int idx = Mathf.Clamp(_currentIndex, 0, _bassNotes.Count - 1);
            return _bassNotes[idx];
        }

        public int GetBassAt(int index)
        {
            if (_bassNotes == null || _bassNotes.Count == 0) return -1;
            index = Mathf.Clamp(index, 0, _bassNotes.Count - 1);
            return _bassNotes[index];
        }
    }
}