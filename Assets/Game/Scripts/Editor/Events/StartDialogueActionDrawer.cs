using UnityEditor;
using UnityEngine;

/// <summary>
/// Simple Start Dialogue UI. Conversation structure lives on the Dialogue Asset / Dialogue Editor.
/// Legacy npcAnchor / npcSpeakerId / dialogueByCharacter stay serialized for Sprint fixtures.
/// </summary>
[CustomPropertyDrawer(typeof(StartDialogueAction))]
public sealed class StartDialogueActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float line = EditorGUIUtility.singleLineHeight + 2f;
        float height = line; // foldout
        height += line; // asset
        height += line; // wait
        height += line; // cancel
        height += line; // progression
        height += line; // auto
        height += line; // help
        height += line; // advanced foldout
        SerializedProperty byCharacter = property.FindPropertyRelative("dialogueByCharacter");
        if (byCharacter != null && byCharacter.isExpanded)
        {
            height += line; // npc speaker
            height += line; // npc anchor
            height += EditorGUI.GetPropertyHeight(byCharacter, true) + 2f;
        }
        height += 10f;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            label,
            true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = position.y + EditorGUIUtility.singleLineHeight + 2f;
        float line = EditorGUIUtility.singleLineHeight;
        float full = position.width;

        SerializedProperty assetProp = property.FindPropertyRelative("dialogueAsset");
        SerializedProperty waitUntilFinished = property.FindPropertyRelative("waitUntilFinished");
        SerializedProperty allowCancel = property.FindPropertyRelative("allowCancel");
        SerializedProperty progressionMode = property.FindPropertyRelative("progressionMode");
        SerializedProperty autoAdvanceHoldSeconds = property.FindPropertyRelative("autoAdvanceHoldSeconds");
        SerializedProperty npcAnchor = property.FindPropertyRelative("npcAnchor");
        SerializedProperty npcSpeakerId = property.FindPropertyRelative("npcSpeakerId");
        SerializedProperty byCharacter = property.FindPropertyRelative("dialogueByCharacter");

        Rect r = new Rect(position.x, y, full, line);
        EditorGUI.PropertyField(r, assetProp, new GUIContent("Dialogue Asset"));
        y += line + 2f;

        r = new Rect(position.x, y, full, line);
        EditorGUI.PropertyField(r, waitUntilFinished);
        y += line + 2f;

        r = new Rect(position.x, y, full, line);
        EditorGUI.PropertyField(r, allowCancel);
        y += line + 2f;

        r = new Rect(position.x, y, full, line);
        EditorGUI.PropertyField(r, progressionMode);
        y += line + 2f;

        r = new Rect(position.x, y, full, line);
        EditorGUI.PropertyField(r, autoAdvanceHoldSeconds);
        y += line + 2f;

        r = new Rect(position.x, y, full, line);
        EditorGUI.LabelField(r, "Speakers / portraits / NPC anchors are authored in Dialogue Editor (per line).", EditorStyles.miniLabel);
        y += line + 2f;

        if (byCharacter != null)
        {
            byCharacter.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, y, full, line),
                byCharacter.isExpanded,
                "Advanced (legacy / fixtures)",
                true);
            y += line + 2f;

            if (byCharacter.isExpanded)
            {
                r = new Rect(position.x, y, full, line);
                EditorGUI.PropertyField(r, npcSpeakerId, new GUIContent("Legacy Npc Speaker Id"));
                y += line + 2f;
                r = new Rect(position.x, y, full, line);
                EditorGUI.PropertyField(r, npcAnchor, new GUIContent("Legacy Npc Anchor"));
                y += line + 2f;
                r = new Rect(position.x, y, full, EditorGUI.GetPropertyHeight(byCharacter, true));
                EditorGUI.PropertyField(r, byCharacter, new GUIContent("Dialogue By Character"), true);
            }
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
