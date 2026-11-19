using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Receives <see cref="DialogueCheckpointMarker"/> notifications while a
/// <see cref="PlayTimelineAction"/> is bound. Owns dialogue sessions started
/// from Timeline so cancel can clean them up.
/// </summary>
[DisallowMultipleComponent]
public class TimelineDialogueBridge : MonoBehaviour, INotificationReceiver
{
    EventContext activeContext;
    PlayableDirector boundDirector;
    DialogueSession ownedSession;
    Coroutine pauseWaitRoutine;
    bool pauseWaitActive;

    public bool HasActiveBinding => activeContext != null && boundDirector != null;
    public bool OwnsRunningDialogue =>
        ownedSession != null && !ownedSession.IsFinished;

    public void Bind(EventContext ctx, PlayableDirector director)
    {
        Unbind(cancelOwnedDialogue: true);
        activeContext = ctx;
        boundDirector = director;
    }

    public void Unbind(bool cancelOwnedDialogue)
    {
        StopPauseWaitRoutine();
        pauseWaitActive = false;

        if (cancelOwnedDialogue)
            CancelOwnedDialogue("timeline-unbind");

        activeContext = null;
        boundDirector = null;
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is not DialogueCheckpointMarker marker)
            return;

        if (!HasActiveBinding)
        {
            Debug.LogWarning(
                "[TimelineDialogueBridge] DialogueCheckpoint ignored — no PlayTimelineAction bound.",
                this);
            return;
        }

        if (marker.dialogueAsset == null)
        {
            FailCinematic("DialogueCheckpoint has no DialogueAsset.");
            return;
        }

        if (pauseWaitActive)
        {
            FailCinematic("DialogueCheckpoint while another Pause & Wait is active.");
            return;
        }

        if (marker.mode == DialogueCheckpointMarker.Mode.PauseAndWait)
            BeginPauseAndWait(marker);
        else
            BeginContinue(marker);
    }

    void BeginPauseAndWait(DialogueCheckpointMarker marker)
    {
        var director = boundDirector;
        var ctx = activeContext;
        if (director == null || ctx == null)
            return;

        director.Pause();
        pauseWaitActive = true;
        ctx.Log($"DialogueCheckpoint Pause&Wait '{marker.dialogueAsset.dialogueId}' @ {director.time:0.00}s.");

        if (!TryStartDialogue(marker, out DialogueSession session))
        {
            pauseWaitActive = false;
            FailCinematic($"dialogue rejected asset='{marker.dialogueAsset.dialogueId}'");
            return;
        }

        ownedSession = session;
        StopPauseWaitRoutine();
        pauseWaitRoutine = StartCoroutine(WaitThenResume(session, marker.dialogueAsset.dialogueId));
    }

    void BeginContinue(DialogueCheckpointMarker marker)
    {
        var ctx = activeContext;
        var director = boundDirector;
        if (ctx == null || director == null)
            return;

        ctx.Log($"DialogueCheckpoint Continue '{marker.dialogueAsset.dialogueId}' @ {director.time:0.00}s.");

        if (!TryStartDialogue(marker, out DialogueSession session))
        {
            FailCinematic($"dialogue rejected asset='{marker.dialogueAsset.dialogueId}'");
            return;
        }

        ownedSession = session;
        // Timeline keeps playing. Ownership stays until session ends or Unbind.
        StopPauseWaitRoutine();
        pauseWaitRoutine = StartCoroutine(WatchContinueSession(session, marker.dialogueAsset.dialogueId));
    }

    bool TryStartDialogue(DialogueCheckpointMarker marker, out DialogueSession session)
    {
        session = null;
        var service = DialogueService.Ensure();
        if (service == null)
            return false;

        DialogueActiveCharacter.TryGetActiveCharacterId(out string characterId);
        DialogueActiveCharacter.TryGetActiveTransform(out Transform actor);

        bool started = service.TryStartInteractive(
            marker.dialogueAsset,
            npcAnchor: null,
            npcSpeakerId: "",
            manualOffsetRef: Vector2.zero,
            preferred: DialogueChatboxPlacement.Auto,
            scale: 1f,
            lockGameplayOverride: null,
            allowCancel: marker.allowCancel,
            initiatorCharacterId: characterId,
            initiatorTransform: actor,
            progressionMode: marker.progressionMode,
            autoAdvanceHoldSeconds: marker.autoAdvanceHoldSeconds);

        if (!started)
            return false;

        session = service.LastInteractiveSession;
        return session != null && session.Asset == marker.dialogueAsset;
    }

    IEnumerator WaitThenResume(DialogueSession session, string assetId)
    {
        try
        {
            while (session != null && !session.IsFinished)
            {
                if (activeContext != null &&
                    (activeContext.IsCancelled || activeContext.StopRequested))
                    yield break;

                yield return null;
            }

            if (activeContext != null &&
                (activeContext.IsCancelled || activeContext.StopRequested))
                yield break;

            var outcome = session != null ? session.Outcome : DialogueSessionOutcome.Failed;
            activeContext?.Log($"DialogueCheckpoint outcome={outcome} asset='{assetId}'.");

            if (outcome != DialogueSessionOutcome.Completed)
            {
                FailCinematic($"dialogue {outcome} asset='{assetId}'");
                yield break;
            }

            if (boundDirector != null)
                boundDirector.Resume();
        }
        finally
        {
            pauseWaitActive = false;
            pauseWaitRoutine = null;
            if (ownedSession == session)
                ownedSession = null;
        }
    }

    IEnumerator WatchContinueSession(DialogueSession session, string assetId)
    {
        try
        {
            while (session != null && !session.IsFinished)
            {
                if (activeContext != null &&
                    (activeContext.IsCancelled || activeContext.StopRequested))
                    yield break;

                yield return null;
            }

            if (activeContext == null ||
                activeContext.IsCancelled ||
                activeContext.StopRequested)
                yield break;

            var outcome = session != null ? session.Outcome : DialogueSessionOutcome.Failed;
            activeContext.Log($"DialogueCheckpoint (Continue) outcome={outcome} asset='{assetId}'.");

            // Continue mode: Cancelled/Failed still fail the cinematic so the
            // EventRunner does not silently Complete after abandoning dialogue.
            if (outcome != DialogueSessionOutcome.Completed)
                FailCinematic($"dialogue {outcome} asset='{assetId}'");
        }
        finally
        {
            pauseWaitRoutine = null;
            if (ownedSession == session)
                ownedSession = null;
        }
    }

    void FailCinematic(string reason)
    {
        StopPauseWaitRoutine();
        pauseWaitActive = false;
        CancelOwnedDialogue("checkpoint-fail");

        if (activeContext != null)
            activeContext.RequestStop(reason);

        if (boundDirector != null)
        {
            try { boundDirector.Stop(); }
            catch (System.Exception ex) { Debug.LogException(ex, this); }
        }
    }

    void CancelOwnedDialogue(string reason)
    {
        var session = ownedSession;
        ownedSession = null;
        if (session != null && !session.IsFinished)
            session.Cancel(reason);
    }

    void StopPauseWaitRoutine()
    {
        if (pauseWaitRoutine == null)
            return;
        StopCoroutine(pauseWaitRoutine);
        pauseWaitRoutine = null;
    }

    void OnDisable()
    {
        Unbind(cancelOwnedDialogue: true);
    }
}
