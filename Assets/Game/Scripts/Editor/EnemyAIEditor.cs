using UnityEditor;

[CustomEditor(typeof(EnemyAI))]
public class EnemyAIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty canDefend = serializedObject.FindProperty("canDefend");
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "defendCoverage" && canDefend != null && !canDefend.boolValue)
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
