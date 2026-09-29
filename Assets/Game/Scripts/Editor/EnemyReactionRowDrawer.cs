using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyReactionChart.Row))]
public class EnemyReactionRowDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 4;
        if (ShowsShoot(property))
            lines++;
        if (ShowsDefend(property))
            lines++;
        return EditorGUIUtility.singleLineHeight * lines
            + EditorGUIUtility.standardVerticalSpacing * (lines - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        bool showDefend = ShowsDefend(property);
        bool showShoot = ShowsShoot(property);
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, line);

        EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
        row.y += line + gap;

        EnemyReactionChart.Row current = Read(property);
        DrawShare(ref row, line, gap, property, "Press", 0, ref current, showDefend, showShoot);
        DrawShare(ref row, line, gap, property, "Step Back", 1, ref current, showDefend, showShoot);
        DrawShare(ref row, line, gap, property, "Hold", 2, ref current, showDefend, showShoot);
        if (showShoot)
            DrawShare(ref row, line, gap, property, "Shoot", 4, ref current, showDefend, showShoot);
        if (showDefend)
            DrawShare(ref row, line, gap, property, "Defend", 3, ref current, showDefend, showShoot);

        EditorGUI.EndProperty();
    }

    static void DrawShare(ref Rect row, float line, float gap, SerializedProperty property, string name, int index, ref EnemyReactionChart.Row current, bool showDefend, bool showShoot)
    {
        int value = ValueAt(current, index);
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.IntSlider(row, name, value, 0, 10);
        if (EditorGUI.EndChangeCheck())
        {
            EnemyReactionChart.SetShare(ref current, index, next, showDefend, showShoot);
            Write(property, current);
        }

        row.y += line + gap;
    }

    static bool ShowsDefend(SerializedProperty property)
    {
        EnemyReactionChart chart = property.serializedObject.targetObject as EnemyReactionChart;
        return chart != null && chart.ShowsDefend;
    }

    static bool ShowsShoot(SerializedProperty property)
    {
        EnemyReactionChart chart = property.serializedObject.targetObject as EnemyReactionChart;
        return chart != null && chart.ShowsShoot;
    }

    static EnemyReactionChart.Row Read(SerializedProperty property)
    {
        return new EnemyReactionChart.Row
        {
            press = property.FindPropertyRelative("press").intValue,
            stepBack = property.FindPropertyRelative("stepBack").intValue,
            hold = property.FindPropertyRelative("hold").intValue,
            defend = property.FindPropertyRelative("defend").intValue,
            shoot = IntOrZero(property, "shoot")
        };
    }

    static void Write(SerializedProperty property, EnemyReactionChart.Row row)
    {
        property.FindPropertyRelative("press").intValue = row.press;
        property.FindPropertyRelative("stepBack").intValue = row.stepBack;
        property.FindPropertyRelative("hold").intValue = row.hold;
        property.FindPropertyRelative("defend").intValue = row.defend;
        SerializedProperty shoot = property.FindPropertyRelative("shoot");
        if (shoot != null)
            shoot.intValue = row.shoot;
        property.serializedObject.ApplyModifiedProperties();
    }

    static int IntOrZero(SerializedProperty property, string name)
    {
        SerializedProperty field = property.FindPropertyRelative(name);
        return field != null ? field.intValue : 0;
    }

    static int ValueAt(EnemyReactionChart.Row row, int index)
    {
        switch (index)
        {
            case 0: return row.press;
            case 1: return row.stepBack;
            case 2: return row.hold;
            case 3: return row.defend;
            default: return row.shoot;
        }
    }
}

[CustomPropertyDrawer(typeof(EnemyReactionChart.CloseRow))]
public class EnemyCloseRowDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 5;
        return EditorGUIUtility.singleLineHeight * lines
            + EditorGUIUtility.standardVerticalSpacing * (lines - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, line);

        EditorGUI.LabelField(row, "When Player Gets Close", EditorStyles.boldLabel);
        row.y += line + gap;

        EnemyReactionChart.CloseRow current = new EnemyReactionChart.CloseRow
        {
            melee = property.FindPropertyRelative("melee").intValue,
            hold = property.FindPropertyRelative("hold").intValue,
            stepBack = property.FindPropertyRelative("stepBack").intValue,
            meleeAndStepBack = property.FindPropertyRelative("meleeAndStepBack").intValue
        };

        Draw(ref row, line, gap, property, "Melee Attack", 0, ref current);
        Draw(ref row, line, gap, property, "Hold", 1, ref current);
        Draw(ref row, line, gap, property, "Step Back", 2, ref current);
        Draw(ref row, line, gap, property, "Melee and Step Back", 3, ref current);
        EditorGUI.EndProperty();
    }

    static void Draw(ref Rect row, float line, float gap, SerializedProperty property, string name, int index, ref EnemyReactionChart.CloseRow current)
    {
        int value = index == 0 ? current.melee : index == 1 ? current.hold : index == 2 ? current.stepBack : current.meleeAndStepBack;
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.IntSlider(row, name, value, 0, 10);
        if (EditorGUI.EndChangeCheck())
        {
            EnemyReactionChart.SetCloseShare(ref current, index, next);
            property.FindPropertyRelative("melee").intValue = current.melee;
            property.FindPropertyRelative("hold").intValue = current.hold;
            property.FindPropertyRelative("stepBack").intValue = current.stepBack;
            property.FindPropertyRelative("meleeAndStepBack").intValue = current.meleeAndStepBack;
            property.serializedObject.ApplyModifiedProperties();
        }

        row.y += line + gap;
    }
}

[CustomEditor(typeof(EnemyReactionChart))]
public class EnemyReactionChartEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnemyReactionChart chart = (EnemyReactionChart)target;
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "playerGetsClose" && !chart.ShowsCloseModule)
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
