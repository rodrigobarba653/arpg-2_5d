using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyMotor))]
public class EnemyMotorEditor : Editor
{
    static readonly string[] Labels =
    {
        "Up (North)",
        "Up Right",
        "Right",
        "Down Right",
        "Down",
        "Down Left",
        "Left",
        "Up Left"
    };

    static readonly Dictionary<int, float> previewedYaw = new Dictionary<int, float>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EnemyMotor motor = (EnemyMotor)target;
        int current = IndexFromYaw(motor.transform.eulerAngles.y);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup("Placed Facing", current, Labels);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(motor.transform, "Set Enemy Facing");
        Vector3 euler = motor.transform.eulerAngles;
        euler.y = next * 45f;
        motor.transform.eulerAngles = euler;
        Preview(motor, force: true);
    }

    void OnSceneGUI()
    {
        EnemyMotor motor = (EnemyMotor)target;
        if (motor == null)
            return;

        Vector3 forward = motor.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Handles.color = new Color(1f, 0.55f, 0.1f, 0.95f);
        Handles.ArrowHandleCap(0, motor.transform.position + Vector3.up * 0.2f, Quaternion.LookRotation(forward), 1.2f, EventType.Repaint);

        Preview(motor, force: false);
    }

    static void Preview(EnemyMotor motor, bool force)
    {
        if (Application.isPlaying || motor == null)
            return;

        float yaw = motor.transform.eulerAngles.y;
        int id = motor.GetInstanceID();
        if (!force && previewedYaw.TryGetValue(id, out float shown) && Mathf.Abs(Mathf.DeltaAngle(shown, yaw)) < 0.5f)
            return;

        previewedYaw[id] = yaw;

        EnemyTopDownAnimDriver driver = motor.GetComponentInChildren<EnemyTopDownAnimDriver>();
        Animator animator = driver != null ? driver.animator : motor.GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        Vector3 forward = motor.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;
        forward.Normalize();

        Object controller = animator.runtimeAnimatorController;
        bool controllerWasDirty = controller != null && EditorUtility.IsDirty(controller);

        animator.SetFloat("MoveX", forward.x);
        animator.SetFloat("MoveY", forward.z);
        animator.Update(0f);

        if (controller != null && !controllerWasDirty)
            EditorUtility.ClearDirty(controller);
    }

    static int IndexFromYaw(float yaw)
    {
        float wrapped = Mathf.Repeat(yaw, 360f);
        return Mathf.RoundToInt(wrapped / 45f) % 8;
    }
}
