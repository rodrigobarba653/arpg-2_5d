using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Starts one interactive dialogue from an event and, by default, waits for that session.
/// Does not acquire GameplayLock. The session locks only when its asset says so.
/// </summary>
[System.Serializable]
public class StartDialogueAction : EventAction
{
    public DialogueAsset dialogueAsset;

    public List<DialogueCharacterAssetOverride> dialogueByCharacter =
        new List<DialogueCharacterAssetOverride>();

    [Tooltip("Scene object this dialogue is tied to (NPC root). Required when the Dialogue Asset origin is Npc. " +
             "Ignored when origin is Active Player and every line is the player.")]
    public Transform npcAnchor;

    [Tooltip("Speaker id that maps to Npc Anchor. Leave empty to use the Dialogue Asset origin speaker id.")]
    public string npcSpeakerId = "";

    [Tooltip("Yield until the session started by this action finishes.")]
    public bool waitUntilFinished = true;

    public bool allowCancel = true;

    public DialogueProgressionMode progressionMode = DialogueProgressionMode.Manual;

    [Tooltip("Automatic mode only. Negative uses the session default (1.75s). 0 advances immediately.")]
    public float autoAdvanceHoldSeconds = 1.75f;

    DialogueSession session;

    public override string DisplayName =>
        dialogueAsset == null
            ? "Start Dialogue"
            : $"Start Dialogue '{dialogueAsset.dialogueId}'";

    public override IEnumerator Execute(EventContext ctx)
    {
        session = null;

        var service = DialogueService.Ensure();
        if (service == null)
        {
            ctx.RequestStop("no DialogueService");
            yield break;
        }

        var asset = DialogueCharacterAssetResolver.ResolveForActivePartyMember(
            dialogueAsset, dialogueByCharacter);
        if (asset == null)
        {
            ctx.RequestStop("no DialogueAsset");
            yield break;
        }

        DialogueActiveCharacter.TryGetActiveCharacterId(out string characterId);
        DialogueActiveCharacter.TryGetActiveTransform(out Transform actor);

        // Prefer per-line DialogueSpeakerBinding in the scene. Legacy npcAnchor/npcSpeakerId
        // remain for NpcDialogueHost and Sprint fixtures.
        string resolvedNpcSpeakerId = ResolveNpcSpeakerId(asset);

        bool started = service.TryStartInteractive(
            asset,
            npcAnchor: npcAnchor,
            npcSpeakerId: resolvedNpcSpeakerId,
            manualOffsetRef: Vector2.zero,
            preferred: DialogueChatboxPlacement.Auto,
            scale: 1f,
            lockGameplayOverride: null,
            allowCancel: allowCancel,
            initiatorCharacterId: characterId,
            initiatorTransform: actor,
            progressionMode: progressionMode,
            autoAdvanceHoldSeconds: autoAdvanceHoldSeconds);

        if (!started)
        {
            ctx.RequestStop($"dialogue rejected asset='{asset.dialogueId}'");
            yield break;
        }

        session = service.LastInteractiveSession;
        if (session == null || session.Asset != asset)
        {
            session = null;
            ctx.RequestStop($"dialogue session missing asset='{asset.dialogueId}'");
            yield break;
        }

        if (!waitUntilFinished)
        {
            ctx.Log($"Dialogue started '{asset.dialogueId}' (not waiting).");
            session = null;
            yield break;
        }

        while (!session.IsFinished)
        {
            if (ctx.IsCancelled)
                yield break;
            yield return null;
        }

        var ended = session;
        session = null;
        var outcome = ended.Outcome;
        ctx.Log($"Dialogue outcome={outcome} asset='{asset.dialogueId}'.");
        if (outcome != DialogueSessionOutcome.Completed)
            ctx.RequestStop($"dialogue {outcome} asset='{asset.dialogueId}'");
    }

    public override void Cancel(EventContext ctx)
    {
        var running = session;
        session = null;
        if (running != null && !running.IsFinished)
            running.Cancel("event-cancel");
    }

    /// <summary>
    /// Prefer the authored override, else the Dialogue Asset origin speaker id.
    /// </summary>
    public string ResolveNpcSpeakerId(DialogueAsset asset)
    {
        if (!string.IsNullOrEmpty(npcSpeakerId))
            return npcSpeakerId.Trim();
        if (asset == null)
            return "";
        string fromOrigin = asset.GetOriginNpcSpeakerId();
        if (!string.IsNullOrEmpty(fromOrigin))
            return fromOrigin;
        return asset.FindFirstNonPlayerSpeakerId();
    }
}
