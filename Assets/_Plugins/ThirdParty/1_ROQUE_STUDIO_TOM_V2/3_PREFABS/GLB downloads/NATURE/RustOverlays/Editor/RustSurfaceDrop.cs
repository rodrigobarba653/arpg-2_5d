using UnityEditor;
using UnityEngine;

namespace Mathra.RustOverlays.Editor
{
    [InitializeOnLoad]
    internal static class RustSurfaceDrop
    {
        static RustSurfaceDrop()
        {
            DragAndDrop.AddDropHandlerV2((DragAndDrop.SceneDropHandler)Drop);
        }

        private static DragAndDropVisualMode Drop(Object picked, Vector3 position, Vector2 mouse,
            Transform defaultParent, bool perform)
        {
            var items = DragAndDrop.objectReferences;
            if (items.Length != 1 || !(items[0] is GameObject prefab) ||
                !AssetDatabase.GetLabels(prefab).ContainsLabel("MathraRustOverlay"))
                return DragAndDropVisualMode.None;

            var target = picked as GameObject;
            if (!target || !target.scene.IsValid())
                return DragAndDropVisualMode.Rejected;

            var mesh = target.GetComponent<MeshFilter>();
            var collider = target.GetComponent<Collider>();
            if ((!mesh || !mesh.sharedMesh) && !collider)
                return DragAndDropVisualMode.Rejected;
            if (!perform)
                return DragAndDropVisualMode.Copy;

            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            RaycastHit hit;
            if (!TrySurface(target, mesh, collider, ray, out hit))
                return DragAndDropVisualMode.Rejected;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Place rust on surface");
            Vector3 normal = hit.normal;
            if (Vector3.Dot(normal, ray.direction) > 0f)
                normal = -normal;
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f
                ? Vector3.forward : Vector3.up;
            instance.transform.SetPositionAndRotation(hit.point + normal * 0.001f,
                Quaternion.LookRotation(-normal, Vector3.ProjectOnPlane(up, normal)));
            Undo.SetTransformParent(instance.transform, target.transform, "Attach rust");
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            Selection.activeGameObject = instance;
            return DragAndDropVisualMode.Copy;
        }

        private static bool TrySurface(GameObject target, MeshFilter mesh, Collider collider,
            Ray ray, out RaycastHit hit)
        {
            if (!mesh || !mesh.sharedMesh || (collider is MeshCollider mc && !mc.convex &&
                mc.sharedMesh == mesh.sharedMesh && mc.enabled))
                return collider.Raycast(ray, out hit, Mathf.Infinity);

            // Match the render mesh even when the prop has only a coarse gameplay collider.
            var probe = new GameObject("Rust placement probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                probe.transform.SetParent(target.transform, false);
                var surface = probe.AddComponent<MeshCollider>();
                surface.sharedMesh = mesh.sharedMesh;
                Physics.SyncTransforms();
                return surface.Raycast(ray, out hit, Mathf.Infinity);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static bool ContainsLabel(this string[] labels, string expected)
        {
            return System.Array.IndexOf(labels, expected) >= 0;
        }
    }
}
