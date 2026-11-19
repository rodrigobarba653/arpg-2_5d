using UnityEditor;
using UnityEngine;

/// <summary>
/// Legacy component UI — migrates into <see cref="EventRunner"/> and removes itself.
/// </summary>
[CustomEditor(typeof(EventSourceTrigger))]
public class EventSourceTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Obsolete. Trigger start modes live on EventRunner (Start Mode). " +
            "Click Migrate to copy settings and remove this component.",
            MessageType.Warning);

        var trigger = (EventSourceTrigger)target;
        var runner = trigger.runner != null
            ? trigger.runner
            : trigger.GetComponent<EventRunner>();

        using (new EditorGUI.DisabledScope(runner == null))
        {
            if (GUILayout.Button("Migrate → EventRunner & Remove"))
            {
                if (runner == null)
                    return;

                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Migrate EventSourceTrigger");
                Undo.RecordObject(runner, "Migrate EventSourceTrigger");
                trigger.MigrateIntoRunner(runner);
                EditorUtility.SetDirty(runner);
                Undo.DestroyObjectImmediate(trigger);
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
        }

        if (runner == null)
            EditorGUILayout.HelpBox("No EventRunner on this object.", MessageType.Error);
    }
}
