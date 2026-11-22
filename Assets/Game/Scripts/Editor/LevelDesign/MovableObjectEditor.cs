using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MovableObject))]
public class MovableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var movable = (MovableObject)target;

        if (movable.persistState && string.IsNullOrEmpty(movable.flagId))
        {
            EditorGUILayout.HelpBox(
                "Persist State is on but Flag Id is empty — nothing will be saved.",
                MessageType.Warning);
        }

        if (movable.activePoseMode == MovableObject.ActivePoseMode.Marker &&
            movable.activePoseMarker == null)
        {
            EditorGUILayout.HelpBox(
                "Active Pose Mode = Marker but Active Pose Marker is None.",
                MessageType.Error);
        }

        if (!movable.movePosition && !movable.moveRotation)
        {
            EditorGUILayout.HelpBox(
                "Both Move Position and Move Rotation are off — the object will not move.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Pose Tools", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Rest (from current)"))
            {
                Undo.RecordObject(movable, "Capture Rest Pose");
                movable.CaptureRestFromCurrent();
                EditorUtility.SetDirty(movable);
            }

            if (GUILayout.Button("Capture Active as Offset"))
            {
                Undo.RecordObject(movable, "Capture Active Offset");
                // Rest must already match the intended A pose in the scene.
                movable.CaptureActiveAsOffsetFromRest();
                EditorUtility.SetDirty(movable);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Active"))
            {
                Undo.RecordObject(movable.transform, "Preview Active Pose");
                if (movable.movingRoot != null && movable.movingRoot != movable.transform)
                    Undo.RecordObject(movable.movingRoot, "Preview Active Pose");
                movable.EditorPreviewActive();
                EditorUtility.SetDirty(movable);
            }

            if (GUILayout.Button("Preview Rest"))
            {
                Undo.RecordObject(movable.transform, "Preview Rest Pose");
                if (movable.movingRoot != null && movable.movingRoot != movable.transform)
                    Undo.RecordObject(movable.movingRoot, "Preview Rest Pose");
                movable.EditorPreviewRest();
                EditorUtility.SetDirty(movable);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                $"Play Mode: IsAtActive={movable.IsAtActive}  IsMoving={movable.IsMoving}",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Move To Active"))
                movable.MoveToActive();
            if (GUILayout.Button("Move To Rest"))
                movable.MoveToRest();
            EditorGUILayout.EndHorizontal();
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
    static void DrawGizmos(MovableObject movable, GizmoType type)
    {
        if (movable == null)
            return;

        Vector3 rest = movable.EditorRestPosition;
        Vector3 active = movable.EditorActivePosition;

        Gizmos.color = new Color(0.2f, 0.85f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(rest, 0.15f);
        Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(active, 0.15f);
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(rest, active);
    }
}
