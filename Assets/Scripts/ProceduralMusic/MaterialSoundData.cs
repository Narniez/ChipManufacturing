using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialSoundData", menuName = "Scriptable Objects/ProceduralMusic/MaterialSoundData")]
public class MaterialSoundData : ScriptableObject
{
    [Header("Producer")]
    [Tooltip("Machine (MachineData SO) that produces this material's sound. Used to determine instrument type/display.")]
    public MachineData producingMachine;

    [Header("Playback")]
    [Tooltip("When the sound should be played relative to production")]
    public MachineSoundData.PlayTiming playTiming = MachineSoundData.PlayTiming.OnProduce;

    [Tooltip("Optional override to choose a specific MachineSoundData profile for this material (if left empty, machine's profile is used).")]
    public MachineSoundData overrideMachineSoundProfile;

    #region Percussion controls
    [Header("Percussion Controls (shown/used for percussion instruments)")]
    [Tooltip("Percussion category to trigger when this material causes a percussion hit.")]
    public MachineSoundData.PercussionCategory percussionCategory = MachineSoundData.PercussionCategory.Kick;

    [Tooltip("Play pattern: Every Nth subdivision")]
    [Min(1)] public int everyN = 4;

    [Tooltip("Subdivision kind to evaluate (downbeat, upbeat/eighth, sixteenth, etc.)")]
    public Measure.SubdivisionKind subdivisionKind = Measure.SubdivisionKind.Downbeat;
    #endregion

    #region Lead / melodic controls
    [Header("Lead / Melodic Controls (used for non-percussion instruments)")]
    [Tooltip("(Hidden) set of possible pitches (MIDI or semitone offsets). Populated by runtime/key logic if needed)")]
    [HideInInspector] public List<int> possiblePitches = new List<int>();

    [Tooltip("(Hidden) current pitch index into possiblePitches (or absolute semitone).")]
    [HideInInspector] public int pitch = 0;

    [Tooltip("Bias toward notes inside the chord (0 = always in chord, 1 = always outside chord)")]
    [Range(0f, 1f)] public float chordBias = 0.5f;

    [Tooltip("Note length expressed in subdivisions (1 = one subdivision of the chosen kind)")]
    [Min(1)] public int lengthInSubdivisions = 1;

    [Tooltip("Maximum allowed pitch jump in semitones (how far melody may jump between triggers)")]
    [Min(0)] public int maxJumpSemitones = 12;
    #endregion

    #region Helpers / debug
    // Ensure sensible values
    private void OnValidate()
    {
        if (everyN < 1) everyN = 1;
        if (lengthInSubdivisions < 1) lengthInSubdivisions = 1;
        if (maxJumpSemitones < 0) maxJumpSemitones = 0;
    }

    // Returns true when the given beat/subdivision position matches this material's pattern.
    // - currentBeatIndex: 0-based beat index inside measure
    // - currentSubdivisionInBeat: 0-based subdivision index within beat (for the requested kind)
    public bool MatchesPattern(int currentBeatIndex, int currentSubdivisionInBeat, Measure measure)
    {
        if (measure == null) return false;
        return measure.MatchesPattern(currentBeatIndex, currentSubdivisionInBeat, Mathf.Max(1, everyN), subdivisionKind);
    }

    // Convenience: return all global subdivision indices in measure that match this pattern.
    public IEnumerable<int> GetMatchingGlobalIndices(Measure measure)
    {
        if (measure == null) yield break;
        foreach (var i in measure.GetMatchingGlobalIndices(Mathf.Max(1, everyN), subdivisionKind))
            yield return i;
    }
    #endregion
}
