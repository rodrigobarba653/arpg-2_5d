using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyReactionChart.Row))]
public class EnemyReactionRowDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = ShowsDefend(property) ? 5 : 4;
        return EditorGUIUtility.singleLineHeight * lines
            + EditorGUIUtility.standardVerticalSpacing * (lines - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        bool showDefend = ShowsDefend(property);
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, line);

        EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
        row.y += line + gap;

        EnemyReactionChart.Row current = Read(property);
        DrawShare(ref row, line, gap, property, "Press", 0, ref current, showDefend);
        DrawShare(ref row, line, gap, property, "Step Back", 1, ref current, showDefend);
        DrawShare(ref row, line, gap, property, "Hold", 2, ref current, showDefend);
        if (showDefend)
            DrawShare(ref row, line, gap, property, "Defend", 3, ref current, showDefend);

        EditorGUI.EndProperty();
    }

    static void DrawShare(ref Rect row, float line, float gap, SerializedProperty property, string name, int index, ref EnemyReactionChart.Row current, bool showDefend)
    {
        int value = ValueAt(current, index);
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.IntSlider(row, name, value, 0, 10);
        if (EditorGUI.EndChangeCheck())
        {
            EnemyReactionChart.SetShare(ref current, index, next, showDefend);
            Write(property, current);
        }

        row.y += line + gap;
    }

    static bool ShowsDefend(SerializedProperty property)
    {
        EnemyReactionChart chart = property.serializedObject.targetObject as EnemyReactionChart;
        return chart != null && chart.ShowsDefend;
    }

    static EnemyReactionChart.Row Read(SerializedProperty property)
    {
        return new EnemyReactionChart.Row
        {
            press = property.FindPropertyRelative("press").intValue,
            stepBack = property.FindPropertyRelative("stepBack").intValue,
            hold = property.FindPropertyRelative("hold").intValue,
            defend = property.FindPropertyRelative("defend").intValue
        };
    }

    static void Write(SerializedProperty property, EnemyReactionChart.Row row)
    {
        property.FindPropertyRelative("press").intValue = row.press;
        property.FindPropertyRelative("stepBack").intValue = row.stepBack;
        property.FindPropertyRelative("hold").intValue = row.hold;
        property.FindPropertyRelative("defend").intValue = row.defend;
        property.serializedObject.ApplyModifiedProperties();
    }

    static int ValueAt(EnemyReactionChart.Row row, int index)
    {
        switch (index)
        {
            case 0: return row.press;
            case 1: return row.stepBack;
            case 2: return row.hold;
            default: return row.defend;
        }
    }
}
