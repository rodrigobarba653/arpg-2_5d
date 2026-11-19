using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(DialogueAsset))]
[CanEditMultipleObjects]
public class DialogueAssetEditor : Editor
{
    static readonly Type[] NodeTypes =
    {
        typeof(DialogueLineNode),
        typeof(DialogueChoiceNode),
        typeof(DialogueConditionNode),
        typeof(DialogueEndNode),
    };

    SerializedProperty dialogueId;
    SerializedProperty entryNodeId;
    SerializedProperty defaultTypewriterCharsPerSecond;
    SerializedProperty lockGameplay;
    SerializedProperty overrideChoicePresentation;
    SerializedProperty choicePresentation;
    SerializedProperty nodes;
    ReorderableList nodeList;
    readonly List<DialogueValidationIssue> issues = new List<DialogueValidationIssue>();
    bool issuesReady;

    void OnEnable()
    {
        dialogueId = serializedObject.FindProperty("dialogueId");
        entryNodeId = serializedObject.FindProperty("entryNodeId");
        defaultTypewriterCharsPerSecond = serializedObject.FindProperty("defaultTypewriterCharsPerSecond");
        lockGameplay = serializedObject.FindProperty("lockGameplay");
        overrideChoicePresentation = serializedObject.FindProperty("overrideChoicePresentation");
        choicePresentation = serializedObject.FindProperty("choicePresentation");
        nodes = serializedObject.FindProperty("nodes");
        issuesReady = false;

        nodeList = new ReorderableList(serializedObject, nodes, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Nodes"),
            elementHeightCallback = GetElementHeight,
            drawElementCallback = DrawElement,
            onAddDropdownCallback = ShowAddMenu,
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        bool pulled = serializedObject.hasModifiedProperties;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Dialogue Editor", GUILayout.Height(24f)))
            {
                if (!serializedObject.isEditingMultipleObjects)
                    DialogueEditorWindow.OpenAsset(target as DialogueAsset);
                else
                    DialogueEditorWindow.Open();
            }
        }
        EditorGUILayout.HelpBox(
            "Prefer the Dialogue Editor for writing conversations (lines, speakers, choices, consequences). " +
            "This Inspector remains the low-level node view.",
            MessageType.Info);

        if (serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.LabelField("Dialogue", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dialogueId);
            EditorGUILayout.PropertyField(entryNodeId);
            EditorGUILayout.PropertyField(defaultTypewriterCharsPerSecond);
            EditorGUILayout.PropertyField(lockGameplay);
            EditorGUILayout.PropertyField(overrideChoicePresentation);
            EditorGUILayout.HelpBox("Select a single asset to edit nodes and validation.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField("Dialogue", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dialogueId);
        DrawEntryRow();
        EditorGUILayout.PropertyField(defaultTypewriterCharsPerSecond);
        EditorGUILayout.PropertyField(lockGameplay);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Choice presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(overrideChoicePresentation);
        if (overrideChoicePresentation.boolValue)
            EditorGUILayout.PropertyField(choicePresentation, true);

        EditorGUILayout.Space(6f);
        nodeList.DoLayoutList();

        bool edited = serializedObject.ApplyModifiedProperties();
        if (!issuesReady || pulled || edited)
            RefreshIssues();
        DrawIssues();
    }

    void DrawEntryRow()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(entryNodeId);
        if (EditorGUILayout.DropdownButton(new GUIContent("Ids"), FocusType.Passive, GUILayout.Width(48f)))
            ShowIdMenu(entryNodeId.propertyPath, entryNodeId.stringValue, allowEmpty: false);
        EditorGUILayout.EndHorizontal();
    }

    void DrawIssues()
    {
        EditorGUILayout.Space(6f);
        int errors = 0;
        int warnings = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == DialogueValidationSeverity.Error)
                errors++;
            else if (issues[i].Severity == DialogueValidationSeverity.Warning)
                warnings++;
        }

        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No issues.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(errors + " errors, " + warnings + " warnings");
        for (int i = 0; i < issues.Count; i++)
        {
            DialogueValidationIssue issue = issues[i];
            EditorGUILayout.HelpBox(
                issue.Severity + " · " + issue.Location + "\n" + issue.Problem + "\n" + issue.Correction,
                issue.Severity == DialogueValidationSeverity.Error
                    ? MessageType.Error
                    : issue.Severity == DialogueValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info);
        }
    }

    void RefreshIssues()
    {
        DialogueAssetValidator.Collect(target as DialogueAsset, issues);
        issuesReady = true;
    }

    void ShowAddMenu(Rect buttonRect, ReorderableList list)
    {
        var menu = new GenericMenu();
        for (int i = 0; i < NodeTypes.Length; i++)
        {
            Type type = NodeTypes[i];
            menu.AddItem(new GUIContent(PrettyType(type)), false, () => AddNode(type));
        }
        menu.DropDown(buttonRect);
    }

    void AddNode(Type type)
    {
        serializedObject.Update();
        int index = nodes.arraySize;
        nodes.arraySize++;
        SerializedProperty element = nodes.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(type);
        if (element.managedReferenceValue is DialogueNode node && string.IsNullOrEmpty(node.nodeId))
            node.nodeId = "node_" + index;
        serializedObject.ApplyModifiedProperties();
        issuesReady = false;
    }

    void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = nodes.GetArrayElementAtIndex(index);
        rect.y += 2f;
        float line = EditorGUIUtility.singleLineHeight;

        if (element.managedReferenceValue == null)
        {
            EditorGUI.LabelField(rect, "[" + index + "] Null node");
            return;
        }

        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, line), ColorFor(element.managedReferenceValue));
        var foldRect = new Rect(rect.x + 8f, rect.y, rect.width - 8f, line);
        bool expanded = EditorGUI.Foldout(foldRect, element.isExpanded, Header(element), true);
        if (expanded != element.isExpanded)
            element.isExpanded = expanded;
        if (!expanded)
            return;

        float y = rect.y + line + 2f;
        SerializedProperty iterator = element.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enter = true;
        while (iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator, end))
        {
            enter = false;
            float height = EditorGUI.GetPropertyHeight(iterator, true);
            var row = new Rect(rect.x + 8f, y, rect.width - 8f, height);
            if (iterator.name == "nextNodeId" || iterator.name == "fallbackNodeId"
                || iterator.name == "trueNodeId" || iterator.name == "falseNodeId")
                DrawDestination(row, iterator);
            else
                EditorGUI.PropertyField(row, iterator, true);
            y += height + 2f;
        }
    }

    float GetElementHeight(int index)
    {
        SerializedProperty element = nodes.GetArrayElementAtIndex(index);
        float line = EditorGUIUtility.singleLineHeight;
        if (element.managedReferenceValue == null)
            return line + 8f;

        float height = line + 8f;
        if (!element.isExpanded)
            return height;

        SerializedProperty iterator = element.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enter = true;
        while (iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator, end))
        {
            enter = false;
            height += EditorGUI.GetPropertyHeight(iterator, true) + 2f;
        }
        return height;
    }

    void DrawDestination(Rect row, SerializedProperty property)
    {
        row.height = EditorGUIUtility.singleLineHeight;
        const float buttonWidth = 48f;
        var field = row;
        field.width = Mathf.Max(40f, row.width - buttonWidth - 4f);
        string tip = property.name == "fallbackNodeId"
            ? "Node id used when no option is visible. Empty can end the conversation."
            : property.name == "trueNodeId"
                ? "Destination when every condition passes. Empty ends the conversation (Completed)."
                : property.name == "falseNodeId"
                    ? "Destination when a condition fails. Empty ends the conversation (Completed)."
                    : "Node id. Empty ends the conversation (Completed).";
        EditorGUI.PropertyField(field, property, new GUIContent(property.displayName, tip));
        var button = new Rect(field.xMax + 4f, row.y, buttonWidth, row.height);
        if (EditorGUI.DropdownButton(button, new GUIContent("Ids"), FocusType.Passive))
            ShowIdMenu(property.propertyPath, property.stringValue, allowEmpty: true);
    }

    void ShowIdMenu(string propertyPath, string current, bool allowEmpty)
    {
        var menu = new GenericMenu();
        if (allowEmpty)
        {
            menu.AddItem(new GUIContent("(Completed)"), string.IsNullOrEmpty(current), () => AssignString(propertyPath, ""));
            menu.AddSeparator("");
        }

        bool listedCurrent = string.IsNullOrEmpty(current);
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty id = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("nodeId");
            if (id == null || string.IsNullOrEmpty(id.stringValue))
                continue;
            string value = id.stringValue;
            if (value == current)
                listedCurrent = true;
            menu.AddItem(new GUIContent(value), value == current, () => AssignString(propertyPath, value));
        }

        if (!listedCurrent)
            menu.AddDisabledItem(new GUIContent(current + " (missing)"));
        menu.ShowAsContext();
    }

    void AssignString(string propertyPath, string value)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property != null)
            property.stringValue = value;
        serializedObject.ApplyModifiedProperties();
        issuesReady = false;
    }

    string Header(SerializedProperty element)
    {
        string id = "";
        SerializedProperty idProperty = element.FindPropertyRelative("nodeId");
        if (idProperty != null)
            id = idProperty.stringValue;
        string type = PrettyType(element.managedReferenceValue.GetType());
        string title = string.IsNullOrEmpty(id) ? type + " (no id)" : type + " '" + id + "'";
        if (!string.IsNullOrEmpty(id) && entryNodeId != null && id == entryNodeId.stringValue)
            return "Entry · " + title;
        return title;
    }

    static Color ColorFor(object node)
    {
        if (node is DialogueLineNode)
            return new Color(0.35f, 0.62f, 0.95f);
        if (node is DialogueChoiceNode)
            return new Color(0.93f, 0.72f, 0.28f);
        if (node is DialogueConditionNode)
            return new Color(0.45f, 0.78f, 0.72f);
        if (node is DialogueEndNode)
            return new Color(0.62f, 0.62f, 0.66f);
        return new Color(0.8f, 0.4f, 0.4f);
    }

    static string PrettyType(Type type)
    {
        if (type == typeof(DialogueLineNode))
            return "Line";
        if (type == typeof(DialogueChoiceNode))
            return "Choice";
        if (type == typeof(DialogueConditionNode))
            return "Condition";
        if (type == typeof(DialogueEndNode))
            return "End";
        return type.Name;
    }
}
