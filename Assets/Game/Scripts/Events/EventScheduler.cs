using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enforces concurrency policy:
/// - At most one player-controlling event at a time.
/// - Unlimited independent environmental events.
/// </summary>
public static class EventScheduler
{
    // After a player-control event ends/cancels, briefly ignore new
    // player-control starts. Disabling a co-located trigger (Cancel QA)
    // can make Unity fire Exit+Enter on the sibling volume and steal control.
    const float PlayerControlEndSuppressSeconds = 0.2f;

    static EventRunner playerControllingRunner;
    static readonly HashSet<EventRunner> environmentalRunners = new HashSet<EventRunner>();
    static float suppressPlayerControlUntil;

    public static EventRunner PlayerControllingRunner => playerControllingRunner;
    public static int EnvironmentalCount => environmentalRunners.Count;
    public static bool HasPlayerControllingEvent => playerControllingRunner != null;

    /// <summary>
    /// Locking interactive dialogue owns player attention the same way a
    /// seize-control EventRunner does — new seize starts must wait.
    /// </summary>
    public static bool HasLockingInteractiveDialogue
    {
        get
        {
            var service = DialogueService.Instance;
            return service != null && service.HasLockingInteractive;
        }
    }

    public static bool TryBegin(EventRunner runner, out string rejectReason)
    {
        rejectReason = null;
        if (runner == null)
        {
            rejectReason = "runner is null";
            return false;
        }

        if (runner.seizesPlayerControl)
        {
            if (Time.unscaledTime < suppressPlayerControlUntil)
            {
                rejectReason = "suppressed after prior player-control end";
                return false;
            }

            if (playerControllingRunner != null && playerControllingRunner != runner)
            {
                rejectReason =
                    $"player-control busy ({playerControllingRunner.name})";
                return false;
            }

            if (HasLockingInteractiveDialogue)
            {
                rejectReason = "locking interactive dialogue busy";
                return false;
            }

            playerControllingRunner = runner;
            return true;
        }

        environmentalRunners.Add(runner);
        return true;
    }

    public static void End(EventRunner runner)
    {
        if (runner == null)
            return;

        if (playerControllingRunner == runner)
        {
            playerControllingRunner = null;
            suppressPlayerControlUntil =
                Time.unscaledTime + PlayerControlEndSuppressSeconds;
        }

        environmentalRunners.Remove(runner);
    }

    public static string DebugSummary()
    {
        string player = playerControllingRunner != null
            ? playerControllingRunner.name
            : "none";
        string dialogue = HasLockingInteractiveDialogue ? "lock" : "none";
        return $"playerControl={player} dialogue={dialogue} env={environmentalRunners.Count}";
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        playerControllingRunner = null;
        environmentalRunners.Clear();
        suppressPlayerControlUntil = 0f;
    }
#endif
}
