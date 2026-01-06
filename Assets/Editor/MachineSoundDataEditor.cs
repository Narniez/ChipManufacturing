using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FMODUnity;

namespace ProceduralMusic {
    [CustomEditor(typeof(MachineSoundData))]
    public class MachineSoundDataEditor : Editor
    {
        private SerializedProperty p_instrumentType;
        private SerializedProperty p_fmodEventPaths;
    private SerializedProperty p_pitchParameterName;
        private SerializedProperty p_percussionCategory;
        private SerializedProperty p_playTiming;
        private SerializedProperty p_randomizeSelection;
        private SerializedProperty p_audibleDistance;

        private void OnEnable()
        {
            p_instrumentType = serializedObject.FindProperty("instrumentType");
            p_fmodEventPaths = serializedObject.FindProperty("fmodEventPaths");
            p_pitchParameterName = serializedObject.FindProperty("pitchParameterName");
            p_percussionCategory = serializedObject.FindProperty("percussionCategory");
            p_playTiming = serializedObject.FindProperty("playTiming");
            p_randomizeSelection = serializedObject.FindProperty("randomizeSelection");
            p_audibleDistance = serializedObject.FindProperty("audibleDistance");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(p_instrumentType);

            // Only show percussion category when the instrument type is Percussion
            var inst = (MachineSoundData.InstrumentType)p_instrumentType.enumValueIndex;
            if (inst == MachineSoundData.InstrumentType.Percussion)
            {
                EditorGUILayout.PropertyField(p_percussionCategory);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("FMOD Event Paths", EditorStyles.boldLabel);

            // Editable list of event paths with Validate button per entry
            if (p_fmodEventPaths.isArray)
            {
                int removeIndex = -1;
                for (int i = 0; i < p_fmodEventPaths.arraySize; i++)
                {
                    SerializedProperty elem = p_fmodEventPaths.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginHorizontal();
                    elem.stringValue = EditorGUILayout.TextField(elem.stringValue);
                    if (GUILayout.Button("Validate", GUILayout.Width(70)))
                    {
                        ValidateEventPath(elem.stringValue);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(22)))
                    {
                        removeIndex = i;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (removeIndex >= 0)
                {
                    p_fmodEventPaths.DeleteArrayElementAtIndex(removeIndex);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Path"))
                {
                    p_fmodEventPaths.InsertArrayElementAtIndex(p_fmodEventPaths.arraySize);
                    p_fmodEventPaths.GetArrayElementAtIndex(p_fmodEventPaths.arraySize - 1).stringValue = "";
                }

                if (GUILayout.Button("Validate All"))
                {
                    for (int i = 0; i < p_fmodEventPaths.arraySize; i++)
                    {
                        var v = p_fmodEventPaths.GetArrayElementAtIndex(i).stringValue;
                        ValidateEventPath(v);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(p_playTiming);

            // Only show pitch parameter name for instruments that use it
            var instType = (MachineSoundData.InstrumentType)p_instrumentType.enumValueIndex;
            if (instType != MachineSoundData.InstrumentType.Percussion)
            {
                EditorGUILayout.PropertyField(p_pitchParameterName, new GUIContent("Pitch Parameter"));
            }

            // Randomize selection is meaningless when there is only one event path
            using (new EditorGUI.DisabledScope(p_fmodEventPaths.arraySize <= 1))
            {
                EditorGUILayout.PropertyField(p_randomizeSelection);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spatial", EditorStyles.boldLabel);
            // Distance slider (0..200 units with float)
            p_audibleDistance.floatValue = EditorGUILayout.Slider("Audible Distance", p_audibleDistance.floatValue, 0f, 200f);

            serializedObject.ApplyModifiedProperties();
        }

        private void ValidateEventPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorUtility.DisplayDialog("FMOD Validate", "Path is empty.", "OK");
                return;
            }

            try
            {
                // Try to locate editor-side EventManager and query EventFromPath like StudioEventEmitterEditor does.
                var editorEvent = EventManager.EventFromPath(path);
                if (editorEvent != null)
                {
                    EditorUtility.DisplayDialog("FMOD Validate", $"Event found: {editorEvent.Path}\nIs3D: {editorEvent.Is3D}", "OK");
                    return;
                }
            }
            catch (Exception ex)
            {
                // EventManager may throw or not exist in some editor contexts; ignore and fall through to not-found
                Debug.LogWarning($"MachineSoundDataEditor.ValidateEventPath: EventManager lookup error: {ex.Message}");
            }

            EditorUtility.DisplayDialog("FMOD Validate", $"Event not found for path:\n{path}", "OK");
        }
    }
}