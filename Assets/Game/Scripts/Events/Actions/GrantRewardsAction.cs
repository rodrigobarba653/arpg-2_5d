using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grants authored items and Varium. Does not claim a chest or write WorldState.
/// Safe when <see cref="EventContext.Runner"/> is null (dialogue consequences).
/// </summary>
[System.Serializable]
public class GrantRewardsAction : EventAction
{
    public List<RewardItemEntry> items = new List<RewardItemEntry>();

    [Min(0)]
    public int varium;

    public override string DisplayName =>
        varium > 0
            ? $"Grant {ItemCount} item(s) + {varium} Varium"
            : $"Grant {ItemCount} item(s)";

    int ItemCount => items != null ? items.Count : 0;

    public override IEnumerator Execute(EventContext ctx)
    {
        if (!RewardDelivery.TryGrant(items, varium, out string failure))
        {
            if (ctx != null)
                ctx.RequestStop(failure);
            yield break;
        }

        if (ctx != null && ctx.Runner != null && ctx.Runner.debugLog)
            ctx.Log("Rewards granted.");

        yield return RewardNoticePanel.WaitUntilIdle();
    }

    public override void Cancel(EventContext ctx)
    {
        RewardNoticePanel.CancelAll();
    }
}
