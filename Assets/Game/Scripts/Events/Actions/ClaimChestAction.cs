using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Claims one chest. The persistent id check, the reward grant, and the
/// WorldState flag happen before this action yields, so a later cancel
/// cannot separate the items from the claim.
/// </summary>
[System.Serializable]
public class ClaimChestAction : EventAction
{
    public ChestView chest;

    public List<RewardItemEntry> items = new List<RewardItemEntry>();

    [Min(0)]
    public int varium;

    public override string DisplayName =>
        chest != null && !string.IsNullOrEmpty(chest.persistentId)
            ? $"Claim chest '{chest.persistentId}'"
            : "Claim chest (?)";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (chest == null || string.IsNullOrEmpty(chest.persistentId))
        {
            if (ctx != null)
                ctx.RequestStop("chest claim missing persistentId");
            yield break;
        }

        if (WorldState.GetFlag(chest.persistentId))
        {
            if (ctx != null && ctx.Runner != null && ctx.Runner.debugLog)
                ctx.Log($"Chest '{chest.persistentId}' already claimed.");
            chest.ApplyState();
            yield break;
        }

        if (!RewardDelivery.TryGrant(items, varium, out string failure))
        {
            if (ctx != null)
                ctx.RequestStop(failure);
            yield break;
        }

        WorldState.SetFlag(chest.persistentId, true);
        chest.ApplyState();

        if (ctx != null && ctx.Runner != null && ctx.Runner.debugLog)
            ctx.Log($"Chest '{chest.persistentId}' claimed.");

        // Modal reward notice freezes the world until Confirm (South / ×).
        yield return RewardNoticePanel.WaitUntilIdle();
    }

    public override void Cancel(EventContext ctx)
    {
        RewardNoticePanel.CancelAll();
    }
}
