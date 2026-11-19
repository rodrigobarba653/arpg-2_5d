using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central runtime world flags + completed event IDs.
/// Pattern mirrors <see cref="PickupRegistry"/>: static runtime state, snapshotted
/// into <see cref="SaveData"/> by <see cref="SaveManager"/> — never writes disk itself.
///
/// Missing flags read as <c>false</c>. Setting a flag does NOT auto-save.
/// </summary>
public static class WorldState
{
    static readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
    static readonly HashSet<string> completedEvents = new HashSet<string>();

    /// <summary>Fired after <see cref="Clear"/> or <see cref="RestoreFrom"/>.</summary>
    public static event Action OnRestored;

    /// <summary>Fired after a single flag write via <see cref="SetFlag"/> / <see cref="ClearFlag"/>.</summary>
    public static event Action<string, bool> OnFlagChanged;

    public static bool GetFlag(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        return flags.TryGetValue(id, out bool value) && value;
    }

    /// <summary>True if this flag id was ever written (true or false).</summary>
    public static bool HasFlag(string id)
    {
        return !string.IsNullOrEmpty(id) && flags.ContainsKey(id);
    }

    public static void SetFlag(string id, bool value)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[WorldState] SetFlag ignored — empty id.");
            return;
        }

        if (flags.TryGetValue(id, out bool previous) && previous == value)
            return;

        flags[id] = value;
        OnFlagChanged?.Invoke(id, value);
    }

    public static void ClearFlag(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;
        SetFlag(id, false);
    }

    public static bool IsEventCompleted(string eventId)
    {
        return !string.IsNullOrEmpty(eventId) && completedEvents.Contains(eventId);
    }

    public static void MarkEventCompleted(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
            return;
        completedEvents.Add(eventId);
    }

    /// <summary>New Game / domain reset.</summary>
    public static void Clear()
    {
        flags.Clear();
        completedEvents.Clear();
        OnRestored?.Invoke();
    }

    public static void RestoreFrom(IEnumerable<SavedWorldFlag> savedFlags, IEnumerable<string> completedIds)
    {
        flags.Clear();
        completedEvents.Clear();

        if (savedFlags != null)
        {
            foreach (var entry in savedFlags)
            {
                if (string.IsNullOrEmpty(entry.id))
                    continue;
                flags[entry.id] = entry.value;
            }
        }

        if (completedIds != null)
        {
            foreach (var id in completedIds)
            {
                if (!string.IsNullOrEmpty(id))
                    completedEvents.Add(id);
            }
        }

        OnRestored?.Invoke();
    }

    public static List<SavedWorldFlag> SnapshotFlags()
    {
        var list = new List<SavedWorldFlag>(flags.Count);
        foreach (var kvp in flags)
        {
            list.Add(new SavedWorldFlag
            {
                id = kvp.Key,
                value = kvp.Value,
            });
        }
        return list;
    }

    public static List<string> SnapshotCompletedEvents()
    {
        return new List<string>(completedEvents);
    }

    public static string DebugSummary()
    {
        return $"flags={flags.Count} completedEvents={completedEvents.Count}";
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        flags.Clear();
        completedEvents.Clear();
        OnRestored = null;
        OnFlagChanged = null;
    }
#endif
}
