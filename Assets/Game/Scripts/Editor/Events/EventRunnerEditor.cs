using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EventRunner))]
public class EventRunnerEditor : Editor
{
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
    SerializedProperty startMode;
    SerializedProperty triggerPlayerLayers;
    SerializedProperty triggerRequirePlayerTag;
    SerializedProperty triggerPlayerTag;
    SerializedProperty toggleCrossCooldownSeconds;
    SerializedProperty conditions;
    SerializedProperty actions;
    SerializedProperty toggleOffActions;

    ReorderableList conditionList;
    ReorderableList actionList;
    ReorderableList toggleOffList;
    readonly List<string> warningBuffer = new List<string>(16);

    void OnEnable()
    {
        eventId = serializedObject.FindProperty("eventId");
        seizesPlayerControl = serializedObject.FindProperty("seizesPlayerControl");
        executionMode = serializedObject.FindProperty("executionMode");
        persistOnce = serializedObject.FindProperty("persistOnce");
        debugLog = serializedObject.FindProperty("debugLog");
        startMode = serializedObject.FindProperty("startMode");
        triggerPlayerLayers = serializedObject.FindProperty("triggerPlayerLayers");
        triggerRequirePlayerTag = serializedObject.FindProperty("triggerRequirePlayerTag");
        triggerPlayerTag = serializedObject.FindProperty("triggerPlayerTag");
        toggleCrossCooldownSeconds = serializedObject.FindProperty("toggleCrossCooldownSeconds");
        conditions = serializedObject.FindProperty("conditions");
        actions = serializedObject.FindProperty("actions");
        toggleOffActions = serializedObject.FindProperty("toggleOffActions");

        conditionList = BuildConditionList();
        actionList = BuildActionList();
        toggleOffList = BuildToggleOffList();

        foreach (UnityEngine.Object t in targets)
        {
            if (t is EventRunner runner)
                MigrateLegacyStartSources(runner);
        }
    }

    ReorderableList BuildConditionList()
    {
        var list = new ReorderableList(serializedObject, conditions, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Conditions (AND)"),
            elementHeightCallback = index => GetManagedHeight(conditions, index),
            drawElementCallback = (rect, index, active, focused) =>
                DrawManagedElement(conditions, rect, index, "Cond"),
            onAddDropdownCallback = (rect, _) =>
                ShowTypeMenu(rect, conditions, ConditionTypes),
        };
        return list;
    }

    ReorderableList BuildActionList()
    {
        var list = new ReorderableList(serializedObject, actions, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Actions"),
            elementHeightCallback = index => GetManagedHeight(actions, index),
            drawElementCallback = DrawRootActionElement,
            onAddDropdownCallback = (rect, _) =>
                ShowCategorizedAddMenu(rect, actions, EventActionRegistry.AuthorContext.EventRunner),
        };
        return list;
    }

    ReorderableList BuildToggleOffList()
    {
        var list = new ReorderableList(serializedObject, toggleOffActions, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Toggle OFF Actions (second cross)"),
            elementHeightCallback = index => GetManagedHeight(toggleOffActions, index),
            drawElementCallback = (rect, index, active, focused) =>
                DrawRootActionElementForList(toggleOffActions, rect, index),
            onAddDropdownCallback = (rect, _) =>
                ShowCategorizedAddMenu(rect, toggleOffActions, EventActionRegistry.AuthorContext.EventRunner),
        };
        return list;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(eventId);
        EditorGUILayout.PropertyField(seizesPlayerControl);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Run Limit", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            executionMode,
            new GUIContent(
                "Execution Mode",
                "How many times this event may complete successfully.\n" +
                "Once = at most one successful run (optionally saved).\n" +
                "Repeat = can complete again after finishing.\n" +
                "Independent of Start Mode (how it is triggered)."));

        using (new EditorGUI.DisabledScope(
                   executionMode.enumValueIndex != (int)EventExecutionMode.Once))
        {
            EditorGUILayout.PropertyField(
                persistOnce,
                new GUIContent(
                    "Persist Once (save)",
                    "Only with Once. Stores completion in WorldState so it survives save/load."));
        }

        if (persistOnce.boolValue && string.IsNullOrEmpty(eventId.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Persist Once requires a stable Event Id.",
                MessageType.Warning);
        }

        EditorGUILayout.PropertyField(debugLog);

        EditorGUILayout.Space(8);
        DrawStartModeSection();

        EditorGUILayout.Space(6);
        conditionList.DoLayoutList();
        EditorGUILayout.Space(4);

        DrawActionWarnings();
        EditorGUILayout.LabelField("Actions (Toggle ON / normal start)", EditorStyles.miniBoldLabel);
        actionList.DoLayoutList();

        if (DetectStartMode((EventRunner)target) == EventStartMode.ToggleCross)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Toggle Cross: first cross runs Actions (ON). Second cross runs Toggle OFF Actions.",
                MessageType.None);
            toggleOffList.DoLayoutList();
        }

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
            "SetFlag before cancel is NOT rolled back. Flag changes do not auto-save to disk.\n" +
            "Parallel Start Delay uses DelayedStartAction under the hood — set the delay on each child row.\n" +
            "Start Mode: trigger modes use this object's Collider. Interact still adds WorldInteractable.",
            MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawStartModeSection()
    {
        var runner = (EventRunner)target;
        bool mixed = HasMixedStartSources(runner);

        EditorGUILayout.LabelField("How It Starts", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Start Mode = what fires the event (walk in, interact, toggle…).  " +
            "Execution Mode (above) = whether it can complete more than once.",
            MessageType.None);
        if (mixed)
        {
            EditorGUILayout.HelpBox(
                "This object still has a legacy EventSourceTrigger or Interact conflict. Pick one Start Mode to clean it up.",
                MessageType.Warning);
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            startMode,
            new GUIContent(
                "Start Mode",
                "What triggers a start attempt.\n" +
                "Manual = Inspector Try Start / code only.\n" +
                "Trigger On Enter/Exit = Collider volume.\n" +
                "Toggle Cross = each plane cross toggles ON/OFF action lists.\n" +
                "Interact = WorldInteractable prompt.\n" +
                "Not the same as Execution Mode (Once vs Repeat)."));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            ApplyStartMode(runner, (EventStartMode)startMode.enumValueIndex);
            serializedObject.Update();
        }

        switch ((EventStartMode)startMode.enumValueIndex)
        {
            case EventStartMode.ManualOnly:
                EditorGUILayout.HelpBox(
                    "Manual: no auto-start. Use Inspector Try Start (Play Mode) or call TryStart from code.",
                    MessageType.None);
                break;
            case EventStartMode.TriggerOnEnter:
            case EventStartMode.TriggerOnExit:
                DrawTriggerExtras(runner);
                break;
            case EventStartMode.Interact:
                DrawInteractExtras(runner);
                break;
            case EventStartMode.ToggleCross:
                DrawToggleCrossExtras(runner);
                break;
        }
    }

    void DrawToggleCrossExtras(EventRunner runner)
    {
        EditorGUILayout.PropertyField(
            toggleCrossCooldownSeconds,
            new GUIContent(
                "Cross Cooldown",
                "Ignore another cross for this many seconds. Prevents double-toggle while walking through."));

        var box = runner.GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 size = box.size;
            float thin = Mathf.Min(size.x, size.y, size.z);
            if (thin > 1.01f)
            {
                EditorGUILayout.HelpBox(
                    $"Trigger thickness is {thin:0.##} (min axis). Prefer ≤ 1 (plane-like) so the player cannot linger and double-toggle.",
                    MessageType.Warning);
                if (GUILayout.Button("Make Thin (set smallest axis to 0.25)"))
                {
                    Undo.RecordObject(box, "Thin Toggle Trigger");
                    if (size.x <= size.y && size.x <= size.z) size.x = 0.25f;
                    else if (size.y <= size.z) size.y = 0.25f;
                    else size.z = 0.25f;
                    box.size = size;
                    EditorUtility.SetDirty(box);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Thin trigger OK (min axis {thin:0.##}). Cooldown handles the rest.",
                    MessageType.Info);
            }
        }
    }

    void DrawTriggerExtras(EventRunner runner)
    {
        var col = runner.GetComponent<Collider>();
        if (col == null)
        {
            EditorGUILayout.HelpBox("Missing Collider — click Start Mode again or add a Box Collider.", MessageType.Warning);
            if (GUILayout.Button("Add Box Collider (trigger)"))
            {
                Undo.AddComponent<BoxCollider>(runner.gameObject);
                EnsureTriggerCollider(runner.gameObject);
            }

            return;
        }

        if (!col.enabled || !col.isTrigger)
        {
            EditorGUILayout.HelpBox("Collider must be enabled and Is Trigger.", MessageType.Warning);
            if (GUILayout.Button("Fix Collider (enable + Is Trigger)"))
                EnsureTriggerCollider(runner.gameObject);
        }

        EditorGUILayout.PropertyField(triggerRequirePlayerTag, new GUIContent("Require Player Tag"));
        if (triggerRequirePlayerTag.boolValue)
            EditorGUILayout.PropertyField(triggerPlayerTag, new GUIContent("Player Tag"));
        EditorGUILayout.PropertyField(triggerPlayerLayers, new GUIContent("Player Layers"));
    }

    void DrawInteractExtras(EventRunner runner)
    {
        var interactable = runner.GetComponent<WorldInteractable>();
        if (interactable == null)
            return;

        EditorGUI.BeginChangeCheck();
        float discovery = EditorGUILayout.FloatField("Discovery Radius", interactable.discoveryRadius);
        float interaction = EditorGUILayout.FloatField("Interaction Radius", interactable.interactionRadius);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(interactable, "Edit WorldInteractable");
            interactable.discoveryRadius = Mathf.Max(0.1f, discovery);
            interactable.interactionRadius = Mathf.Max(0.05f, interaction);
            EditorUtility.SetDirty(interactable);
        }
    }

    static EventStartMode DetectStartMode(EventRunner runner)
    {
        if (runner == null)
            return EventStartMode.ManualOnly;
        return runner.startMode;
    }

    static bool HasMixedStartSources(EventRunner runner)
    {
        var interactable = runner.GetComponent<WorldInteractable>();
        var legacy = runner.GetComponent<EventSourceTrigger>();
        bool interact = interactable != null && interactable.enabled;
        bool usesVolume =
            runner.startMode == EventStartMode.TriggerOnEnter ||
            runner.startMode == EventStartMode.TriggerOnExit ||
            runner.startMode == EventStartMode.ToggleCross;
        return interact && (usesVolume || (legacy != null && legacy.enabled));
    }

    /// <summary>
    /// One-shot editor migrate: copy obsolete EventSourceTrigger into EventRunner.startMode and remove it.
    /// </summary>
    static void MigrateLegacyStartSources(EventRunner runner)
    {
        if (runner == null)
            return;

        var legacy = runner.GetComponent<EventSourceTrigger>();
        if (legacy == null)
            return;

        Undo.RecordObject(runner, "Migrate EventSourceTrigger → EventRunner");
        legacy.MigrateIntoRunner(runner);
        EditorUtility.SetDirty(runner);
        Undo.DestroyObjectImmediate(legacy);
    }

    static void ApplyStartMode(EventRunner runner, EventStartMode mode)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Event Start Mode");

        Undo.RecordObject(runner, "Set Event Start Mode");
        runner.startMode = mode;
        EditorUtility.SetDirty(runner);

        RemoveComponent<EventSourceTrigger>(runner.gameObject);

        switch (mode)
        {
            case EventStartMode.ManualOnly:
                RemoveComponent<WorldInteractable>(runner.gameObject);
                break;

            case EventStartMode.TriggerOnEnter:
            case EventStartMode.TriggerOnExit:
                RemoveComponent<WorldInteractable>(runner.gameObject);
                EnsureTriggerCollider(runner.gameObject);
                break;

            case EventStartMode.Interact:
                var interactable = runner.GetComponent<WorldInteractable>();
                if (interactable == null)
                    interactable = Undo.AddComponent<WorldInteractable>(runner.gameObject);
                Undo.RecordObject(interactable, "Configure WorldInteractable");
                interactable.eventRunner = runner;
                interactable.useCentralDetector = true;
                interactable.promptMode = InteractPromptMode.DiscoveryAndInteraction;
                if (interactable.discoveryRadius < 0.1f)
                    interactable.discoveryRadius = 3.5f;
                if (interactable.interactionRadius < 0.05f)
                    interactable.interactionRadius = 1.6f;
                interactable.enabled = true;
                EditorUtility.SetDirty(interactable);
                break;

            case EventStartMode.ToggleCross:
                RemoveComponent<WorldInteractable>(runner.gameObject);
                EnsureTriggerCollider(runner.gameObject);
                EnsureThinToggleCollider(runner.gameObject);
                if (runner.toggleCrossCooldownSeconds < 0.05f)
                    runner.toggleCrossCooldownSeconds = 0.45f;
                runner.executionMode = EventExecutionMode.Repeat;
                runner.persistOnce = false;
                runner.seizesPlayerControl = false;
                EditorUtility.SetDirty(runner);
                break;
        }

        EditorUtility.SetDirty(runner.gameObject);
        Undo.CollapseUndoOperations(group);
    }

    static void EnsureThinToggleCollider(GameObject go)
    {
        var box = go.GetComponent<BoxCollider>();
        if (box == null)
            return;

        Vector3 size = box.size;
        float thin = Mathf.Min(size.x, size.y, size.z);
        if (thin <= 1.01f)
            return;

        Undo.RecordObject(box, "Thin Toggle Trigger");
        if (size.x <= size.y && size.x <= size.z) size.x = 0.25f;
        else if (size.y <= size.z) size.y = 0.25f;
        else size.z = 0.25f;
        box.size = size;
        EditorUtility.SetDirty(box);
    }

    static void EnsureTriggerCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null)
            col = Undo.AddComponent<BoxCollider>(go);

        Undo.RecordObject(col, "Configure trigger collider");
        col.enabled = true;
        col.isTrigger = true;
        EditorUtility.SetDirty(col);
    }

    static void RemoveComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null)
            Undo.DestroyObjectImmediate(c);
    }

    void DrawActionWarnings()
    {
        warningBuffer.Clear();
        var runner = (EventRunner)target;
        if (runner.actions != null)
            EventActionEditorUtility.CollectWarnings(runner.actions, warningBuffer, insideParallel: false);

        string text = EventActionEditorUtility.FormatWarnings(warningBuffer);
        if (!string.IsNullOrEmpty(text))
            EditorGUILayout.HelpBox(text, MessageType.Warning);
    }

    float GetManagedHeight(SerializedProperty listProp, int index)
    {
        if (index < 0 || index >= listProp.arraySize)
            return EditorGUIUtility.singleLineHeight;

        var element = listProp.GetArrayElementAtIndex(index);
        if (element.managedReferenceValue == null)
            return EditorGUIUtility.singleLineHeight + 4f;

        return EditorGUI.GetPropertyHeight(element, true) + 8f;
    }

    void DrawRootActionElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        DrawRootActionElementForList(actions, rect, index);
    }

    /// <summary>
    /// ReorderableList drag handle sits on the left; SerializeReference foldouts
    /// draw their triangle slightly into that zone. Pad content so they don't overlap.
    /// </summary>
    internal const float ReorderFoldoutPad = 14f;

    void DrawRootActionElementForList(SerializedProperty listProp, Rect rect, int index)
    {
        if (index < 0 || index >= listProp.arraySize)
            return;

        var element = listProp.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.xMin += ReorderFoldoutPad;

        float pingW = 36f;
        float gap = 2f;
        var body = new Rect(rect.x, rect.y, rect.width - pingW - gap, rect.height);
        var ping = new Rect(body.xMax + gap, rect.y, pingW, EditorGUIUtility.singleLineHeight);

        string label = $"{index + 1}. Null";
        if (element.managedReferenceValue is EventAction action)
            label = $"{index + 1}. {EventActionEditorUtility.Summary(action)}";
        else if (element.managedReferenceValue != null)
            label = $"{index + 1}. {element.managedReferenceValue.GetType().Name}";

        body.height = EditorGUI.GetPropertyHeight(element, true);
        EditorGUI.PropertyField(body, element, new GUIContent(label), true);

        if (GUI.Button(ping, "Ping") && element.managedReferenceValue is EventAction pingAction)
            EventActionEditorUtility.PingActionRefs(pingAction);
    }

    void DrawManagedElement(SerializedProperty listProp, Rect rect, int index, string prefix)
    {
        if (index < 0 || index >= listProp.arraySize)
            return;

        var element = listProp.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.xMin += ReorderFoldoutPad;
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

    internal static void ShowCategorizedAddMenu(
        Rect buttonRect,
        SerializedProperty listProp,
        EventActionRegistry.AuthorContext context)
    {
        var menu = new GenericMenu();
        foreach (var entry in EventActionRegistry.ForContext(context))
        {
            Type type = entry.Type;
            string path = $"{entry.Category}/{entry.MenuName}";
            menu.AddItem(new GUIContent(path), false, () => AddManaged(listProp, type));
        }

        menu.DropDown(buttonRect);
    }

    static void ShowTypeMenu(Rect buttonRect, SerializedProperty listProp, Type[] types)
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
    const float DelayWidth = 70f;
    const float PingWidth = 36f;
    const float Gap = 2f;
    const string DelayPrefKey = "TOM.Event.Parallel.Fold.";

    readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();
    readonly List<string> warningBuffer = new List<string>(8);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var actions = property.FindPropertyRelative("actions");
        if (actions == null)
            return EditorGUIUtility.singleLineHeight;

        var list = GetList(property, actions);
        float h = list.GetHeight();
        warningBuffer.Clear();
        if (property.managedReferenceValue is ParallelActions parallel)
            EventActionEditorUtility.CollectWarnings(parallel.actions, warningBuffer, insideParallel: true);
        if (warningBuffer.Count > 0)
            h += EditorGUIUtility.singleLineHeight * Mathf.Min(warningBuffer.Count + 1, 5) + 8f;
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var actions = property.FindPropertyRelative("actions");
        if (actions == null)
        {
            EditorGUI.LabelField(position, label);
            return;
        }

        warningBuffer.Clear();
        if (property.managedReferenceValue is ParallelActions parallel)
            EventActionEditorUtility.CollectWarnings(parallel.actions, warningBuffer, insideParallel: true);

        string warn = EventActionEditorUtility.FormatWarnings(warningBuffer);
        if (!string.IsNullOrEmpty(warn))
        {
            float warnH = EditorGUIUtility.singleLineHeight * Mathf.Min(warningBuffer.Count + 1, 5) + 4f;
            var warnRect = new Rect(position.x, position.y, position.width, warnH);
            EditorGUI.HelpBox(warnRect, warn, MessageType.Warning);
            position.y += warnH + 4f;
            position.height -= warnH + 4f;
        }

        GetList(property, actions).DoList(position);
    }

    ReorderableList GetList(SerializedProperty parallelProp, SerializedProperty actions)
    {
        string key = parallelProp.propertyPath;
        if (lists.TryGetValue(key, out var existing) &&
            existing.serializedProperty != null &&
            existing.serializedProperty.propertyPath == actions.propertyPath)
            return existing;

        var list = new ReorderableList(actions.serializedObject, actions, true, true, true, true)
        {
            drawHeaderCallback = rect =>
            {
                int count = actions.arraySize;
                EditorGUI.LabelField(rect, $"Parallel children ({count}) — Start Delay per row");
            },
            elementHeightCallback = index => ElementHeight(actions, index),
            drawElementCallback = (rect, index, active, focused) =>
                DrawChild(actions, rect, index),
            onAddDropdownCallback = (rect, _) =>
                EventRunnerEditor.ShowCategorizedAddMenu(
                    rect,
                    actions,
                    EventActionRegistry.AuthorContext.ParallelChild),
        };

        lists[key] = list;
        return list;
    }

    static float ElementHeight(SerializedProperty actions, int index)
    {
        if (index < 0 || index >= actions.arraySize)
            return EditorGUIUtility.singleLineHeight + 4f;

        var element = actions.GetArrayElementAtIndex(index);
        var leafProp = GetLeafProperty(element, out _);
        float line = EditorGUIUtility.singleLineHeight;
        float height = line + Gap; // header: name + delay + ping

        bool fold = IsFoldedOut(element);
        if (fold && leafProp != null)
        {
            // Draw leaf without its root foldout label overhead via PropertyField includeChildren.
            height += EditorGUI.GetPropertyHeight(leafProp, true) + Gap;
        }
        else if (fold && element.managedReferenceValue == null)
        {
            height += line + Gap;
        }

        return height + 4f;
    }

    void DrawChild(SerializedProperty actions, Rect rect, int index)
    {
        if (index < 0 || index >= actions.arraySize)
            return;

        var element = actions.GetArrayElementAtIndex(index);
        rect.y += 1f;
        rect.xMin += EventRunnerEditor.ReorderFoldoutPad;

        float line = EditorGUIUtility.singleLineHeight;
        var delayLabel = new Rect(rect.x, rect.y, 70f, line);
        var delayField = new Rect(delayLabel.xMax + Gap, rect.y, DelayWidth, line);
        var pingRect = new Rect(rect.xMax - PingWidth, rect.y, PingWidth, line);
        var foldRect = new Rect(
            delayField.xMax + Gap,
            rect.y,
            pingRect.x - (delayField.xMax + Gap) - Gap,
            line);

        float delay = EventActionEditorUtility.GetStartDelay(element.managedReferenceValue as EventAction);
        EditorGUI.LabelField(delayLabel, "Delay (s)");
        EditorGUI.BeginChangeCheck();
        float newDelay = EditorGUI.FloatField(delayField, delay);
        if (EditorGUI.EndChangeCheck())
            ApplyDelay(element, Mathf.Max(0f, newDelay));

        string summary = "Null";
        if (element.managedReferenceValue is EventAction action)
            summary = $"{index}. {EventActionEditorUtility.Summary(action)}";

        bool fold = IsFoldedOut(element);
        fold = EditorGUI.Foldout(foldRect, fold, summary, true);
        SetFoldedOut(element, fold);

        if (GUI.Button(pingRect, "Ping") && element.managedReferenceValue is EventAction pingAction)
            EventActionEditorUtility.PingActionRefs(pingAction);

        if (!fold)
            return;

        var leafProp = GetLeafProperty(element, out bool isWrapped);
        float y = rect.y + line + Gap;
        if (leafProp == null)
        {
            EditorGUI.LabelField(new Rect(rect.x + 12f, y, rect.width - 12f, line), "(empty child)");
            return;
        }

        // Indent leaf fields; hide DelayedStart's own startDelay/action list by drawing leaf only.
        var leafRect = new Rect(rect.x + 12f, y, rect.width - 12f, EditorGUI.GetPropertyHeight(leafProp, true));
        EditorGUI.PropertyField(leafRect, leafProp, new GUIContent(isWrapped ? "Action" : " "), true);
    }

    static SerializedProperty GetLeafProperty(SerializedProperty element, out bool isWrapped)
    {
        isWrapped = false;
        if (element.managedReferenceValue is DelayedStartAction)
        {
            isWrapped = true;
            return element.FindPropertyRelative("action");
        }

        return element;
    }

    static void ApplyDelay(SerializedProperty element, float delay)
    {
        var so = element.serializedObject;
        so.Update();

        if (element.managedReferenceValue is DelayedStartAction)
        {
            if (delay <= 0.0001f)
            {
                var actionProp = element.FindPropertyRelative("action");
                object child = actionProp != null ? actionProp.managedReferenceValue : null;
                element.managedReferenceValue = child;
            }
            else
            {
                var delayProp = element.FindPropertyRelative("startDelay");
                if (delayProp != null)
                    delayProp.floatValue = delay;
            }
        }
        else if (delay > 0.0001f)
        {
            var child = element.managedReferenceValue as EventAction;
            element.managedReferenceValue = new DelayedStartAction
            {
                startDelay = delay,
                action = child,
            };
        }

        so.ApplyModifiedProperties();
    }

    static bool IsFoldedOut(SerializedProperty element)
    {
        return SessionState.GetBool(DelayPrefKey + element.propertyPath, true);
    }

    static void SetFoldedOut(SerializedProperty element, bool value)
    {
        SessionState.SetBool(DelayPrefKey + element.propertyPath, value);
    }
}
