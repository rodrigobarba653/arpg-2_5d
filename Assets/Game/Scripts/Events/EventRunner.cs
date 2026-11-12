using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Runs an ordered SerializeReference action list.
/// Player-controlling runners acquire <see cref="GameplayLockChannels.PlayerControl"/>
/// for the duration of the sequence and release it on complete/cancel/disable.
///
/// Completion policy (Sprint 02):
/// - An event is completed only after successful full execution.
/// - Cancelled / failed runs do NOT set session Once or WorldState completed ids.
/// - Actions that already ran before cancel (e.g. SetFlag) are NOT rolled back.
/// </summary>
[DisallowMultipleComponent]
public class EventRunner : MonoBehaviour
{
    [Tooltip("Stable id for WorldState completed-event tracking and Persistent Once. " +
             "Required when Persist Once is enabled. Designer-assigned — never auto-generated.")]
    public string eventId = "";

    [Tooltip("When true, this event locks player input/interact/menu/magic/swap " +
             "and only one such event may run at a time.")]
    public bool seizesPlayerControl = true;

    [Tooltip("Once = fire at most one successful run. Repeat = may fire again after completion.")]
    public EventExecutionMode executionMode = EventExecutionMode.Once;

    [Tooltip("When Once + Persist Once: completion is stored in WorldState and " +
             "survives save/load. Session Once (this unchecked) only lasts until domain reload.")]
    public bool persistOnce;

    [Tooltip("Log step progress to the Console.")]
    public bool debugLog = true;

    [SerializeReference]
    public List<EventCondition> conditions = new List<EventCondition>();

    [SerializeReference]
    public List<EventAction> actions = new List<EventAction>();

    CancellationTokenSource cts;
    Coroutine running;
    EventAction currentAction;
    EventContext currentContext;
    bool completedOnce;
    bool holdsSchedulerSlot;
    bool holdsGameplayLock;
    bool cleaningUp;

    public bool IsRunning => running != null;
    public bool HasCompletedOnce => completedOnce;
    public int CurrentActionIndex { get; private set; } = -1;

    void OnEnable()
    {
        WorldState.OnRestored += SyncCompletedFromWorldState;
        SyncCompletedFromWorldState();
        WarnIfDuplicateEventId();
    }

    /// <summary>
    /// Attempt to start this runner. Returns false if rejected (busy, already
    /// running, Once already done, conditions failed, scheduler conflict).
    /// Conditions are checked BEFORE GameplayLock / EventScheduler ownership.
    /// </summary>
    public bool TryStart()
    {
        if (!isActiveAndEnabled)
            return false;

        // Silent when already running — CharacterController edge jitter / child
        // colliders would otherwise spam the Console every physics touch.
        if (IsRunning)
            return false;

        SyncCompletedFromWorldState();

        if (executionMode == EventExecutionMode.Once && completedOnce)
        {
            if (debugLog)
                Debug.Log($"[Event:{name}] TryStart rejected — Once already completed.", this);
            return false;
        }

        if (persistOnce)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogWarning(
                    $"[Event:{name}] Persist Once enabled but eventId is empty — " +
                    "refusing start. Assign a stable eventId in the Inspector.",
                    this);
                return false;
            }

            if (WorldState.IsEventCompleted(eventId))
            {
                completedOnce = true;
                if (debugLog)
                    Debug.Log(
                        $"[Event:{name}] TryStart rejected — persistent Once " +
                        $"('{eventId}') already completed in WorldState.",
                        this);
                return false;
            }
        }

        if (!EvaluateConditions(out string failedCondition))
        {
            if (debugLog)
                Debug.Log(
                    $"[Event:{name}] TryStart rejected — condition failed: {failedCondition}",
                    this);
            return false;
        }

        if (!EventScheduler.TryBegin(this, out string reason))
        {
            if (debugLog)
                Debug.Log($"[Event:{name}] TryStart rejected — {reason}.", this);
            return false;
        }

        holdsSchedulerSlot = true;

        if (seizesPlayerControl)
        {
            GameplayLock.Acquire(this, GameplayLockChannels.PlayerControl);
            holdsGameplayLock = true;
        }

        cts = new CancellationTokenSource();
        running = StartCoroutine(RunSequence(cts.Token));
        return true;
    }

    public void Cancel(string reason = null)
    {
        if (!IsRunning && !holdsGameplayLock && !holdsSchedulerSlot)
            return;

        if (debugLog)
            Debug.Log(
                $"[Event:{name}] Cancel ({reason ?? "manual"}) — locks released, control should return. " +
                "Completed-event / Once NOT marked. Prior SetFlag actions remain.",
                this);

        if (cts != null && !cts.IsCancellationRequested)
            cts.Cancel();

        if (currentAction != null && currentContext != null)
        {
            try { currentAction.Cancel(currentContext); }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        CleanupAfterRun();
    }

    void OnDisable()
    {
        WorldState.OnRestored -= SyncCompletedFromWorldState;
        Cancel("OnDisable");
    }

    void OnDestroy()
    {
        WorldState.OnRestored -= SyncCompletedFromWorldState;
        Cancel("OnDestroy");
    }

    bool EvaluateConditions(out string failedDisplayName)
    {
        failedDisplayName = null;
        if (conditions == null || conditions.Count == 0)
            return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            if (condition == null)
                continue;

            if (!condition.Evaluate())
            {
                failedDisplayName = condition.DisplayName;
                return false;
            }
        }

        return true;
    }

    void SyncCompletedFromWorldState()
    {
        if (!persistOnce || string.IsNullOrEmpty(eventId))
            return;

        if (IsRunning)
            return;

        completedOnce = WorldState.IsEventCompleted(eventId);
    }

    IEnumerator RunSequence(CancellationToken token)
    {
        var ctx = new EventContext(this, token);
        currentContext = ctx;
        CurrentActionIndex = -1;
        bool finishedSuccessfully = false;

        if (debugLog)
            Debug.Log($"[Event:{name}] Start ({actions?.Count ?? 0} actions, " +
                      $"playerControl={seizesPlayerControl}, eventId='{eventId}').", this);

        if (actions != null)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (token.IsCancellationRequested)
                    break;

                var action = actions[i];
                if (action == null)
                {
                    if (debugLog)
                        Debug.LogWarning($"[Event:{name}] Skipping null action at [{i}].", this);
                    continue;
                }

                CurrentActionIndex = i;
                currentAction = action;

                var step = action.Execute(ctx);
                while (step.MoveNext())
                {
                    if (token.IsCancellationRequested)
                        break;
                    yield return step.Current;
                }

                currentAction = null;

                if (ctx.StopRequested)
                {
                    if (debugLog)
                        Debug.Log(
                            $"[Event:{name}] Stopped at action [{i}] — {ctx.StopReason}. " +
                            "Completed NOT marked. Prior actions remain.",
                            this);
                    break;
                }

                if (token.IsCancellationRequested)
                    break;
            }
        }

        finishedSuccessfully = !token.IsCancellationRequested && !ctx.StopRequested;
        if (finishedSuccessfully)
            MarkCompletedSuccessfully();

        // Normal completion path (StopCoroutine skips this; Cancel cleans up).
        running = null;
        CleanupAfterRun();
    }

    void MarkCompletedSuccessfully()
    {
        completedOnce = true;

        if (!string.IsNullOrEmpty(eventId))
            WorldState.MarkEventCompleted(eventId);

        if (debugLog)
            Debug.Log($"[Event:{name}] Completed.", this);
    }

    void CleanupAfterRun()
    {
        if (cleaningUp)
            return;

        cleaningUp = true;
        try
        {
            currentAction = null;
            currentContext = null;
            CurrentActionIndex = -1;
            running = null;

            if (holdsGameplayLock)
            {
                GameplayLock.Release(this);
                holdsGameplayLock = false;
            }

            if (holdsSchedulerSlot)
            {
                EventScheduler.End(this);
                holdsSchedulerSlot = false;
            }

            if (cts != null)
            {
                cts.Dispose();
                cts = null;
            }
        }
        finally
        {
            cleaningUp = false;
        }
    }

    void WarnIfDuplicateEventId()
    {
        if (string.IsNullOrEmpty(eventId))
            return;

        var runners = Object.FindObjectsByType<EventRunner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < runners.Length; i++)
        {
            var other = runners[i];
            if (other == null || other == this)
                continue;
            if (other.eventId != eventId)
                continue;

            Debug.LogWarning(
                $"[EventRunner] Duplicate eventId '{eventId}' on " +
                $"'{name}' and '{other.name}'. Persistent Once / " +
                "completed tracking will collide.",
                this);
            break;
        }
    }
}
