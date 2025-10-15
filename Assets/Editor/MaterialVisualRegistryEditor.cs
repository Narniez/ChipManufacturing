using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MaterialVisualRegistry))]
public class MaterialVisualRegistryEditor : Editor
{
    private ReorderableList _list;

    private void OnEnable()
    {
        var entriesProp = serializedObject.FindProperty("entries");
        _list = new ReorderableList(serializedObject, entriesProp, true, true, true, true);

        _list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Material → Item Prefab");
        };

        _list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = entriesProp.GetArrayElementAtIndex(index);
            var matProp = element.FindPropertyRelative("material");
            var prefabProp = element.FindPropertyRelative("itemPrefab");

            rect.y += 3f;
            float labelWidth = 140f;
            float spacing = 6f;

            // Label shows the selected MaterialType name
            string matName = matProp.enumDisplayNames[matProp.enumValueIndex];
            var labelRect = new Rect(rect.x, rect.y, labelWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, matName, EditorStyles.boldLabel);

            // Material field (optional to change) and Prefab field
            float fieldX = rect.x + labelWidth + spacing;
            float matFieldWidth = 120f;
            var matRect = new Rect(fieldX, rect.y, matFieldWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(matRect, matProp, GUIContent.none);

            float prefabX = matRect.x + matFieldWidth + spacing;
            var prefabRect = new Rect(prefabX, rect.y, rect.xMax - prefabX, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(prefabRect, prefabProp, GUIContent.none);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        _list.DoLayoutList();
        serializedObject.ApplyModifiedProperties();

        // Button to rebuild the runtime map immediately (calls MonoBehaviour method)
        if (GUILayout.Button("Rebuild Map"))
        {
            (target as MaterialVisualRegistry)?.Rebuild();
            EditorUtility.SetDirty(target);
        }
    }
}