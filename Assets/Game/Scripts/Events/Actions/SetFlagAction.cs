using System.Collections;
using UnityEngine;

[System.Serializable]
public class SetFlagAction : EventAction
{
    public string flagId = "";
    public bool value = true;

    public override string DisplayName =>
        string.IsNullOrEmpty(flagId)
            ? "SetFlag (?)"
            : $"SetFlag '{flagId}' = {value}";

    public override IEnumerator Execute(EventContext ctx)
    {
        WorldState.SetFlag(flagId, value);
        if (ctx.Runner != null && ctx.Runner.debugLog)
            Debug.Log($"[Event:{ctx.Runner.name}] SetFlag '{flagId}' = {value}", ctx.Runner);
        yield break;
    }
}
