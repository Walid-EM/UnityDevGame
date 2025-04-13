using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlacementGenerator))]
public class PlacementGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PlacementGenerator placementGenerator = (PlacementGenerator)target;

        // Draw the default inspector UI
        DrawDefaultInspector();

        // Add "Generate" and "Clear" buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate"))
        {
            placementGenerator.Generate();
        }
        if (GUILayout.Button("Clear"))
        {
            placementGenerator.Clear();
        }
        EditorGUILayout.EndHorizontal();
    }
}