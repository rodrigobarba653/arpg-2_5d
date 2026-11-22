using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EncounterKillGate))]
public class EncounterKillGateEditor : Editor
{
    SerializedProperty eventRunner;
    SerializedProperty enemies;
    SerializedProperty requiredKills;
    SerializedProperty completedFlagId;
    SerializedProperty persistCompletion;
    SerializedProperty startEventOnComplete;
    SerializedProperty debugLog;
    SerializedProperty onCompleted;

    void OnEnable()
    {
        eventRunner = serializedObject.FindProperty("eventRunner");
        enemies = serializedObject.FindProperty("enemies");
        requiredKills = serializedObject.FindProperty("requiredKills");
        completedFlagId = serializedObject.FindProperty("completedFlagId");
        persistCompletion = serializedObject.FindProperty("persistCompletion");
        startEventOnComplete = serializedObject.FindProperty("startEventOnComplete");
        debugLog = serializedObject.FindProperty("debugLog");
        onCompleted = serializedObject.FindProperty("onCompleted");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var gate = (EncounterKillGate)target;

        EditorGUILayout.LabelField("How it works", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Kill N selected enemies → EventRunner.TryStart().\n" +
            "The runner does the actual beat (Open Door, Move Object, Wait, Camera, spawn via Set Active…).\n" +
            "Keep EventRunner Start Mode = Manual Only — this gate starts it.\n" +
            "Required Kills = 0 means all registered members must die.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Event Runner", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(eventRunner);

        if (eventRunner.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "No Event Runner. Add one on this object (or assign another).",
                MessageType.Warning);
            if (GUILayout.Button("Add EventRunner (Manual Only)"))
                AddRunner(gate);
        }
        else
        {
            var runner = eventRunner.objectReferenceValue as EventRunner;
            if (runner != null && runner.startMode != EventStartMode.ManualOnly)
            {
                EditorGUILayout.HelpBox(
                    $"EventRunner Start Mode is {runner.startMode}. Prefer Manual Only — " +
                    "EncounterKillGate already starts it when kills complete.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Members", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enemies);
        EditorGUILayout.PropertyField(
            requiredKills,
            new GUIContent(
                "Required Kills",
                "0 = all registered members. Example: list 20 enemies, require 15."));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Collect Children"))
        {
            Undo.RecordObject(gate, "Collect Encounter Children");
            gate.CollectChildren();
            EditorUtility.SetDirty(gate);
        }

        if (GUILayout.Button("Ping Members"))
        {
            for (int i = 0; i < gate.enemies.Count; i++)
            {
                if (gate.enemies[i] != null)
                    EditorGUIUtility.PingObject(gate.enemies[i]);
            }
        }
        EditorGUILayout.EndHorizontal();

        int listed = CountNonNull(gate.enemies);
        string needLabel = requiredKills.intValue > 0
            ? requiredKills.intValue.ToString()
            : $"all ({listed})";
        EditorGUILayout.HelpBox(
            $"Listed enemies: {listed}  ·  Threshold: {needLabel}\n" +
            "Runtime spawns: add EncounterMember on the prefab and assign this gate.",
            MessageType.None);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Persistence", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(persistCompletion);
        EditorGUILayout.PropertyField(completedFlagId);
        if (persistCompletion.boolValue && string.IsNullOrEmpty(completedFlagId.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Persist Completion is on but Completed Flag Id is empty — nothing will be saved.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("On Complete", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startEventOnComplete);
        EditorGUILayout.PropertyField(debugLog);
        EditorGUILayout.PropertyField(onCompleted);

        if (Application.isPlaying)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"Play Mode: {gate.KillCount}/{gate.RequiredCount}  " +
                $"registered={gate.RegisteredCount}  completed={gate.IsCompleted}",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(gate.IsCompleted))
            {
                if (GUILayout.Button("Force Complete (Play)"))
                    gate.EditorForceComplete();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    static int CountNonNull(System.Collections.Generic.List<EnemyHealth> list)
    {
        if (list == null)
            return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                n++;
        }
        return n;
    }

    static void AddRunner(EncounterKillGate gate)
    {
        var runner = gate.GetComponent<EventRunner>();
        if (runner == null)
            runner = Undo.AddComponent<EventRunner>(gate.gameObject);

        Undo.RecordObject(gate, "Wire Encounter EventRunner");
        gate.eventRunner = runner;
        runner.startMode = EventStartMode.ManualOnly;
        runner.executionMode = EventExecutionMode.Once;
        if (string.IsNullOrEmpty(runner.eventId))
            runner.eventId = gate.gameObject.name;
        EditorUtility.SetDirty(gate);
        EditorUtility.SetDirty(runner);
    }
}
