using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single place that decides whether a <see cref="PlayerInput"/> may be enabled.
///
/// Owners that disable input (GamePause / LoadingScreen via <see cref="PlayerPauseHandler"/>,
/// party dormancy via <see cref="PartyMemberControl"/>, and <see cref="GameplayLock"/>)
/// must call <see cref="Refresh"/> / <see cref="RefreshAll"/> instead of blindly
/// setting <c>enabled = true</c>.
/// </summary>
public static class PlayerInputGate
{
    public static bool ShouldEnable(PlayerInput input)
    {
        if (input == null)
            return false;

        if (GamePause.IsPaused)
            return false;

        if (GameplayLock.IsLocked(GameplayLockChannels.PlayerInput))
            return false;

        var control = input.GetComponent<PartyMemberControl>();
        if (control != null && !control.isControlled)
            return false;

        return true;
    }

    /// <summary>
    /// Apply the eligibility rule to one PlayerInput. Safe to call any time.
    /// </summary>
    public static void Refresh(PlayerInput input)
    {
        if (input == null)
            return;

        bool want = ShouldEnable(input);
        if (input.enabled != want)
            input.enabled = want;
    }

    /// <summary>
    /// Refresh every PlayerInput currently loaded.
    /// Prefer this after GameplayLock / pause / party control changes.
    /// </summary>
    public static void RefreshAll()
    {
        var inputs = Object.FindObjectsByType<PlayerInput>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < inputs.Length; i++)
            Refresh(inputs[i]);
    }
}
