using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger-volume adapter that starts interactive-style dialogue without Interact.
/// Presentation = interactive; lock and progression are independent policies.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class NarrativeDialogueTrigger : MonoBehaviour
{
    public enum ActorFilter
    {
        ActivePlayerOnly = 0,
        AnyPartyMember = 1,
    }

    public enum FireMode
    {
        Repeat = 0,
        OncePerScene = 1,
        OncePersistent = 2,
    }

    public enum ConflictPolicy
    {
        /// <summary>Discard this activation if another interactive session is running.</summary>
        Ignore = 0,

        /// <summary>Keep at most one pending request; start when service is free if still valid.</summary>
        Queue = 1,

        /// <summary>Cancel the active interactive session and start this one immediately.</summary>
        Interrupt = 2,
    }

    public enum LockOverride
    {
        UseAsset = 0,
        ForceBlocking = 1,
        ForceNonBlocking = 2,
    }

    [Header("Identity")]
    [Tooltip("Stable id for OncePersistent WorldState flag. Empty → dialogueAsset.dialogueId or GameObject name.")]
    public string triggerId = "";

    [Header("Dialogue")]
    public DialogueAsset dialogueAsset;

    public List<DialogueCharacterAssetOverride> dialogueByCharacter =
        new List<DialogueCharacterAssetOverride>();

    public LockOverride lockGameplay = LockOverride.UseAsset;
    public bool allowCancel = true;

    [Header("Progression (independent of lock)")]
    public DialogueProgressionMode progressionMode = DialogueProgressionMode.Manual;

    [Tooltip("Seconds to keep a finished line visible before auto-advance (Automatic mode).")]
    public float autoAdvanceHoldSeconds = 1.75f;

    [Header("Trigger policy")]
    public ActorFilter actorFilter = ActorFilter.ActivePlayerOnly;
    public FireMode fireMode = FireMode.Repeat;

    [Min(0f)]
    public float retriggerCooldownSeconds = 2f;

    public ConflictPolicy conflictPolicy = ConflictPolicy.Ignore;

    [Tooltip("If Queue and the player leaves the volume, drop the pending request.")]
    public bool cancelPendingOnExit = true;

    [Header("Presentation (optional)")]
    public DialogueChatboxPlacement preferredPlacement = DialogueChatboxPlacement.Auto;
    public Vector2 manualOffset;
    public float scale = 1f;
    public float maxWidthOverride;

    Collider triggerCollider;
    bool isInside;
    bool sceneConsumed;
    bool pending;
    bool finishCallbackQueued;
    float cooldownUntil;
    DialogueSession startedSession;
    PlayerHealth pendingActor;
    Transform pendingAnchor;

    public string ResolvedTriggerId
    {
        get
        {
            if (!string.IsNullOrEmpty(triggerId))
                return triggerId;
            if (dialogueAsset != null && !string.IsNullOrEmpty(dialogueAsset.dialogueId))
                return dialogueAsset.dialogueId;
            return gameObject.name;
        }
    }

    public string PersistentFlagId => $"dialogue.narrative.{ResolvedTriggerId}.seen";

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled)
            return;

        if (!TryResolveActor(other, out var actor, out var anchor))
            return;

        isInside = true;
        TryActivate(actor, anchor);
    }

    void OnTriggerExit(Collider other)
    {
        if (!TryResolveActor(other, out _, out _))
            return;

        isInside = false;
        if (cancelPendingOnExit)
            pending = false;
    }

    void TryActivate(PlayerHealth actor, Transform anchor)
    {
        if (!CanFireNow())
            return;

        var service = DialogueService.Ensure();
        if (service == null)
            return;

        if (service.HasInteractive)
        {
            if (conflictPolicy == ConflictPolicy.Ignore)
                return;

            if (conflictPolicy == ConflictPolicy.Queue)
            {
                // One pending only for this trigger.
                pending = true;
                pendingActor = actor;
                pendingAnchor = anchor;
                EnsureFinishCallback(service);
                return;
            }

            // Interrupt: stop the active session, then start ours.
            // Interrupted session is cancelled → its Once flags are NOT consumed.
            pending = false;
            pendingActor = null;
            pendingAnchor = null;
            service.CancelInteractive("narrative-interrupt");
        }

        StartDialogue(service, actor, anchor);
    }

    void EnsureFinishCallback(DialogueService service)
    {
        if (finishCallbackQueued)
            return;
        finishCallbackQueued = true;
        service.EnqueueAfterInteractive(OnInteractiveSlotFree);
    }

    void OnInteractiveSlotFree()
    {
        finishCallbackQueued = false;

        // Completion of a session we started.
        if (startedSession != null)
        {
            bool success = startedSession.Outcome == DialogueSessionOutcome.Completed;
            startedSession = null;
            if (success)
                ConsumeOnce();
            cooldownUntil = Time.time + Mathf.Max(0f, retriggerCooldownSeconds);
        }

        if (!pending)
            return;

        if (cancelPendingOnExit && !isInside)
        {
            pending = false;
            pendingActor = null;
            pendingAnchor = null;
            return;
        }

        if (!CanFireNow())
        {
            pending = false;
            return;
        }

        var service = DialogueService.Ensure();
        if (service == null || service.HasInteractive)
        {
            // Still busy — keep pending and re-arm callback.
            if (pending && conflictPolicy == ConflictPolicy.Queue)
                EnsureFinishCallback(service);
            return;
        }

        var actor = pendingActor;
        var anchor = pendingAnchor;
        pending = false;
        pendingActor = null;
        pendingAnchor = null;

        if (actor == null || anchor == null)
            return;

        StartDialogue(service, actor, anchor);
    }

    void StartDialogue(DialogueService service, PlayerHealth actor, Transform anchor)
    {
        string characterId = actor.character != null ? actor.character.id : "";
        var asset = DialogueCharacterAssetResolver.Resolve(
            dialogueAsset, dialogueByCharacter, characterId);
        if (asset == null)
        {
            Debug.LogWarning(
                $"[NarrativeDialogueTrigger] No DialogueAsset for '{characterId}' (trigger '{ResolvedTriggerId}').",
                this);
            return;
        }

        bool? lockOverride = lockGameplay switch
        {
            LockOverride.ForceBlocking => true,
            LockOverride.ForceNonBlocking => false,
            _ => null,
        };

        bool started = service.TryStartInteractive(
            asset,
            npcAnchor: null,
            npcSpeakerId: "",
            manualOffsetRef: manualOffset,
            preferred: preferredPlacement,
            scale: scale,
            maxWidthRef: maxWidthOverride,
            lockGameplayOverride: lockOverride,
            allowCancel: allowCancel,
            initiatorCharacterId: characterId,
            initiatorTransform: anchor,
            progressionMode: progressionMode,
            autoAdvanceHoldSeconds: autoAdvanceHoldSeconds);

        if (!started)
        {
            // Failed start — do not consume Once; allow retry on next enter / queue.
            return;
        }

        startedSession = service.InteractiveSession;
        EnsureFinishCallback(service);
    }

    bool CanFireNow()
    {
        if (Time.time < cooldownUntil)
            return false;

        if (fireMode == FireMode.OncePerScene && sceneConsumed)
            return false;

        if (fireMode == FireMode.OncePersistent && WorldState.GetFlag(PersistentFlagId))
            return false;

        return true;
    }

    void ConsumeOnce()
    {
        if (fireMode == FireMode.OncePerScene)
            sceneConsumed = true;
        else if (fireMode == FireMode.OncePersistent)
            WorldState.SetFlag(PersistentFlagId, true);
    }

    bool TryResolveActor(Collider other, out PlayerHealth actor, out Transform anchor)
    {
        actor = null;
        anchor = null;
        if (other == null)
            return false;

        actor = other.GetComponentInParent<PlayerHealth>();
        if (actor == null)
            return false;

        if (actorFilter == ActorFilter.ActivePlayerOnly && !Party.IsActive(actor))
            return false;

        // AnyPartyMember: any registered PlayerHealth is enough (no Party API changes).
        if (actor.character == null || string.IsNullOrEmpty(actor.character.id))
            return false;

        anchor = actor.transform;
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var col = triggerCollider != null ? triggerCollider : GetComponent<Collider>();
        if (col == null)
            return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
        }
    }
#endif
}
