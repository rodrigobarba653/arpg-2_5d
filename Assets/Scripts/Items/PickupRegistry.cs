using System.Collections.Generic;

/// <summary>
/// Global tracker of pickup IDs that the player has already consumed. Survives
/// scene changes, gets snapshotted into / restored from SaveData.
///
/// ItemPickup checks IsConsumed in Awake and hides itself if already taken.
/// </summary>
public static class PickupRegistry
{
    static readonly HashSet<string> consumed = new HashSet<string>();

    public static bool IsConsumed(string id)
        => !string.IsNullOrEmpty(id) && consumed.Contains(id);

    public static void MarkConsumed(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        consumed.Add(id);
    }

    /// <summary>Wipe everything. Called on New Game.</summary>
    public static void Clear() => consumed.Clear();

    /// <summary>Replace contents with the supplied ids. Called on Load.</summary>
    public static void RestoreFrom(IEnumerable<string> ids)
    {
        consumed.Clear();
        if (ids == null) return;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) consumed.Add(id);
    }

    /// <summary>Snapshot for saving. Returns a new list so callers can't mutate.</summary>
    public static List<string> Snapshot()
    {
        return new List<string>(consumed);
    }
}
