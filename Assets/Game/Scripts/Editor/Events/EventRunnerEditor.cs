using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EventRunner))]
public class EventRunnerEditor : Editor
{
    static readonly Type[] ActionTypes =
    {
        typeof(WaitAction),
        typeof(DebugLogAction),
        typeof(CameraFocusAction),
        typeof(CameraRestoreAction),
        typeof(CameraShotAction),
        typeof(CameraMoveAction),
        typeof(CameraShakeAction),
        typeof(ParallelActions),
        typeof(StartDialogueAction),
        typeof(UnityEventAction),
        typeof(SetFlagAction),
        typeof(ClearFlagAction),
        typeof(MarkEventCompletedAction),
        typeof(OpenDoorAction),
        typeof(CloseDoorAction),
        typeof(SetDoorStateAction),
        typeof(UnlockDoorAction),
    };

    internal static readonly Type[] ChildActionTypes;

    static EventRunnerEditor()
    {
        var children = new System.Collections.Generic.List<Type>(ActionTypes.Length);
        for (int i = 0; i < ActionTypes.Length; i++)
        {
            if (ActionTypes[i] != typeof(ParallelActions))
                children.Add(ActionTypes[i]);
        }

        ChildActionTypes = children.ToArray();
    }

    static readonly Type[] ConditionTypes =
    {
        typeof(FlagIsTrueCondition),
        typeof(FlagIsFalseCondition),
        typeof(EventIsCompletedCondition),
        typeof(EventIsNotCompletedCondition),
    };

    SerializedProperty eventId;
    SerializedProperty seizesPlayerControl;
    SerializedProperty executionMode;
    SerializedProperty persistOnce;
    SerializedProperty debugLog;
    SerializedProperty conditions;
    SerializedProperty actions;

    ReorderableList conditionList;
    ReorderableList actionList;

    void OnEnable()
    {
        eventId = serializedObject.FindProperty("eventId");
        seizesPlayerControl = serializedObject.FindProperty("seizesPlayerControl");
        executionMode = serializedObject.FindProperty("executionMode");
        persistOnce = serializedObject.FindProperty("persistOnce");
        debugLog = serializedObject.FindProperty("debugLog");
        conditions = serializedObject.FindProperty("conditions");
        actions = serializedObject.FindProperty("actions");

        conditionList = BuildList(conditions, "Conditions (AND)", ConditionTypes, DrawConditionElement);
        actionList = BuildList(actions, "Actions (SerializeReference)", ActionTypes, DrawActionElement);
    }

    ReorderableList BuildList(
        SerializedProperty property,
        string header,
        Type[] types,
        ReorderableList.ElementCallbackDelegate draw)
    {
        var list = new ReorderableList(serializedObject, property, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, header),
            elementHeightCallback = index => GetElementHeight(property, index),
            drawElementCallback = draw,
            onAddDropdownCallback = (rect, _) => ShowAddMenu(rect, property, types),
        };
        return list;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(eventId);
        EditorGUILayout.PropertyField(seizesPlayerControl);
        EditorGUILayout.PropertyField(executionMode);

        using (new EditorGUI.DisabledScope(
                   executionMode.enumValueIndex != (int)EventExecutionMode.Once))
        {
            EditorGUILayout.PropertyField(persistOnce, new GUIContent("Persist Once (save)"));
        }

        if (persistOnce.boolValue && string.IsNullOrEmpty(eventId.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Persist Once requires a stable Event Id.",
                MessageType.Warning);
        }

        EditorGUILayout.PropertyField(debugLog);

        EditorGUILayout.Space(6);
        conditionList.DoLayoutList();
        EditorGUILayout.Space(4);
        actionList.DoLayoutList();

        var runner = (EventRunner)target;
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Running={runner.IsRunning}  Index={runner.CurrentActionIndex}  " +
                $"CompletedOnce={runner.HasCompletedOnce}\n" +
                $"{GameplayLock.DebugSummary()}\n{EventScheduler.DebugSummary()}\n" +
                $"{WorldState.DebugSummary()}",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(runner.IsRunning))
            {
                if (GUILayout.Button("Try Start"))
                    runner.TryStart();
            }

            using (new EditorGUI.DisabledScope(!runner.IsRunning))
            {
                if (GUILayout.Button("Cancel"))
                    runner.Cancel("Inspector");
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Completion = successful full run only. Cancel does not mark Once / WorldState. " +
            "SetFlag before cancel is NOT rolled back. Flag changes do not auto-save to disk.",
            MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    float GetElementHeight(SerializedProperty listProp, int index)
    {
        if (index < 0 || index >= listProp.arraySize)
            return EditorGUIUtility.singleLineHeight;

        var element = listProp.GetArrayElementAtIndex(index);
        if (element.managedReferenceValue == null)
            return EditorGUIUtility.singleLineHeight + 4f;

        return EditorGUI.GetPropertyHeight(element, true) + 6f;
    }

    void DrawConditionElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        DrawManagedElement(conditions, rect, index, "Cond");
    }

    void DrawActionElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        DrawManagedElement(actions, rect, index, "Act");
    }

    void DrawManagedElement(SerializedProperty listProp, Rect rect, int index, string prefix)
    {
        if (index < 0 || index >= listProp.arraySize)
            return;

        var element = listProp.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);

        string label = $"{prefix}[{index}] Null";
        if (element.managedReferenceValue is EventCondition condition)
            label = $"{prefix}[{index}] {condition.DisplayName}";
        else if (element.managedReferenceValue is EventAction action)
            label = $"{prefix}[{index}] {action.DisplayName}";
        else if (element.managedReferenceValue != null)
            label = $"{prefix}[{index}] {element.managedReferenceValue.GetType().Name}";

        EditorGUI.PropertyField(rect, element, new GUIContent(label), true);
    }

    internal static void ShowAddMenu(Rect buttonRect, SerializedProperty listProp, Type[] types)
    {
        var menu = new GenericMenu();
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            menu.AddItem(new GUIContent(type.Name), false, () => AddManaged(listProp, type));
        }
        menu.DropDown(buttonRect);
    }

    internal static void AddManaged(SerializedProperty listProp, Type type)
    {
        var serializedObject = listProp.serializedObject;
        serializedObject.Update();
        int index = listProp.arraySize;
        listProp.arraySize++;
        var element = listProp.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(type);
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomPropertyDrawer(typeof(ParallelActions))]
public class ParallelActionsDrawer : PropertyDrawer
{
    const float RemoveWidth = 22f;
    const float Gap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var actions = property.FindPropertyRelative("actions");
        float height = EditorGUIUtility.singleLineHeight + Gap;
        if (actions == null)
            return height;

        for (int i = 0; i < actions.arraySize; i++)
        {
            var element = actions.GetArrayElementAtIndex(i);
            height += EditorGUI.GetPropertyHeight(element, true) + Gap;
        }

        height += EditorGUIUtility.singleLineHeight + Gap;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var actions = property.FindPropertyRelative("actions");
        if (actions == null)
        {
            EditorGUI.LabelField(position, label);
            return;
        }

        var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(header, label, EditorStyles.boldLabel);
        float y = header.yMax + Gap;

        int removeIndex = -1;
        for (int i = 0; i < actions.arraySize; i++)
        {
            var element = actions.GetArrayElementAtIndex(i);
            float rowHeight = EditorGUI.GetPropertyHeight(element, true);
            var row = new Rect(position.x, y, position.width - RemoveWidth - Gap, rowHeight);
            var remove = new Rect(row.xMax + Gap, y, RemoveWidth, EditorGUIUtility.singleLineHeight);

            string childLabel = $"[{i}]";
            if (element.managedReferenceValue is EventAction action)
                childLabel = $"[{i}] {action.DisplayName}";
            else if (element.managedReferenceValue == null)
                childLabel = $"[{i}] Null";

            EditorGUI.PropertyField(row, element, new GUIContent(childLabel), true);
            if (GUI.Button(remove, "−"))
                removeIndex = i;

            y += rowHeight + Gap;
        }

        if (removeIndex >= 0)
        {
            var removed = actions.GetArrayElementAtIndex(removeIndex);
            removed.managedReferenceValue = null;
            actions.DeleteArrayElementAtIndex(removeIndex);
            property.serializedObject.ApplyModifiedProperties();
            return;
        }

        var add = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        if (GUI.Button(add, "Add child action"))
            EventRunnerEditor.ShowAddMenu(add, actions, EventRunnerEditor.ChildActionTypes);
    }
}
