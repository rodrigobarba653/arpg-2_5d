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

    [Tooltip("Run limit — independent of Start Mode. Once = at most one successful completion. " +
             "Repeat = may complete again after finishing.")]
    public EventExecutionMode executionMode = EventExecutionMode.Once;

    [Tooltip("When Once + Persist Once: completion is stored in WorldState and " +
             "survives save/load. Session Once (this unchecked) only lasts until domain reload.")]
    public bool persistOnce;

    [Tooltip("Log step progress to the Console.")]
    public bool debugLog = true;

    [Header("Start Mode")]
    [Tooltip("Manual / volume trigger / interact / toggle cross. Trigger modes use this object's Collider.")]
    public EventStartMode startMode = EventStartMode.ManualOnly;

    [Tooltip("Trigger modes only. Layers that can fire the volume.")]
    public LayerMask triggerPlayerLayers = ~0;

    [Tooltip("Trigger modes only. Also require Player tag on the collider or its root.")]
    public bool triggerRequirePlayerTag = true;

    [Tooltip("Tag used when Require Player Tag is on.")]
    public string triggerPlayerTag = "Player";

    [Tooltip("Toggle Cross only. Ignore another cross for this many seconds.")]
    [Min(0f)]
    public float toggleCrossCooldownSeconds = 0.45f;

    [SerializeReference]
    public List<EventCondition> conditions = new List<EventCondition>();

    [SerializeReference]
    public List<EventAction> actions = new List<EventAction>();

    [Tooltip("Used by Start Mode Toggle Cross when turning OFF (second cross). " +
             "Typical: Gameplay Follow Offset → Restore.")]
    [SerializeReference]
    public List<EventAction> toggleOffActions = new List<EventAction>();

    CancellationTokenSource cts;
    Coroutine running;
    EventAction currentAction;
    EventContext currentContext;
    bool completedOnce;
    bool holdsSchedulerSlot;
    bool holdsGameplayLock;
    bool cleaningUp;
    bool runningToggleOff;

    readonly Dictionary<Transform, int> triggerInsideCounts = new Dictionary<Transform, int>();
    bool toggleCrossOn;
    float lastToggleCrossUnscaled = -999f;

    public bool IsRunning => running != null;
    public bool HasCompletedOnce => completedOnce;
    public int CurrentActionIndex { get; private set; } = -1;
    public bool ToggleCrossIsOn => toggleCrossOn;

    void OnEnable()
    {
        WorldState.OnRestored += SyncCompletedFromWorldState;
        SyncCompletedFromWorldState();
        WarnIfDuplicateEventId();
    }

    void OnDisable()
    {
        WorldState.OnRestored -= SyncCompletedFromWorldState;
        triggerInsideCounts.Clear();
        Cancel("OnDisable");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!UsesVolumeTrigger)
            return;
        if (!IsValidTriggerPlayer(other))
            return;

        Transform root = other.transform.root;
        triggerInsideCounts.TryGetValue(root, out int count);
        triggerInsideCounts[root] = count + 1;
        if (count != 0)
            return;

        if (startMode == EventStartMode.TriggerOnEnter)
            TryStart();
        else if (startMode == EventStartMode.ToggleCross)
            HandleToggleCross();
    }

    void OnTriggerExit(Collider other)
    {
        if (!UsesVolumeTrigger)
            return;
        if (!IsValidTriggerPlayer(other))
            return;

        Transform root = other.transform.root;
        if (!triggerInsideCounts.TryGetValue(root, out int count))
            return;

        count--;
        if (count <= 0)
        {
            triggerInsideCounts.Remove(root);
            if (startMode == EventStartMode.TriggerOnExit)
                TryStart();
        }
        else
        {
            triggerInsideCounts[root] = count;
        }
    }

    bool UsesVolumeTrigger =>
        startMode == EventStartMode.TriggerOnEnter ||
        startMode == EventStartMode.TriggerOnExit ||
        startMode == EventStartMode.ToggleCross;

    void HandleToggleCross()
    {
        float now = Time.unscaledTime;
        if (now - lastToggleCrossUnscaled < toggleCrossCooldownSeconds)
            return;

        lastToggleCrossUnscaled = now;
        toggleCrossOn = !toggleCrossOn;
        if (toggleCrossOn)
            TryStart();
        else
            TryStartToggleOff();
    }

    bool IsValidTriggerPlayer(Collider other)
    {
        if (other == null)
            return false;
        if (((1 << other.gameObject.layer) & triggerPlayerLayers) == 0)
            return false;
        if (!triggerRequirePlayerTag)
            return true;
        if (other.CompareTag(triggerPlayerTag))
            return true;
        Transform root = other.transform.root;
        return root != null && root.CompareTag(triggerPlayerTag);
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
        runningToggleOff = false;
        running = StartCoroutine(RunSequence(actions, markComplete: true, cts.Token));
        return true;
    }

    /// <summary>
    /// Toggle Cross OFF path. Runs <see cref="toggleOffActions"/> and never marks Once / completed.
    /// </summary>
    public bool TryStartToggleOff()
    {
        if (!isActiveAndEnabled)
            return false;

        if (IsRunning)
        {
            Cancel("toggle-off-replace");
        }

        if (!EvaluateConditions(out string failedCondition))
        {
            if (debugLog)
                Debug.Log(
                    $"[Event:{name}] TryStartToggleOff rejected — condition failed: {failedCondition}",
                    this);
            return false;
        }

        if (!EventScheduler.TryBegin(this, out string reason))
        {
            if (debugLog)
                Debug.Log($"[Event:{name}] TryStartToggleOff rejected — {reason}.", this);
            return false;
        }

        holdsSchedulerSlot = true;

        if (seizesPlayerControl)
        {
            GameplayLock.Acquire(this, GameplayLockChannels.PlayerControl);
            holdsGameplayLock = true;
        }

        cts = new CancellationTokenSource();
        runningToggleOff = true;
        running = StartCoroutine(RunSequence(toggleOffActions, markComplete: false, cts.Token));
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

        runningToggleOff = false;
        CleanupAfterRun();
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

    IEnumerator RunSequence(List<EventAction> sequence, bool markComplete, CancellationToken token)
    {
        var ctx = new EventContext(this, token);
        currentContext = ctx;
        CurrentActionIndex = -1;
        bool finishedSuccessfully = false;
        string label = runningToggleOff ? "ToggleOff" : "Start";

        if (debugLog)
            Debug.Log($"[Event:{name}] {label} ({sequence?.Count ?? 0} actions, " +
                      $"playerControl={seizesPlayerControl}, eventId='{eventId}').", this);

        if (sequence != null)
        {
            for (int i = 0; i < sequence.Count; i++)
            {
                if (token.IsCancellationRequested)
                    break;

                var action = sequence[i];
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
        if (finishedSuccessfully && markComplete)
            MarkCompletedSuccessfully();

        // Normal completion path (StopCoroutine skips this; Cancel cleans up).
        running = null;
        runningToggleOff = false;
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

            CameraDirector.NotifyRunnerFinished(this);
            PlayCharacterAnimationAction.ReleaseHeldForRunner(this);

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
