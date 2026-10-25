using UnityEditor;

[CustomEditor(typeof(EnemyAI))]
public class EnemyAIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty canDefend = serializedObject.FindProperty("canDefend");
        SerializedProperty meleeAttack = serializedObject.FindProperty("meleeAttack");
        SerializedProperty longRange = serializedObject.FindProperty("longRangeAttacker");
        SerializedProperty hybridEngage = serializedObject.FindProperty("hybridEngage");
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "defendCoverage" && canDefend != null && !canDefend.boolValue)
                continue;

            if (iterator.name == "meleeCloseDistance"
                && meleeAttack != null
                && meleeAttack.enumValueIndex != (int)EnemyAI.MeleeAttackMode.OnlyWhenClose)
                continue;

            if (iterator.name == "longRangeFire" && longRange != null && !longRange.boolValue)
                continue;

            bool mixesWeapons = longRange != null && longRange.boolValue
                && meleeAttack != null
                && meleeAttack.enumValueIndex != (int)EnemyAI.MeleeAttackMode.No;
            bool closesIn = mixesWeapons
                && hybridEngage != null
                && hybridEngage.enumValueIndex == (int)EnemyAI.HybridEngage.CloseIn;

            if (iterator.name == "hybridEngage" && !mixesWeapons)
                continue;

            if (iterator.name == "closeInMeleeShare" && !closesIn)
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
