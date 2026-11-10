using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ref-counted, multi-owner gameplay lock. Does NOT change Time.timeScale.
///
/// Overlapping owners are safe: each channel stays locked until every owner
/// that acquired it has released (or been ForceReleased).
///
/// PlayerInput is never blindly re-enabled on release — see <see cref="PlayerInputGate"/>.
/// </summary>
public static class GameplayLock
{
    static readonly Dictionary<object, GameplayLockChannels> owners = new Dictionary<object, GameplayLockChannels>();
    static readonly Dictionary<GameplayLockChannels, int> channelCounts = new Dictionary<GameplayLockChannels, int>();

    static readonly GameplayLockChannels[] TrackedChannels =
    {
        GameplayLockChannels.PlayerInput,
        GameplayLockChannels.Interact,
        GameplayLockChannels.Menu,
        GameplayLockChannels.Magic,
        GameplayLockChannels.PartySwap,
    };

    public static event Action OnChanged;

    public static GameplayLockChannels ActiveChannels { get; private set; }

    public static bool IsLocked(GameplayLockChannels channel)
    {
        if (channel == GameplayLockChannels.None)
            return false;

        // Any of the requested bits currently held.
        return (ActiveChannels & channel) != 0;
    }

    public static bool IsPlayerControlLocked => IsLocked(GameplayLockChannels.PlayerControl);

    /// <summary>
    /// Acquire (or replace) the channels held by <paramref name="owner"/>.
    /// Same owner re-acquiring replaces its previous mask atomically.
    /// </summary>
    public static void Acquire(object owner, GameplayLockChannels channels)
    {
        if (owner == null)
        {
            Debug.LogError("[GameplayLock] Acquire called with null owner.");
            return;
        }

        if (channels == GameplayLockChannels.None)
        {
            Release(owner);
            return;
        }

        if (owners.TryGetValue(owner, out var previous) && previous == channels)
            return;

        var before = ActiveChannels;

        if (previous != GameplayLockChannels.None)
            RemoveChannels(previous);

        owners[owner] = channels;
        AddChannels(channels);
        RebuildActive();
        ApplySideEffects(before, ActiveChannels);
        OnChanged?.Invoke();
    }

    public static void Release(object owner)
    {
        if (owner == null)
            return;

        if (!owners.TryGetValue(owner, out var previous))
            return;

        var before = ActiveChannels;
        owners.Remove(owner);
        RemoveChannels(previous);
        RebuildActive();
        ApplySideEffects(before, ActiveChannels);
        OnChanged?.Invoke();
    }

    /// <summary>Drop every owner. Used on domain reload / hard reset.</summary>
    public static void ReleaseAll()
    {
        if (owners.Count == 0 && ActiveChannels == GameplayLockChannels.None)
            return;

        var before = ActiveChannels;
        owners.Clear();
        channelCounts.Clear();
        ActiveChannels = GameplayLockChannels.None;
        ApplySideEffects(before, ActiveChannels);
        OnChanged?.Invoke();
    }

    public static int OwnerCount => owners.Count;

    public static string DebugSummary()
    {
        return $"owners={owners.Count} channels={ActiveChannels}";
    }

    static void AddChannels(GameplayLockChannels channels)
    {
        for (int i = 0; i < TrackedChannels.Length; i++)
        {
            var bit = TrackedChannels[i];
            if ((channels & bit) == 0)
                continue;

            channelCounts.TryGetValue(bit, out int count);
            channelCounts[bit] = count + 1;
        }
    }

    static void RemoveChannels(GameplayLockChannels channels)
    {
        for (int i = 0; i < TrackedChannels.Length; i++)
        {
            var bit = TrackedChannels[i];
            if ((channels & bit) == 0)
                continue;

            if (!channelCounts.TryGetValue(bit, out int count))
                continue;

            count--;
            if (count <= 0)
                channelCounts.Remove(bit);
            else
                channelCounts[bit] = count;
        }
    }

    static void RebuildActive()
    {
        GameplayLockChannels mask = GameplayLockChannels.None;
        for (int i = 0; i < TrackedChannels.Length; i++)
        {
            var bit = TrackedChannels[i];
            if (channelCounts.TryGetValue(bit, out int count) && count > 0)
                mask |= bit;
        }
        ActiveChannels = mask;
    }

    static void ApplySideEffects(GameplayLockChannels previous, GameplayLockChannels next)
    {
        bool hadInput = (previous & GameplayLockChannels.PlayerInput) != 0;
        bool hasInput = (next & GameplayLockChannels.PlayerInput) != 0;

        if (!hadInput && hasInput)
        {
            CancelActiveCombat();
            PlayerInputGate.RefreshAll();
        }
        else if (hadInput && !hasInput)
        {
            PlayerInputGate.RefreshAll();
        }
        else if (hasInput)
        {
            // Still locked — keep input disabled (e.g. party swap mid-event).
            PlayerInputGate.RefreshAll();
        }
    }

    static void CancelActiveCombat()
    {
        var active = Party.Active;
        if (active == null)
            return;

        var combat = active.GetComponent<PlayerCombatController>();
        if (combat != null)
            combat.CancelCombatImmediate();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        owners.Clear();
        channelCounts.Clear();
        ActiveChannels = GameplayLockChannels.None;
        OnChanged = null;
    }
#endif
}
