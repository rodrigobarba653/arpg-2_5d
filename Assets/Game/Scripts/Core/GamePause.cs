using System;
using UnityEngine;

/// <summary>
/// Global pause state. Any system that needs to "freeze" while a menu / dialog
/// is open subscribes to OnPauseChanged and reacts (disable input, stop AI, etc).
///
/// Setting Time.timeScale = 0 alone is NOT enough — Input System callbacks and
/// some MonoBehaviours still fire. This helper centralizes both the time scale
/// AND a flag so other scripts can opt-out cleanly.
/// </summary>
public static class GamePause
{
    public static bool IsPaused { get; private set; }
    public static event Action<bool> OnPauseChanged;

    public static void SetPaused(bool paused, bool freezeTime = true)
    {
        if (IsPaused == paused) return;

        IsPaused = paused;

        if (freezeTime)
            Time.timeScale = paused ? 0f : 1f;

        OnPauseChanged?.Invoke(paused);
    }
}
