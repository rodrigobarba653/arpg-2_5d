using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows Destination Mode and only the fields that mode uses.
/// </summary>
[CustomPropertyDrawer(typeof(MoveCharacterAction))]
public sealed class MoveCharacterActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float line = EditorGUIUtility.singleLineHeight + 2f;
        float h = line; // foldout

        SerializedProperty character = property.FindPropertyRelative("character");
        if (character != null)
            h += EditorGUI.GetPropertyHeight(character, true) + 2f;

        h += line; // destinationMode

        var mode = property.FindPropertyRelative("destinationMode");
        int modeIndex = mode != null ? mode.enumValueIndex : 0;
        if (modeIndex == (int)MoveCharacterAction.DestinationMode.Marker)
            h += line; // destination
        else if (modeIndex == (int)MoveCharacterAction.DestinationMode.RelativeOffset)
            h += line; // relativeOffset
        else
            h += line; // worldPosition

        h += line; // speed
        h += line; // stopDistance
        h += line; // timeoutSeconds
        h += line; // faceMovementDirection
        h += line; // holdPositionUntilEventEnds
        h += 6f;
        return h;
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

        SerializedProperty character = property.FindPropertyRelative("character");
        SerializedProperty destinationMode = property.FindPropertyRelative("destinationMode");
        SerializedProperty destination = property.FindPropertyRelative("destination");
        SerializedProperty worldPosition = property.FindPropertyRelative("worldPosition");
        SerializedProperty relativeOffset = property.FindPropertyRelative("relativeOffset");
        SerializedProperty speed = property.FindPropertyRelative("speed");
        SerializedProperty stopDistance = property.FindPropertyRelative("stopDistance");
        SerializedProperty timeoutSeconds = property.FindPropertyRelative("timeoutSeconds");
        SerializedProperty faceMovementDirection = property.FindPropertyRelative("faceMovementDirection");
        SerializedProperty hold = property.FindPropertyRelative("holdPositionUntilEventEnds");

        if (character != null)
        {
            float ch = EditorGUI.GetPropertyHeight(character, true);
            EditorGUI.PropertyField(new Rect(position.x, y, full, ch), character, true);
            y += ch + 2f;
        }

        if (destinationMode != null)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, full, line),
                destinationMode,
                new GUIContent("Destination Mode"));
            y += line + 2f;
        }

        int modeIndex = destinationMode != null ? destinationMode.enumValueIndex : 0;
        if (modeIndex == (int)MoveCharacterAction.DestinationMode.Marker)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, full, line),
                destination,
                new GUIContent("Destination"));
            y += line + 2f;
        }
        else if (modeIndex == (int)MoveCharacterAction.DestinationMode.RelativeOffset)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, full, line),
                relativeOffset,
                new GUIContent("Relative Offset", "Added to the character's position when the move starts (e.g. X=2)."));
            y += line + 2f;
        }
        else
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, full, line),
                worldPosition,
                new GUIContent("World Position"));
            y += line + 2f;
        }

        DrawLine(ref y, position.x, full, line, speed);
        DrawLine(ref y, position.x, full, line, stopDistance);
        DrawLine(ref y, position.x, full, line, timeoutSeconds);
        DrawLine(ref y, position.x, full, line, faceMovementDirection);
        DrawLine(ref y, position.x, full, line, hold);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    static void DrawLine(ref float y, float x, float width, float line, SerializedProperty prop)
    {
        if (prop == null)
            return;
        EditorGUI.PropertyField(new Rect(x, y, width, line), prop);
        y += line + 2f;
    }
}
