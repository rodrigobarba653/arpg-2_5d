using System.Collections;
using UnityEngine;

/// <summary>
/// Explicitly marks an event id completed in WorldState mid-sequence.
/// Successful EventRunner completion also marks <see cref="EventRunner.eventId"/>
/// automatically — use this only when you need to mark a *different* id early.
/// </summary>
[System.Serializable]
public class MarkEventCompletedAction : EventAction
{
    public string eventId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(eventId)
            ? "MarkEventCompleted (?)"
            : $"MarkEventCompleted '{eventId}'";

    public override IEnumerator Execute(EventContext ctx)
    {
        WorldState.MarkEventCompleted(eventId);
        if (ctx.Runner != null && ctx.Runner.debugLog)
            Debug.Log($"[Event:{ctx.Runner.name}] MarkEventCompleted '{eventId}'", ctx.Runner);
        yield break;
    }
}
