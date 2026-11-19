using UnityEditor;
using UnityEngine;

/// <summary>
/// When EventRunner Start Mode = Interact owns this component, hide the duplicate
/// full inspector — radii are edited on EventRunner instead.
/// </summary>
[CustomEditor(typeof(WorldInteractable))]
[CanEditMultipleObjects]
public sealed class WorldInteractableEditor : Editor
{
    bool showAdvanced;

    public override void OnInspectorGUI()
    {
        var interactable = (WorldInteractable)target;
        var runner = interactable.GetComponent<EventRunner>();
        bool managedByEventRunner =
            runner != null &&
            interactable.enabled &&
            (interactable.eventRunner == null || interactable.eventRunner == runner);

        if (managedByEventRunner)
        {
            EditorGUILayout.HelpBox(
                "Managed by EventRunner → Start Mode (Interact).\n" +
                "Edit Discovery / Interaction radius there. This component must stay on the object for runtime.",
                MessageType.Info);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced (WorldInteractable)", true);
            if (!showAdvanced)
                return;

            EditorGUILayout.Space(4f);
        }

        DrawDefaultInspector();
    }
}
