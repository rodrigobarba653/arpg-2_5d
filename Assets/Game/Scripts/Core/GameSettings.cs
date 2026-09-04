using System;
using UnityEngine;

/// <summary>
/// Small persisted player-facing settings that don't belong to any specific
/// system (AudioManager owns its own volumes; this is for gameplay/UX toggles).
/// Backed by PlayerPrefs so they survive between sessions without a save file.
/// </summary>
public static class GameSettings
{
    const string RelaxedCombatKey = "Settings_RelaxedCombat";

    static bool loaded;
    static bool relaxedCombatMode;

    /// <summary>
    /// Combat Style toggle, set from Settings ▸ Gameplay.
    ///   false (Active, default) — no slow-mo on the spell wheel. Casting is
    ///   instant for players who already know their binds.
    ///   true (Relaxed) — the world slows down while the spell wheel is
    ///   expanded (actively cycling spells), giving more time to pick.
    /// </summary>
    public static bool RelaxedCombatMode
    {
        get { EnsureLoaded(); return relaxedCombatMode; }
        set
        {
            EnsureLoaded();
            if (relaxedCombatMode == value) return;
            relaxedCombatMode = value;
            PlayerPrefs.SetInt(RelaxedCombatKey, value ? 1 : 0);
            PlayerPrefs.Save();
            OnRelaxedCombatModeChanged?.Invoke(value);
        }
    }

    /// <summary>Fired whenever RelaxedCombatMode actually changes value.</summary>
    public static event Action<bool> OnRelaxedCombatModeChanged;

    static void EnsureLoaded()
    {
        if (loaded) return;
        relaxedCombatMode = PlayerPrefs.GetInt(RelaxedCombatKey, 0) == 1;
        loaded = true;
    }
}
