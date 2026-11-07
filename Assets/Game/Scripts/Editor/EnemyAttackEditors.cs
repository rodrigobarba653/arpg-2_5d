using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyCombatController))]
public class EnemyCombatControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EnemyAI ai = ((EnemyCombatController)target).GetComponent<EnemyAI>();
        if (ai != null && !ai.UsesMelee)
        {
            EditorGUILayout.HelpBox("Melee Attack is No on Enemy AI.", MessageType.None);
            return;
        }

        if (ai != null && ai.meleeAttack == EnemyAI.MeleeAttackMode.OnlyWhenClose)
            EditorGUILayout.HelpBox("Range is Melee Close Distance on Enemy AI.", MessageType.None);

        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(EnemyRangedCombatController))]
public class EnemyRangedCombatControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!LongRangeOn((Component)target))
        {
            EditorGUILayout.HelpBox("Turn on Long Range Attacker on Enemy AI.", MessageType.None);
            return;
        }

        DrawDefaultInspector();
    }

    public static bool LongRangeOn(Component component)
    {
        EnemyAI ai = component.GetComponent<EnemyAI>();
        return ai != null && ai.longRangeAttacker;
    }
}

[CustomEditor(typeof(DirectionalShootPoint))]
public class DirectionalShootPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (!EnemyRangedCombatControllerEditor.LongRangeOn((Component)target))
        {
            EditorGUILayout.HelpBox("Turn on Long Range Attacker on Enemy AI.", MessageType.None);
            return;
        }

        DrawDefaultInspector();
    }
}
