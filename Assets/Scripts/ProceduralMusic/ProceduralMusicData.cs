using UnityEngine;
using System.Collections.Generic;

public class ProceduralMusicData : MonoBehaviour
{
    
}


[System.Serializable]
public class Key
{
    // Simple display-friendly key representation
    public string keyName = "C";
    // Additional fields (scale, mode, etc.) can be added later
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