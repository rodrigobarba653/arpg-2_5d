using System.Collections;
using UnityEngine;

/// <summary>
/// One-shot enable/disable inside an EventRunner sequence.
/// Prefer <see cref="FlagDrivenActive"/> when the object should also match the flag after load.
/// </summary>
[System.Serializable]
public class SetGameObjectActiveAction : EventAction
{
    public GameObject target;
    public bool active = true;

    public override string DisplayName =>
        target == null
            ? "Set Active (?)"
            : $"Set Active '{target.name}' = {active}";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (target == null)
        {
            if (ctx != null && ctx.Runner != null && ctx.Runner.debugLog)
                Debug.LogWarning($"[Event:{ctx.Runner.name}] SetGameObjectActive missing target.", ctx.Runner);
            yield break;
        }

        target.SetActive(active);
        if (ctx != null && ctx.Runner != null && ctx.Runner.debugLog)
            ctx.Log($"SetActive '{target.name}' = {active}");
        yield break;
    }
}
