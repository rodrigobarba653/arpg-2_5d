using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Waits <see cref="startDelay"/> seconds, then runs exactly one child action.
/// Used for staggered starts inside <see cref="ParallelActions"/>. Authors
/// normally set the delay in the Parallel drawer; the wrapper is the
/// serialization form.
/// </summary>
[Serializable]
public class DelayedStartAction : EventAction
{
    [Min(0f)]
    public float startDelay;

    [SerializeReference]
    public EventAction action;

    EventAction runningChild;

    public override string DisplayName
    {
        get
        {
            string child = action != null ? action.DisplayName : "(empty)";
            if (startDelay <= 0.0001f)
                return $"Delay → {child}";
            return $"Delay {startDelay:0.##}s → {child}";
        }
    }

    public override IEnumerator Execute(EventContext ctx)
    {
        runningChild = null;

        if (action == null)
        {
            ctx?.RequestStop("DelayedStart has no child action.");
            yield break;
        }

        if (action is DelayedStartAction)
        {
            ctx?.RequestStop("DelayedStart cannot wrap another DelayedStart.");
            yield break;
        }

        if (action is ParallelActions)
        {
            ctx?.RequestStop("DelayedStart cannot wrap ParallelActions.");
            yield break;
        }

        float remaining = Mathf.Max(0f, startDelay);
        ctx?.Log($"Delayed start {remaining:0.##}s → {action.DisplayName}.");

        while (remaining > 0f)
        {
            if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
                yield break;

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
            yield break;

        runningChild = action;
        var step = action.Execute(ctx);
        while (step.MoveNext())
        {
            if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
                yield break;

            yield return step.Current;
        }

        runningChild = null;
    }

    public override void Cancel(EventContext ctx)
    {
        if (runningChild != null)
        {
            try { runningChild.Cancel(ctx); }
            catch (Exception ex) { Debug.LogException(ex); }
            runningChild = null;
        }
    }

    /// <summary>
    /// Returns the innermost non-delay action. Does not recurse into Parallel.
    /// </summary>
    public static EventAction Unwrap(EventAction action)
    {
        int guard = 0;
        while (action is DelayedStartAction delay && guard++ < 8)
            action = delay.action;
        return action;
    }
}
