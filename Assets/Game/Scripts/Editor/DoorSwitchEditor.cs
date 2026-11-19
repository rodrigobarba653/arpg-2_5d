using UnityEditor;
using UnityEngine;

/// <summary>
/// EventRunner-first inspector. One-shot / persist / local prompt live in Advanced —
/// prefer EventRunner Once / door Persist / InteractivePromptTrigger.
/// </summary>
[CustomEditor(typeof(DoorSwitch))]
public class DoorSwitchEditor : Editor
{
    SerializedProperty eventRunner;
    SerializedProperty deactivateEventRunner;
    SerializedProperty toggleMode;
    SerializedProperty doors;
    SerializedProperty interactAction;
    SerializedProperty oneShot;
    SerializedProperty persistentId;
    SerializedProperty persistState;
    SerializedProperty interactPrompt;
    SerializedProperty lockJumpInside;
    SerializedProperty visualOff;
    SerializedProperty visualOn;
    SerializedProperty sfx;
    SerializedProperty activateSound;
    SerializedProperty deactivateSound;
    SerializedProperty audioVolume;
    SerializedProperty onActivated;
    SerializedProperty onDeactivated;

    bool showAdvanced;

    void OnEnable()
    {
        eventRunner = serializedObject.FindProperty("eventRunner");
        deactivateEventRunner = serializedObject.FindProperty("deactivateEventRunner");
        toggleMode = serializedObject.FindProperty("toggleMode");
        doors = serializedObject.FindProperty("doors");
        interactAction = serializedObject.FindProperty("interactAction");
        oneShot = serializedObject.FindProperty("oneShot");
        persistentId = serializedObject.FindProperty("persistentId");
        persistState = serializedObject.FindProperty("persistState");
        interactPrompt = serializedObject.FindProperty("interactPrompt");
        lockJumpInside = serializedObject.FindProperty("lockJumpInside");
        visualOff = serializedObject.FindProperty("visualOff");
        visualOn = serializedObject.FindProperty("visualOn");
        sfx = serializedObject.FindProperty("sfx");
        activateSound = serializedObject.FindProperty("activateSound");
        deactivateSound = serializedObject.FindProperty("deactivateSound");
        audioVolume = serializedObject.FindProperty("audioVolume");
        onActivated = serializedObject.FindProperty("onActivated");
        onDeactivated = serializedObject.FindProperty("onDeactivated");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var sw = (DoorSwitch)target;

        EditorGUILayout.LabelField("How it works", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Interact → Event Runner (Open Door / Wait / Camera / Dialogue…).\n" +
            "One-time use → set Execution Mode = Once on the EventRunner (not here).\n" +
            "Save door open state → Persist on the SimpleDoor.\n" +
            "Player prompt → InteractivePromptTrigger on this object.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Event Runner", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(eventRunner, new GUIContent("On Activate"));
        EditorGUILayout.PropertyField(toggleMode);
        if (toggleMode.boolValue)
        {
            EditorGUILayout.PropertyField(
                deactivateEventRunner,
                new GUIContent("On Deactivate", "Second Interact — e.g. Close Door sequence."));
        }

        if (eventRunner.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "No Event Runner. Door Creator normally adds one with Open Door. " +
                "Use the button below, or Advanced → Legacy Doors.",
                MessageType.Warning);

            if (GUILayout.Button("Add EventRunner + Open Door"))
                TryWireNewRunner(sw);
        }
        else
        {
            var runner = eventRunner.objectReferenceValue as EventRunner;
            if (runner != null && runner.startMode != EventStartMode.ManualOnly)
            {
                EditorGUILayout.HelpBox(
                    $"EventRunner Start Mode is {runner.startMode}. Prefer Manual Only — " +
                    "DoorSwitch already handles Interact.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Near the switch", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            lockJumpInside,
            new GUIContent("Lock Jump While Near", "Gameplay feel — not part of EventRunner."));
        EditorGUILayout.PropertyField(interactAction, new GUIContent("Interact Action (optional)"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Look & sound", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(visualOff, new GUIContent("Visual Off (idle)"));
        EditorGUILayout.PropertyField(visualOn, new GUIContent("Visual On (activated)"));
        EditorGUILayout.PropertyField(activateSound);
        EditorGUILayout.PropertyField(deactivateSound);
        EditorGUILayout.PropertyField(audioVolume);
        EditorGUILayout.PropertyField(sfx, new GUIContent("SFX Source (fallback)"));

        EditorGUILayout.Space(8);
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced / Legacy", true);
        if (showAdvanced)
        {
            EditorGUILayout.HelpBox(
                "Prefer EventRunner Once instead of One Shot.\n" +
                "Prefer SimpleDoor Persist instead of switch Persist.\n" +
                "Prefer InteractivePromptTrigger instead of Local Prompt Icon.\n" +
                "Legacy Doors only if Event Runner is empty.",
                MessageType.None);

            EditorGUILayout.PropertyField(
                oneShot,
                new GUIContent("One Shot (legacy)", "Prefer EventRunner Execution Mode = Once."));
            EditorGUILayout.PropertyField(
                persistState,
                new GUIContent("Persist Switch State (legacy)", "Prefer Persist on SimpleDoor + Event Once."));
            using (new EditorGUI.DisabledScope(!persistState.boolValue))
                EditorGUILayout.PropertyField(persistentId);
            EditorGUILayout.PropertyField(
                interactPrompt,
                new GUIContent("Local Prompt Icon (legacy)", "Prefer InteractivePromptTrigger."));
            EditorGUILayout.PropertyField(doors, new GUIContent("Legacy Doors"), true);
            EditorGUILayout.PropertyField(onActivated, true);
            EditorGUILayout.PropertyField(onDeactivated, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void TryWireNewRunner(DoorSwitch sw)
    {
        if (sw == null)
            return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Wire DoorSwitch EventRunner");

        var runner = sw.GetComponent<EventRunner>();
        if (runner == null)
            runner = Undo.AddComponent<EventRunner>(sw.gameObject);

        Undo.RecordObject(runner, "Configure EventRunner");
        runner.seizesPlayerControl = false;
        runner.executionMode = EventExecutionMode.Repeat;
        runner.persistOnce = false;
        runner.debugLog = false;
        runner.startMode = EventStartMode.ManualOnly;

        SimpleDoor door = null;
        if (sw.doors != null)
        {
            for (int i = 0; i < sw.doors.Length; i++)
            {
                if (sw.doors[i] != null)
                {
                    door = sw.doors[i];
                    break;
                }
            }
        }

        if (door == null)
            door = sw.GetComponentInParent<SimpleDoor>();
        if (door == null)
        {
            var doorsInRoot = sw.transform.root.GetComponentsInChildren<SimpleDoor>(true);
            if (doorsInRoot != null && doorsInRoot.Length > 0)
                door = doorsInRoot[0];
        }

        if (runner.actions == null)
            runner.actions = new System.Collections.Generic.List<EventAction>();
        if (runner.actions.Count == 0 && door != null)
            runner.actions.Add(new OpenDoorAction { door = door, waitForCompletion = true });

        Undo.RecordObject(sw, "Assign EventRunner");
        sw.eventRunner = runner;
        sw.doors = null;
        sw.oneShot = false;
        sw.persistState = false;
        EditorUtility.SetDirty(runner);
        EditorUtility.SetDirty(sw);
        Undo.CollapseUndoOperations(group);

        if (door == null)
        {
            EditorUtility.DisplayDialog(
                "DoorSwitch",
                "EventRunner added. No SimpleDoor found nearby — add an Open Door action and assign the door.",
                "OK");
        }
    }
}
