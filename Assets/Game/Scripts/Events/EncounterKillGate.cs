using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Counts kills in an encounter group, then starts an <see cref="EventRunner"/>
/// (open door, spawn boss, cutscene…). Same pattern as DoorSwitch: the gate is
/// the trigger, the runner (Start Mode = Manual Only) is the motor.
///
/// Seed enemies in the Inspector, parent them under this object and Collect, or
/// add <see cref="EncounterMember"/> on spawns that register at runtime.
/// </summary>
[DisallowMultipleComponent]
public class EncounterKillGate : MonoBehaviour
{
    [Header("Event Runner (motor)")]
    [Tooltip("Started when the kill threshold is met. Keep Start Mode = Manual Only.")]
    public EventRunner eventRunner;

    [Header("Members")]
    [Tooltip("Enemies that count for this encounter. Also filled by Collect Children / EncounterMember.")]
    public List<EnemyHealth> enemies = new List<EnemyHealth>();

    [Tooltip("How many kills are needed. 0 = all registered members must die.")]
    [Min(0)]
    public int requiredKills;

    [Header("Persistence")]
    [Tooltip("WorldState flag set when the gate completes. Also used to skip re-fire after load. " +
             "Door/boss visual persistence still lives on SimpleDoor / MovableObject / SetActive+flag.")]
    public string completedFlagId = "";

    [Tooltip("If true, completion is remembered via Completed Flag Id (survives save/load).")]
    public bool persistCompletion = true;

    [Header("On Complete")]
    [Tooltip("Call EventRunner.TryStart when the threshold is met (not on save restore).")]
    public bool startEventOnComplete = true;

    [Tooltip("Log kill progress and completion to the Console.")]
    public bool debugLog = true;

    [Header("Unity Events")]
    public UnityEvent onCompleted;

    readonly HashSet<EnemyHealth> members = new HashSet<EnemyHealth>();
    readonly HashSet<EnemyHealth> killed = new HashSet<EnemyHealth>();

    bool completed;
    bool armed;

    public bool IsCompleted => completed;
    public int RegisteredCount => members.Count;
    public int KillCount => killed.Count;

    public int RequiredCount
    {
        get
        {
            if (requiredKills > 0)
                return requiredKills;
            return Mathf.Max(1, members.Count);
        }
    }

    void Reset()
    {
        eventRunner = GetComponent<EventRunner>();
    }

    void Awake()
    {
        if (eventRunner == null)
            eventRunner = GetComponent<EventRunner>();

        for (int i = 0; i < enemies.Count; i++)
            RegisterInternal(enemies[i], countIfAlreadyDead: true);
    }

    void OnEnable()
    {
        EnemyHealth.Died += OnEnemyDied;
        WorldState.OnRestored += OnWorldRestored;
        armed = true;
        SyncFromPersistOrProgress(startEventIfNewlyMet: false);
    }

    void OnDisable()
    {
        EnemyHealth.Died -= OnEnemyDied;
        WorldState.OnRestored -= OnWorldRestored;
        armed = false;
    }

    void Start()
    {
        // After all EncounterMember OnEnable registrations.
        SyncFromPersistOrProgress(startEventIfNewlyMet: false);
    }

    /// <summary>Add an enemy to this encounter (safe to call more than once).</summary>
    public void Register(EnemyHealth enemy)
    {
        RegisterInternal(enemy, countIfAlreadyDead: true);
        if (armed && !completed)
            TryComplete(startEvent: true);
    }

    /// <summary>Remove a living enemy that should no longer count (despawn without kill).</summary>
    public void Unregister(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        members.Remove(enemy);
        killed.Remove(enemy);
        SyncInspectorList();
    }

    /// <summary>Editor / tools: pull EnemyHealth from children into the seed list.</summary>
    public void CollectChildren()
    {
        enemies.Clear();
        var found = GetComponentsInChildren<EnemyHealth>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && !enemies.Contains(found[i]))
                enemies.Add(found[i]);
        }

        members.Clear();
        killed.Clear();
        for (int i = 0; i < enemies.Count; i++)
            RegisterInternal(enemies[i], countIfAlreadyDead: true);
    }

    void OnWorldRestored()
    {
        SyncFromPersistOrProgress(startEventIfNewlyMet: false);
    }

    void OnEnemyDied(EnemyHealth enemy)
    {
        if (completed || enemy == null)
            return;
        if (!members.Contains(enemy))
            return;

        if (!killed.Add(enemy))
            return;

        if (debugLog)
        {
            Debug.Log(
                $"[KillGate:{name}] Kill {KillCount}/{RequiredCount} ('{enemy.name}')",
                this);
        }

        TryComplete(startEvent: true);
    }

    void RegisterInternal(EnemyHealth enemy, bool countIfAlreadyDead)
    {
        if (enemy == null)
            return;

        members.Add(enemy);
        if (!enemies.Contains(enemy))
            enemies.Add(enemy);

        if (countIfAlreadyDead && enemy.IsDead)
            killed.Add(enemy);
    }

    void SyncFromPersistOrProgress(bool startEventIfNewlyMet)
    {
        if (persistCompletion &&
            !string.IsNullOrEmpty(completedFlagId) &&
            WorldState.GetFlag(completedFlagId))
        {
            completed = true;
            if (debugLog)
                Debug.Log($"[KillGate:{name}] Already completed (flag '{completedFlagId}').", this);
            return;
        }

        if (completed)
            return;

        // Refresh dead set from current members (scene reload without persist).
        killed.Clear();
        foreach (var enemy in members)
        {
            if (enemy != null && enemy.IsDead)
                killed.Add(enemy);
        }

        TryComplete(startEvent: startEventIfNewlyMet);
    }

    void TryComplete(bool startEvent)
    {
        if (completed)
            return;

        if (members.Count == 0)
            return;

        if (KillCount < RequiredCount)
            return;

        Complete(startEvent);
    }

    void Complete(bool startEvent)
    {
        if (completed)
            return;

        completed = true;

        if (persistCompletion && !string.IsNullOrEmpty(completedFlagId))
            WorldState.SetFlag(completedFlagId, true);

        if (debugLog)
        {
            Debug.Log(
                $"[KillGate:{name}] Completed ({KillCount}/{RequiredCount}). " +
                $"startEvent={startEvent && startEventOnComplete}",
                this);
        }

        onCompleted?.Invoke();

        if (!startEvent || !startEventOnComplete)
            return;

        if (eventRunner == null)
        {
            Debug.LogWarning(
                $"[KillGate:{name}] Threshold met but Event Runner is missing.",
                this);
            return;
        }

        if (eventRunner.startMode != EventStartMode.ManualOnly && debugLog)
        {
            Debug.LogWarning(
                $"[KillGate:{name}] EventRunner Start Mode is {eventRunner.startMode}. " +
                "Prefer Manual Only — the Kill Gate starts it.",
                eventRunner);
        }

        eventRunner.TryStart();
    }

    void SyncInspectorList()
    {
        enemies.Clear();
        foreach (var enemy in members)
        {
            if (enemy != null)
                enemies.Add(enemy);
        }
    }

#if UNITY_EDITOR
    public void EditorForceComplete()
    {
        foreach (var enemy in members)
        {
            if (enemy != null)
                killed.Add(enemy);
        }

        Complete(startEvent: Application.isPlaying);
    }
#endif
}
