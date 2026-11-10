using System.Collections;
using UnityEngine;

[System.Serializable]
public class ClearFlagAction : EventAction
{
    public string flagId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(flagId) ? "ClearFlag (?)" : $"ClearFlag '{flagId}'";

    public override IEnumerator Execute(EventContext ctx)
    {
        WorldState.ClearFlag(flagId);
        if (ctx.Runner != null && ctx.Runner.debugLog)
            Debug.Log($"[Event:{ctx.Runner.name}] ClearFlag '{flagId}'", ctx.Runner);
        yield break;
    }
}
