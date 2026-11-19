using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FlagDrivenActive))]
public class FlagDrivenActiveEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var driven = (FlagDrivenActive)target;
        if (driven.target == null)
        {
            EditorGUILayout.HelpBox(
                "Target is None — this component will not hide/show anything. " +
                "Assign the soldier (prefer a child; keep this host active).",
                MessageType.Error);
        }
        else if (driven.target == driven.gameObject)
        {
            EditorGUILayout.HelpBox(
                "Target is this same GameObject. Prefer a parent host + child soldier, " +
                "otherwise the component cannot turn itself off and keep listening.",
                MessageType.Warning);
        }

        if (Application.isPlaying)
        {
            bool flag = WorldState.GetFlag(driven.flagId);
            bool known = WorldState.HasFlag(driven.flagId);
            EditorGUILayout.HelpBox(
                $"Play Mode: '{driven.flagId}' value={flag}  hasFlag={known}\n" +
                "(Missing flag reads as false.)",
                MessageType.Info);

            if (GUILayout.Button("Apply Now"))
                driven.Apply();
        }
    }
}
