using UnityEngine;
using System.Collections.Generic;

public class ProceduralMusicObjects : MonoBehaviour
{
    
}

// Replaced simple Key with a full-featured Key class used by the music system
[System.Serializable]
public class Key
{
    public enum NoteName
    {
        C = 0, Cs = 1, D = 2, Ds = 3, E = 4, F = 5, Fs = 6, G = 7, Gs = 8, A = 9, As = 10, B = 11
    }

    public enum ScaleType
    {
        Major,
        NaturalMinor,
        HarmonicMinor,
        MelodicMinor,
        PentatonicMajor,
        PentatonicMinor,
        Dorian,
        Mixolydian,
        Locrian,
        Custom
    }

    [Header("Identity")]
    public string displayName = "C Major";
    public NoteName root = NoteName.C;
    public ScaleType scale = ScaleType.Major;

    [Header("Custom intervals (semitones from root) - used only when Scale = Custom")]
    public List<int> customIntervals = new List<int> { 0, 2, 4, 5, 7, 9, 11 };

    // cache
    private int[] _intervalsCache = null;

    private static readonly Dictionary<ScaleType, int[]> BuiltInScales = new Dictionary<ScaleType, int[]>
    {
        { ScaleType.Major, new[]{0,2,4,5,7,9,11} },
        { ScaleType.NaturalMinor, new[]{0,2,3,5,7,8,10} },
        { ScaleType.HarmonicMinor, new[]{0,2,3,5,7,8,11} },
        { ScaleType.MelodicMinor, new[]{0,2,3,5,7,9,11} },
        { ScaleType.PentatonicMajor, new[]{0,2,4,7,9} },
        { ScaleType.PentatonicMinor, new[]{0,3,5,7,10} },
        { ScaleType.Dorian, new[]{0,2,3,5,7,9,10} },
        { ScaleType.Mixolydian, new[]{0,2,4,5,7,9,10} },
        { ScaleType.Locrian, new[]{0,1,3,5,6,8,10} },
    };

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = $"{root} {scale}";
        _intervalsCache = null;
    }

    // returns semitone intervals from root for the chosen scale
    public int[] GetScaleIntervals()
    {
        if (_intervalsCache != null) return _intervalsCache;

        if (scale == ScaleType.Custom)
        {
            var list = new List<int>();
            foreach (var v in customIntervals)
            {
                int clamped = ((v % 12) + 12) % 12;
                if (!list.Contains(clamped)) list.Add(clamped);
            }
            list.Sort();
            _intervalsCache = list.ToArray();
            return _intervalsCache;
        }

        if (BuiltInScales.TryGetValue(scale, out var ints))
        {
            _intervalsCache = (int[])ints.Clone();
            return _intervalsCache;
        }

        _intervalsCache = (int[])BuiltInScales[ScaleType.Major].Clone();
        return _intervalsCache;
    }

    // pitch classes in the key (0..11)
    public HashSet<int> GetPitchClasses()
    {
        var rootPc = (int)root;
        var ints = GetScaleIntervals();
        var set = new HashSet<int>();
        for (int i = 0; i < ints.Length; i++)
            set.Add((rootPc + ints[i]) % 12);
        return set;
    }

    public bool ContainsMidiNote(int midiNumber)
    {
        int pc = ((midiNumber % 12) + 12) % 12;
        return GetPitchClasses().Contains(pc);
    }

    // Get a MIDI note for a given scale degree (1-based) at given octave (middle C octave=4)
    public int MidiNoteForDegree(int degree, int octave)
    {
        var ints = GetScaleIntervals();
        if (ints.Length == 0) return 60;
        degree = Mathf.Max(1, degree);
        int idx = (degree - 1) % ints.Length;
        int octaveOffset = (degree - 1) / ints.Length;
        int midiC = 12 * (octave + 1); // MIDI for C of given octave
        int rootPc = (int)root;
        return midiC + rootPc + ints[idx] + 12 * octaveOffset;
    }
}

[System.Serializable]
public class Measure
{
    [Tooltip("Number of beats per measure (e.g. 4 for 4/4, 3 for 3/4)")]
    public int beatsPerMeasure = 4;

    [Tooltip("Beat unit (denominator of time signature). Commonly 4 for quarter-note)")]
    public int beatUnit = 4;

    // Logical subdivisions that callers (e.g. machines/drum patterns) can request.
    // 'Downbeat' and 'Quarter' are identical (beat boundaries).
    // 'Upbeat' is the off-beat of an eighth-note (the "and" if using 1-&-2-&).
    public enum SubdivisionKind
    {
        Downbeat,   // quarter / beat boundary
        Quarter = Downbeat,
        Upbeat,     // off-beat (1 per beat, the eighth-note "and")
        Eighth,     // two subdivisions per beat: 0=downbeat,1=upbeat
        Sixteenth,  // four subdivisions per beat
        ThirtySecond // eight subdivisions per beat
    }

    // Map a kind to subdivisions per BEAT
    public static int SubdivisionsPerBeatForKind(SubdivisionKind kind)
    {
        switch (kind)
        {
            case SubdivisionKind.Upbeat:   // treated as 2 subdivisions/beat (the second one)
            case SubdivisionKind.Eighth:   return 2;
            case SubdivisionKind.Sixteenth: return 4;
            case SubdivisionKind.ThirtySecond: return 8;
            case SubdivisionKind.Downbeat:
            default: return 1;
        }
    }

    // Total subdivisions in one measure for the requested kind
    public int TotalSubdivisions(SubdivisionKind kind)
    {
        return Mathf.Max(1, beatsPerMeasure) * SubdivisionsPerBeatForKind(kind);
    }

    // Convert a (beatIndex, subdivisionIndexWithinBeat) into a 0-based global subdivision index within the measure.
    // subdivisionIndexWithinBeat must be in [0, SubdivisionsPerBeatForKind(kind)-1]
    public int GetGlobalSubdivisionIndex(int beatIndex, int subdivisionIndexWithinBeat, SubdivisionKind kind)
    {
        int spb = SubdivisionsPerBeatForKind(kind);
        beatIndex = Mathf.Clamp(beatIndex, 0, Mathf.Max(0, beatsPerMeasure - 1));
        subdivisionIndexWithinBeat = Mathf.Clamp(subdivisionIndexWithinBeat, 0, Mathf.Max(0, spb - 1));
        return beatIndex * spb + subdivisionIndexWithinBeat;
    }

    // Convenience: decide whether a given global subdivision index (0-based in measure) should trigger
    // when asking "every Nth <kind>".
    // Semantics: "every 4th" means trigger when (globalIndex+1) % everyN == 0 (i.e. 4th, 8th, ...).
    public bool IsEveryNthSubdivision(int globalSubdivisionIndex, int everyN)
    {
        if (everyN <= 1) return true;
        return ((globalSubdivisionIndex + 1) % everyN) == 0;
    }

    // High-level helper consumed by patterns:
    // - currentBeatIndex: 0-based beat index inside measure
    // - currentSubdivisionInBeat: 0-based subdivision index within beat for the requested kind
    // - everyN: number from "every [N]th"
    // - kind: subdivision kind to evaluate (downbeat/eighth/sixteenth/etc)
    //
    // Returns true when the current position matches the requested "every Nth <kind>" pattern.
    public bool MatchesPattern(int currentBeatIndex, int currentSubdivisionInBeat, int everyN, SubdivisionKind kind)
    {
        int global = GetGlobalSubdivisionIndex(currentBeatIndex, currentSubdivisionInBeat, kind);
        return IsEveryNthSubdivision(global, Mathf.Max(1, everyN));
    }

    // Convenience: produce all global subdivision indices inside the measure that match the "every Nth <kind>" rule.
    public IEnumerable<int> GetMatchingGlobalIndices(int everyN, SubdivisionKind kind)
    {
        int total = TotalSubdivisions(kind);
        everyN = Mathf.Max(1, everyN);
        for (int i = 0; i < total; i++)
            if (((i + 1) % everyN) == 0)
                yield return i;
    }
}

// --- Utility helpers for note parsing & semitone conversion (added) ---
public static class NoteUtils
{
    // MIDI number for A1 (55Hz) is 33
    public const int MidiA1 = 33;

    // Map note name (e.g. "C4", "A#3", "Db2") to MIDI note number (int).
    // Returns -1 on parse failure.
    public static int NoteLabelToMidi(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return -1;
        label = label.Trim();

        // parse letter [A-G]
        char letter = char.ToUpper(label[0]);
        if (letter < 'A' || letter > 'G') return -1;

        int pos = 1;
        int accidental = 0;
        if (pos < label.Length)
        {
            char c = label[pos];
            if (c == '#' || c == '♯')
            {
                accidental = 1;
                pos++;
            }
            else if (c == 'b' || c == '♭')
            {
                accidental = -1;
                pos++;
            }
        }

        // rest must be octave number (can be multi-digit, allow negative if needed)
        if (pos >= label.Length) return -1;
        string octStr = label.Substring(pos);
        if (!int.TryParse(octStr, out int octave)) return -1;

        // note name to semitone relative to C
        int basePc;
        switch (letter)
        {
            case 'C': basePc = 0; break;
            case 'D': basePc = 2; break;
            case 'E': basePc = 4; break;
            case 'F': basePc = 5; break;
            case 'G': basePc = 7; break;
            case 'A': basePc = 9; break;
            case 'B': basePc = 11; break;
            default: return -1;
        }

        int pc = (basePc + accidental + 12) % 12;
        // MIDI for C of octave: 12 * (octave + 1) (C4 == 60)
        int midiC = 12 * (octave + 1);
        int midi = midiC + pc;
        return midi;
    }

    // Convert note label to semitone offset relative to A1 (55 Hz).
    // Example: "A1" -> 0, "A2" -> +12, "C2" -> (MIDI(C2)-MIDI(A1))
    // Returns nullable int (null if parse failed).
    public static int? NoteLabelToSemitoneOffsetFromA1(string label)
    {
        int midi = NoteLabelToMidi(label);
        if (midi < 0) return null;
        return midi - MidiA1;
    }

    // Clamp semitone offset to FMOD parameter range (-24..+24)
    public static float ClampForFmodPitch(float semitoneOffset)
    {
        return Mathf.Clamp(semitoneOffset, -24f, 24f);
    }
}