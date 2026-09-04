using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps track of which enemies are currently engaging a given player and
/// hands each one a distinct angle slot around them, so a group of enemies
/// spreads out in a rough circle instead of all pathing to the exact same
/// point and stacking on top of each other.
///
/// Pure bookkeeping — no MonoBehaviour, no per-frame cost beyond a dictionary
/// lookup. EnemyAI registers/unregisters itself as it enters/leaves combat.
/// </summary>
public static class EnemySurroundGroup
{
    static readonly Dictionary<Transform, List<EnemyAI>> groups = new Dictionary<Transform, List<EnemyAI>>();

    public static void Register(Transform player, EnemyAI enemy)
    {
        if (player == null || enemy == null) return;

        if (!groups.TryGetValue(player, out var list))
        {
            list = new List<EnemyAI>();
            groups[player] = list;
        }

        if (!list.Contains(enemy))
            list.Add(enemy);
    }

    public static void Unregister(Transform player, EnemyAI enemy)
    {
        if (player == null || enemy == null) return;

        if (groups.TryGetValue(player, out var list))
        {
            list.Remove(enemy);
            if (list.Count == 0)
                groups.Remove(player);
        }
    }

    /// <summary>World-space offset from the player's position that this enemy
    /// should path toward, so it approaches from its own slot on the circle
    /// instead of the player's exact position. Zero if not registered.</summary>
    public static Vector3 GetSurroundOffset(Transform player, EnemyAI enemy, float radius)
    {
        if (player == null || enemy == null) return Vector3.zero;
        if (!groups.TryGetValue(player, out var list) || list.Count == 0) return Vector3.zero;

        int index = list.IndexOf(enemy);
        if (index < 0) return Vector3.zero;

        // Spread evenly around the circle. Slots shift slightly when enemies
        // join/leave the group (list index changes) — a minor re-angle, not a
        // teleport, since the NavMeshAgent just steers toward the new point.
        float angleStep = 360f / list.Count;
        float angle = angleStep * index;

        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        return rot * Vector3.forward * radius;
    }
}
