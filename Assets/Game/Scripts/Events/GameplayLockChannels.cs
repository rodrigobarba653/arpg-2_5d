using System;

/// <summary>
/// Selective lock channels used by <see cref="GameplayLock"/>.
/// These never touch <c>Time.timeScale</c> — that remains <see cref="GamePause"/>'s job.
/// </summary>
[Flags]
public enum GameplayLockChannels
{
    None = 0,
    PlayerInput = 1 << 0,
    Interact = 1 << 1,
    Menu = 1 << 2,
    Magic = 1 << 3,
    PartySwap = 1 << 4,

    /// <summary>
    /// Convenience mask for a player-controlling event sequence.
    /// </summary>
    PlayerControl = PlayerInput | Interact | Menu | Magic | PartySwap,
}
