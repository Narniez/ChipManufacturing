using System.Collections.Generic;
using UnityEngine;

namespace ProceduralMusic
{
    [CreateAssetMenu(fileName = "MachineSoundData", menuName = "Scriptable Objects/ProceduralMusic/MachineSoundData")]
    public class MachineSoundData : ScriptableObject
    {
        public enum InstrumentType
        {
            Percussion,
            Bass,
            Chord,
            Lead
        }

        public enum PercussionCategory
        {
            Kick,
            Snare,
            HighHat,
            Tom,
            Cymbal,
            Other
        }

        public enum PlayTiming
        {
            OnProduce,       // play when product is finished and released
            WhileProducing   // play during production (progress / ticks)
        }

        [Header("Instrument")]
        [SerializeField] private InstrumentType instrumentType = InstrumentType.Percussion;

        [Header("Prefab match")]
        [Tooltip("Assign the machine prefab this MachineSoundData applies to. Used at runtime to auto-attach sounds to spawned machines.")]
        [SerializeField] private GameObject prefabMatch;

        [Header("Sounds (FMOD event paths / IDs)")]
        [Tooltip("List of FMOD event paths or internal identifiers this machine may play. Example: 'event:/Drums/Kick'")]
        [SerializeField] private List<string> fmodEventPaths = new List<string>();

        [Header("Percussion (optional)")]
        [Tooltip("Category used mainly for percussion machines")]
        [SerializeField] private PercussionCategory percussionCategory = PercussionCategory.Kick;

        [Header("Playback")]
        [Tooltip("When to play the sound: when the product is finished (OnProduce) or while it's being produced (WhileProducing).")]
        [SerializeField] private PlayTiming playTiming = PlayTiming.OnProduce;

        [Tooltip("If true, pick a random sound from the list each time; otherwise use the first entry.")]
        [SerializeField] private bool randomizeSelection = true;

        [Header("Spatial")]
        [Tooltip("Audible range for this sound (units). Conveyors/Machines will use this to decide whether to start/stop the FMOD instance).")]
        [SerializeField, Min(0f)] private float audibleDistance = 10f;

        [Header("FMOD parameters")]
        [Tooltip("Name of the FMOD parameter used for pitch (semitone offset).")]
        [SerializeField] private string pitchParameterName = "Pitch";

        // --- Public read-only accessors ---
        public InstrumentType Instrument => instrumentType;
        public IReadOnlyList<string> FmodEventPaths => fmodEventPaths;
        public PercussionCategory Percussion => percussionCategory;
        public PlayTiming Timing => playTiming;
        public bool RandomizeSelection => randomizeSelection;
        public float AudibleDistance => audibleDistance;
        public GameObject PrefabMatch => prefabMatch;
        public string PitchParameterName => pitchParameterName;

        // Helper: return a selected event path or null if none defined
        public string GetSelectedEventPath()
        {
            if (fmodEventPaths == null || fmodEventPaths.Count == 0) return null;
            if (randomizeSelection)
            {
                return fmodEventPaths[Random.Range(0, fmodEventPaths.Count)];
            }
            return fmodEventPaths[0];
        }
    }
}
