using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueConsequenceListAttribute))]
public class DialogueConsequenceListDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property == null || !property.isArray)
            return EditorGUIUtility.singleLineHeight;
        return Build(property).GetHeight();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null || !property.isArray)
        {
            EditorGUI.LabelField(position, label, new GUIContent("Not a list"));
            return;
        }

        Build(property).DoList(position);
    }

    static ReorderableList Build(SerializedProperty property)
    {
        var list = new ReorderableList(property.serializedObject, property, true, true, true, true)
        {
            drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Consequences (once per confirm)"),
            elementHeightCallback = index => ElementHeight(property, index),
            drawElementCallback = (rect, index, active, focused) => DrawElement(property, rect, index),
            onAddDropdownCallback = (rect, source) => ShowAddMenu(rect, source.serializedProperty),
        };
        return list;
    }

    static void ShowAddMenu(Rect buttonRect, SerializedProperty list)
    {
        var serializedObject = list.serializedObject;
        string path = list.propertyPath;
        var menu = new GenericMenu();
        foreach (var entry in EventActionRegistry.ForContext(
                     EventActionRegistry.AuthorContext.DialogueConsequence))
        {
            Type type = entry.Type;
            string menuPath = $"{entry.Category}/{entry.MenuName}";
            menu.AddItem(new GUIContent(menuPath), false, () => Add(serializedObject, path, type));
        }

        menu.DropDown(buttonRect);
    }

    static void Add(SerializedObject serializedObject, string path, Type type)
    {
        serializedObject.Update();
        SerializedProperty list = serializedObject.FindProperty(path);
        if (list == null)
            return;

        int index = list.arraySize;
        list.arraySize++;
        SerializedProperty element = list.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(type);
        serializedObject.ApplyModifiedProperties();
    }

    static float ElementHeight(SerializedProperty list, int index)
    {
        if (index < 0 || index >= list.arraySize)
            return EditorGUIUtility.singleLineHeight + 4f;

        SerializedProperty element = list.GetArrayElementAtIndex(index);
        if (element.managedReferenceValue == null)
            return EditorGUIUtility.singleLineHeight + 6f;

        return EditorGUI.GetPropertyHeight(element, true) + 6f;
    }

    static void DrawElement(SerializedProperty list, Rect rect, int index)
    {
        if (index < 0 || index >= list.arraySize)
            return;

        SerializedProperty element = list.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);

        string caption = "Null";
        if (element.managedReferenceValue is EventAction action)
            caption = EventActionEditorUtility.Summary(action);
        else if (element.managedReferenceValue != null)
            caption = element.managedReferenceValue.GetType().Name;

        EditorGUI.PropertyField(rect, element, new GUIContent("[" + index + "] " + caption), true);
    }
}
