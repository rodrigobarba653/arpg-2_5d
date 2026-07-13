using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight read-only view over the party. Backed by PlayerHealth.All (the
/// PlayerHealths registered in the scene) plus a single "active" pointer.
///
/// Nothing here owns runtime state — characters are PlayerHealth components in
/// the scene. This class just answers "who's in the party" and "who is the
/// player currently controlling" without each system having to re-derive that.
///
/// Subscribe to OnActiveChanged or OnPartyChanged for UI / save hooks.
/// </summary>
public static class Party
{
    /// <summary>Fires when the active character changes (including via load).</summary>
    public static event Action<PlayerHealth> OnActiveChanged;

    /// <summary>Fires when a member joins or leaves the party.</summary>
    public static event Action OnPartyChanged;

    static PlayerHealth explicitActive;

    /// <summary>All party members currently in the scene.</summary>
    public static IReadOnlyList<PlayerHealth> Members => PlayerHealth.All;

    /// <summary>
    /// The character the player is currently controlling. Defaults to the
    /// first registered member if nothing has been set explicitly. Null if
    /// the party is empty.
    /// </summary>
    public static PlayerHealth Active
    {
        get
        {
            if (explicitActive != null) return explicitActive;
            return PlayerHealth.All.Count > 0 ? PlayerHealth.All[0] : null;
        }
    }

    /// <summary>True if there is at least one party member.</summary>
    public static bool HasAnyMembers => PlayerHealth.All.Count > 0;

    public static int MemberCount => PlayerHealth.All.Count;

    public static bool IsActive(PlayerHealth member) => member != null && Active == member;

    /// <summary>Convenience: Active.transform, or null if no member.</summary>
    public static Transform ActiveTransform
    {
        get { var a = Active; return a != null ? a.transform : null; }
    }

    /// <summary>
    /// Switch the active character. Pass null to revert to "default = first
    /// registered member". Fires OnActiveChanged on real changes.
    /// </summary>
    public static void SetActive(PlayerHealth member)
    {
        var oldActive = Active;
        explicitActive = member;
        var newActive = Active;

        if (oldActive != newActive)
        {
            // TEMPORARY DIAGNOSTIC: log where the swap came from so we can find
            // whatever is calling SetActive when the player takes damage.
            string oldName = oldActive != null ? oldActive.name : "NULL";
            string newName = newActive != null ? newActive.name : "NULL";
            Debug.Log($"[Party] SetActive: '{oldName}' → '{newName}'\n{System.Environment.StackTrace}");

            OnActiveChanged?.Invoke(newActive);
        }
    }

    /// <summary>
    /// Switch the active character by character id. Convenient for save load.
    /// Returns true if a matching member was found.
    /// </summary>
    public static bool SetActiveByCharacterId(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;

        var match = FindByCharacterId(characterId);
        if (match == null) return false;

        SetActive(match);
        return true;
    }

    public static PlayerHealth FindByCharacterId(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;

        var all = PlayerHealth.All;
        for (int i = 0; i < all.Count; i++)
        {
            var p = all[i];
            if (p == null || p.character == null) continue;
            if (p.character.id == characterId) return p;
        }
        return null;
    }

    /// <summary>Index of a member in PlayerHealth.All, or -1 if not present.</summary>
    public static int IndexOf(PlayerHealth member)
    {
        if (member == null) return -1;
        var all = PlayerHealth.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] == member) return i;
        return -1;
    }

    /// <summary>
    /// Internal hook used by PlayerHealth when it registers / unregisters. UI
    /// or external code shouldn't need to call this directly.
    /// </summary>
    public static void RaisePartyChanged()
    {
        OnPartyChanged?.Invoke();
    }

    // ============================================================
    // SWAP COOLDOWN
    // ============================================================

    /// <summary>Seconds between swaps. Set by PartySwapInput from its Inspector.</summary>
    public static float SwapCooldown = 1f;

    static float nextSwapTime;

    /// <summary>True if we are not on cooldown AND there are at least 2 members.</summary>
    public static bool CanSwap
        => Time.unscaledTime >= nextSwapTime && PlayerHealth.All.Count > 1;

    /// <summary>Seconds remaining on the cooldown (0 if ready).</summary>
    public static float SwapCooldownRemaining
        => Mathf.Max(0f, nextSwapTime - Time.unscaledTime);

    /// <summary>0–1: how full the cooldown bar is (1 = ready, 0 = just swapped).</summary>
    public static float SwapCooldownProgress01
    {
        get
        {
            if (SwapCooldown <= 0f) return 1f;
            return 1f - Mathf.Clamp01(SwapCooldownRemaining / SwapCooldown);
        }
    }

    /// <summary>
    /// Toggle to the next party member (round-robin). No-op if there is fewer
    /// than 2 members, or if the cooldown hasn't elapsed. Returns true on a
    /// successful swap.
    /// </summary>
    public static bool SwapNext()
    {
        if (!CanSwap) return false;

        var all = PlayerHealth.All;
        if (all.Count < 2) return false;

        int currentIdx = IndexOf(Active);
        if (currentIdx < 0) currentIdx = 0;

        int nextIdx = (currentIdx + 1) % all.Count;
        var next = all[nextIdx];
        if (next == null || next == Active) return false;

        SetActive(next);
        nextSwapTime = Time.unscaledTime + SwapCooldown;
        return true;
    }

    /// <summary>Force the cooldown to be ready immediately (useful for testing).</summary>
    public static void ResetSwapCooldown()
    {
        nextSwapTime = 0f;
    }

    // ============================================================
    // DAMAGE-BASED SWAP LOCKOUT
    // ============================================================

    /// <summary>Time.time at which any active party member last took damage.
    /// Set from PlayerHealth.TakeDamage. Read by PartySwapInput to swallow
    /// reflex swap-button presses right after a hit.</summary>
    public static float LastActiveDamageTime = -999f;

    /// <summary>Called by PlayerHealth.TakeDamage on the active member.</summary>
    public static void NotifyActiveTookDamage()
    {
        LastActiveDamageTime = Time.time;
    }
}
