using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs child actions together and waits until every one finishes.
/// One camera-pose action is allowed in the group. Nested groups are rejected.
/// </summary>
[Serializable]
public class ParallelActions : EventAction
{
    [SerializeReference]
    public List<EventAction> actions = new List<EventAction>();

    readonly List<ChildSlot> slots = new List<ChildSlot>();
    EventRunner host;
    bool stopIssued;

    sealed class ChildSlot
    {
        public EventAction action;
        public Coroutine routine;
        public bool finished;
    }

    public override string DisplayName => $"Parallel ({actions?.Count ?? 0})";

    public override IEnumerator Execute(EventContext ctx)
    {
        slots.Clear();
        host = ctx != null ? ctx.Runner : null;
        stopIssued = false;
        bool settled = false;

        try
        {
            if (ctx == null || ctx.Runner == null)
            {
                if (ctx != null)
                    ctx.RequestStop("Parallel group requires an EventRunner.");
                yield break;
            }

            if (actions == null)
                actions = new List<EventAction>();

            if (!TryValidate(ctx))
                yield break;

            int childCount = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null)
                    childCount++;
            }

            ctx.Log($"Parallel group started ({childCount}).");

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    Debug.LogWarning($"[Event:{ctx.Runner.name}] Parallel skipping null child at [{i}].", ctx.Runner);
                    continue;
                }

                var slot = new ChildSlot { action = action };
                slots.Add(slot);
                slot.routine = ctx.Runner.StartCoroutine(RunChild(slot, ctx));
            }

            if (slots.Count == 0)
            {
                settled = true;
                ctx.Log("Parallel group settled.");
                yield break;
            }

            if (ctx.StopRequested || ctx.IsCancelled)
            {
                StopRemaining(ctx);
                yield break;
            }

            while (AnyRunning())
            {
                yield return null;
                if (stopIssued || ctx.IsCancelled)
                    yield break;

                if (ctx.StopRequested)
                {
                    StopRemaining(ctx);
                    yield break;
                }
            }

            if (ctx.StopRequested || ctx.IsCancelled || stopIssued)
                yield break;

            settled = true;
            ctx.Log("Parallel group settled.");
        }
        finally
        {
            if (!settled)
                StopRemaining(ctx);
        }
    }

    public override void Cancel(EventContext ctx)
    {
        StopRemaining(ctx);
    }

    bool TryValidate(EventContext ctx)
    {
        int poseCount = 0;
        var moveBodies = new System.Collections.Generic.HashSet<Transform>();
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action == null)
                continue;

            if (!TryValidateWrapper(action, ctx))
                return false;

            // Delays do not license conflicting ownership — inspect the leaf.
            var leaf = DelayedStartAction.Unwrap(action);
            if (leaf == null)
                continue;

            if (leaf is ParallelActions)
            {
                ctx.RequestStop("Parallel groups cannot be nested.");
                return false;
            }

            if (IsPoseAction(leaf))
                poseCount++;

            if (leaf is MoveCharacterAction move)
            {
                if (move.character != null &&
                    move.character.TryResolve(out Transform body, out _, out _, out _))
                {
                    if (!moveBodies.Add(body))
                    {
                        ctx.RequestStop("Parallel group has two moves on the same character.");
                        return false;
                    }
                }
            }
        }

        if (poseCount > 1)
        {
            ctx.RequestStop("Parallel group has more than one camera pose action.");
            return false;
        }

        return true;
    }

    static bool TryValidateWrapper(EventAction action, EventContext ctx)
    {
        if (action is DelayedStartAction delay)
        {
            if (delay.action == null)
            {
                ctx.RequestStop("DelayedStart has no child action.");
                return false;
            }

            if (delay.action is DelayedStartAction)
            {
                ctx.RequestStop("DelayedStart cannot wrap another DelayedStart.");
                return false;
            }

            if (delay.action is ParallelActions)
            {
                ctx.RequestStop("DelayedStart cannot wrap ParallelActions.");
                return false;
            }
        }

        return true;
    }

    static bool IsPoseAction(EventAction action)
    {
        return action is CameraFocusAction
            || action is CameraShotAction
            || action is CameraMoveAction
            || action is CameraRestoreAction;
    }

    bool AnyRunning()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].finished)
                return true;
        }

        return false;
    }

    IEnumerator RunChild(ChildSlot slot, EventContext ctx)
    {
        IEnumerator step = null;
        bool failed = false;
        try
        {
            step = slot.action.Execute(ctx);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, host);
            ctx.RequestStop("Parallel child failed.");
            failed = true;
        }

        if (!failed && step != null)
        {
            while (true)
            {
                bool more = false;
                object current = null;
                try
                {
                    more = step.MoveNext();
                    if (more)
                        current = step.Current;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, host);
                    ctx.RequestStop("Parallel child failed.");
                    failed = true;
                }

                if (failed || !more)
                    break;

                yield return current;
            }
        }

        slot.finished = true;
        slot.routine = null;
    }

    void StopRemaining(EventContext ctx)
    {
        if (stopIssued)
            return;

        stopIssued = true;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.finished)
                continue;

            try
            {
                slot.action?.Cancel(ctx);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, host);
            }

            if (host != null && slot.routine != null)
            {
                try { host.StopCoroutine(slot.routine); }
                catch (Exception ex) { Debug.LogException(ex, host); }
            }

            slot.routine = null;
            slot.finished = true;
        }
    }
}
