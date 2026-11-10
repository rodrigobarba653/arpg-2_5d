using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(DialogueAsset))]
public class DialogueAssetEditor : Editor
{
    static readonly Type[] NodeTypes =
    {
        typeof(DialogueLineNode),
        typeof(DialogueChoiceNode),
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

    void OnEnable()
    {
        dialogueId = serializedObject.FindProperty("dialogueId");
        entryNodeId = serializedObject.FindProperty("entryNodeId");
        defaultTypewriterCharsPerSecond = serializedObject.FindProperty("defaultTypewriterCharsPerSecond");
        lockGameplay = serializedObject.FindProperty("lockGameplay");
        overrideChoicePresentation = serializedObject.FindProperty("overrideChoicePresentation");
        choicePresentation = serializedObject.FindProperty("choicePresentation");
        nodes = serializedObject.FindProperty("nodes");

        nodeList = new ReorderableList(serializedObject, nodes, true, true, true, true)
        {
            drawHeaderCallback = r => EditorGUI.LabelField(r, "Nodes (SerializeReference)"),
            elementHeightCallback = GetElementHeight,
            drawElementCallback = DrawElement,
            onAddDropdownCallback = ShowAddMenu,
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(dialogueId);
        EditorGUILayout.PropertyField(entryNodeId);
        EditorGUILayout.PropertyField(defaultTypewriterCharsPerSecond);
        EditorGUILayout.PropertyField(lockGameplay);
        EditorGUILayout.PropertyField(overrideChoicePresentation);
        if (overrideChoicePresentation.boolValue)
            EditorGUILayout.PropertyField(choicePresentation);
        EditorGUILayout.Space(6);
        nodeList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    void ShowAddMenu(Rect buttonRect, ReorderableList list)
    {
        var menu = new GenericMenu();
        for (int i = 0; i < NodeTypes.Length; i++)
        {
            var type = NodeTypes[i];
            menu.AddItem(new GUIContent(type.Name), false, () =>
            {
                serializedObject.Update();
                int index = nodes.arraySize;
                nodes.arraySize++;
                var element = nodes.GetArrayElementAtIndex(index);
                element.managedReferenceValue = Activator.CreateInstance(type);
                if (element.managedReferenceValue is DialogueNode node && string.IsNullOrEmpty(node.nodeId))
                    node.nodeId = $"node_{index}";
                serializedObject.ApplyModifiedProperties();
            });
        }
        menu.DropDown(buttonRect);
    }

    void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = nodes.GetArrayElementAtIndex(index);
        rect.y += 2;
        EditorGUI.PropertyField(rect, element, new GUIContent(GetNodeLabel(element)), true);
    }

    float GetElementHeight(int index)
    {
        var element = nodes.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, true) + 6f;
    }

    static string GetNodeLabel(SerializedProperty element)
    {
        if (element?.managedReferenceValue is DialogueNode node)
            return node.DisplayName;
        return "Node";
    }
}
