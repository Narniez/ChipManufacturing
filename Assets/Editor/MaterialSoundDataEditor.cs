using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(MaterialSoundData))]
public class MaterialSoundDataEditor : Editor
{
    private SerializedProperty producingMachineProp;
    private SerializedProperty playTimingProp;
    private SerializedProperty overrideProfileProp;
    private SerializedProperty percussionCategoryProp;
    private SerializedProperty everyNProp;
    private SerializedProperty subdivisionKindProp;
    private SerializedProperty chordBiasProp;
    private SerializedProperty lengthInSubdivisionsProp;
    private SerializedProperty maxJumpSemitonesProp;

    private enum PreviewMode { Auto, Percussion, Bass, Chord, Lead }
    private PreviewMode previewMode = PreviewMode.Auto;

    private void OnEnable()
    {
        producingMachineProp = serializedObject.FindProperty("producingMachine");
        playTimingProp = serializedObject.FindProperty("playTiming");
        overrideProfileProp = serializedObject.FindProperty("overrideMachineSoundProfile");

        percussionCategoryProp = serializedObject.FindProperty("percussionCategory");
        everyNProp = serializedObject.FindProperty("everyN");
        subdivisionKindProp = serializedObject.FindProperty("subdivisionKind");

        chordBiasProp = serializedObject.FindProperty("chordBias");
        lengthInSubdivisionsProp = serializedObject.FindProperty("lengthInSubdivisions");
        maxJumpSemitonesProp = serializedObject.FindProperty("maxJumpSemitones");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(producingMachineProp);
        EditorGUILayout.PropertyField(overrideProfileProp);
        EditorGUILayout.PropertyField(playTimingProp);

        // Preview mode toolbar
        previewMode = (PreviewMode)GUILayout.Toolbar((int)previewMode,
            new string[] { "Auto", "Percussion", "Bass", "Chord", "Lead" });

        // Determine effective instrument type
        MachineSoundData.InstrumentType effectiveInstrument = DetermineInstrumentType();

        EditorGUILayout.Space();

        // Show appropriate controls based on instrument or preview override
        if (effectiveInstrument == MachineSoundData.InstrumentType.Percussion)
        {
            EditorGUILayout.LabelField("Percussion Controls", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(percussionCategoryProp, new GUIContent("Percussion Category"));
            EditorGUILayout.PropertyField(everyNProp, new GUIContent("Every Nth"));
            EditorGUILayout.PropertyField(subdivisionKindProp, new GUIContent("Subdivision Kind"));

            // Draw simple timeline preview
            DrawPatternTimeline();
        }
        else // show lead-ish controls for other instruments
        {
            EditorGUILayout.LabelField("Melodic Controls", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(chordBiasProp, new GUIContent("Chord Bias"));
            EditorGUILayout.PropertyField(lengthInSubdivisionsProp, new GUIContent("Length (subdivisions)"));
            EditorGUILayout.PropertyField(maxJumpSemitonesProp, new GUIContent("Max Jump (semitones)"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Instrument preview mode can be set to Auto (tries override profile), or forced for editing.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private MachineSoundData.InstrumentType DetermineInstrumentType()
    {
        // If preview forced, honor it
        if (previewMode != PreviewMode.Auto)
        {
            switch (previewMode)
            {
                case PreviewMode.Percussion: return MachineSoundData.InstrumentType.Percussion;
                case PreviewMode.Bass: return MachineSoundData.InstrumentType.Bass;
                case PreviewMode.Chord: return MachineSoundData.InstrumentType.Chord;
                case PreviewMode.Lead: return MachineSoundData.InstrumentType.Lead;
                default: break;
            }
        }

        // If override profile present, use it
        var overrideObj = overrideProfileProp.objectReferenceValue as MachineSoundData;
        if (overrideObj != null) return overrideObj.Instrument;

        // Attempt to infer from producingMachine: try to find a MachineSoundData field on it
        var machineSO = producingMachineProp.objectReferenceValue as ScriptableObject;
        if (machineSO != null)
        {
            // Look for any serialized field referencing MachineSoundData and use it if found
            var so = new SerializedObject(machineSO);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var val = it.objectReferenceValue;
                    if (val is MachineSoundData msd)
                        return msd.Instrument;
                }
            }
        }

        // Fallback to Percussion
        return MachineSoundData.InstrumentType.Percussion;
    }

    private void DrawPatternTimeline()
    {
        // Get the current measure from ProceduralMusicManager in scene if available, else fallback
        Measure measure = null;
        var am = UnityEngine.Object.FindFirstObjectByType<ProceduralMusicManager>();
        if (am != null) measure = am.CurrentMeasure;
        if (measure == null) measure = new Measure() { beatsPerMeasure = 4, beatUnit = 4 };

        int everyN = Mathf.Max(1, everyNProp.intValue);
        Measure.SubdivisionKind kind = (Measure.SubdivisionKind)subdivisionKindProp.enumValueIndex;
        int total = measure.TotalSubdivisions(kind);

        Rect r = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, 40);
        EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));

        if (total <= 0) return;

        float w = r.width / total;
        HashSet<int> matches = new HashSet<int>();
        for (int i = 0; i < total; i++)
        {
            if (measure.IsEveryNthSubdivision(i, everyN))
                matches.Add(i);
        }

        for (int i = 0; i < total; i++)
        {
            Rect cell = new Rect(r.x + i * w, r.y, w - 2, r.height - 6);
            Color bg = matches.Contains(i) ? new Color(0.18f, 0.6f, 0.2f) : new Color(0.25f, 0.25f, 0.25f);
            EditorGUI.DrawRect(cell, bg);
            if ((i % Measure.SubdivisionsPerBeatForKind(kind)) == 0)
            {
                // beat separator
                Rect sep = new Rect(cell.x, r.y + r.height - 6, cell.width, 2);
                EditorGUI.DrawRect(sep, new Color(1f, 1f, 1f, 0.08f));
            }
        }

        // Labels
        GUIStyle label = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        Rect labelRect = new Rect(r.x, r.y + r.height - 18, r.width, 18);
        EditorGUI.LabelField(labelRect, $"Pattern: every {everyN} {kind}  (total subdivisions: {total})", label);
    }
}