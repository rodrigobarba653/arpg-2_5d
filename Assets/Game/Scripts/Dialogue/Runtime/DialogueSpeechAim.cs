using UnityEngine;

/// <summary>
/// World aim point for chatbox tails: speechAnchor if set, else top of a body collider.
/// </summary>
public static class DialogueSpeechAim
{
    /// <summary>
    /// Point the ambient (or follow) tail at — top-center of the source element.
    /// </summary>
    public static Vector3 ResolveAimWorldPoint(Transform root, Transform speechAnchor = null)
    {
        // Explicit speechAnchor (child) wins; otherwise aim at collider top of root.
        if (speechAnchor != null && speechAnchor != root)
            return speechAnchor.position;

        if (root == null)
            return Vector3.zero;

        if (TryGetColliderTop(root, out Vector3 top))
            return top;

        return root.position;
    }

    /// <summary>
    /// Highest non-trigger collider point on <paramref name="root"/> (and children),
    /// XZ kept at the collider center so the tip aims at the silhouette top.
    /// </summary>
    public static bool TryGetColliderTop(Transform root, out Vector3 worldTop)
    {
        worldTop = default;
        if (root == null)
            return false;

        var cols = root.GetComponentsInChildren<Collider>(includeInactive: false);
        bool found = false;
        Bounds combined = default;

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;

            if (!found)
            {
                combined = col.bounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(col.bounds);
            }
        }

        if (!found)
            return false;

        worldTop = new Vector3(combined.center.x, combined.max.y, combined.center.z);
        return true;
    }
}
