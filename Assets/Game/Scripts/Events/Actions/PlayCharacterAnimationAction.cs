using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a 2D Animator state on a character (hard cut, no crossfade).
/// Hold = stay in pose until the EventRunner ends. Play Once = wait for the clip, then continue.
/// </summary>
[System.Serializable]
public class PlayCharacterAnimationAction : EventAction
{
    public enum Mode
    {
        /// <summary>Force the pose until the event finishes or is cancelled.</summary>
        HoldUntilEventEnds = 0,
        /// <summary>Play the clip once, then return control and continue the event.</summary>
        PlayOnce = 1,
    }

    public ScriptedCharacterTarget character = new ScriptedCharacterTarget();

    [Tooltip("Animator state name on the SpriteBody (or NPC) Animator.")]
    public string stateName = "Idle";

    [Min(0)]
    public int layer;

    [Tooltip("Hold = keep this pose for the rest of the event. Play Once = play clip then continue.")]
    public Mode mode = Mode.HoldUntilEventEnds;

    [Tooltip("Safety cap for Play Once only (looping clips).")]
    [Min(0.25f)]
    public float playOnceTimeoutSeconds = 8f;

    // Legacy fields kept so old scenes don't lose data; ignored at runtime.
    public bool waitUntilFinished = true;
    public float timeoutSeconds = 2f;
    public float crossFade;
    public bool keepAnimation;

    TopDownAnimDriver boundDriver;
    ScriptedNpcMotor boundNpc;
    Animator boundAnimator;
    EventAnimPoseHold poseHold;
    bool owning;
    bool setInCombat;
    EventRunner holdRunner;

    static readonly List<PlayCharacterAnimationAction> heldByEvents =
        new List<PlayCharacterAnimationAction>(8);

    static readonly int InCombatHash = Animator.StringToHash("InCombat");

    public override string DisplayName =>
        string.IsNullOrEmpty(stateName)
            ? "Play Animation (?)"
            : mode == Mode.HoldUntilEventEnds
                ? $"Hold '{stateName}'"
                : $"Play Once '{stateName}'";

    public override IEnumerator Execute(EventContext ctx)
    {
        ClearLocalBindings();

        if (string.IsNullOrEmpty(stateName))
        {
            ctx?.RequestStop("animation state name is empty");
            yield break;
        }

        if (!character.TryResolve(out Transform body, out PlayerMotor playerMotor, out ScriptedNpcMotor npcMotor, out string failure))
        {
            ctx?.RequestStop(failure);
            yield break;
        }

        ReleaseHeldForBody(body);

        if (playerMotor != null)
        {
            var combat = body.GetComponent<PlayerCombatController>();
            if (combat != null && (combat.IsAttacking() || combat.IsRolling()))
            {
                ctx?.RequestStop("animation rejected — combat owns the body");
                yield break;
            }

            var climbing = body.GetComponent<PlayerClimbing>();
            if (climbing != null && climbing.IsClimbing())
            {
                ctx?.RequestStop("animation rejected — climbing owns the animator");
                yield break;
            }

            boundDriver = body.GetComponentInChildren<TopDownAnimDriver>(true);
            boundAnimator = ResolvePlayerAnimator(body, boundDriver);
            if (boundAnimator == null)
            {
                ctx?.RequestStop("no Animator on character");
                yield break;
            }

            if (!HasState(boundAnimator, stateName, layer))
            {
                ctx?.RequestStop($"animator state '{stateName}' not found");
                yield break;
            }

            if (boundDriver != null)
                boundDriver.enabled = false;
            owning = true;
        }
        else
        {
            boundNpc = npcMotor;
            boundAnimator = npcMotor.animator != null
                ? npcMotor.animator
                : npcMotor.GetComponentInChildren<Animator>(true);
            if (boundAnimator == null)
            {
                ctx?.RequestStop("no Animator on NPC");
                yield break;
            }

            if (!HasState(boundAnimator, stateName, layer))
            {
                ctx?.RequestStop($"animator state '{stateName}' not found");
                yield break;
            }

            if (!npcMotor.TryBeginAnimationOwner(this))
            {
                ctx?.RequestStop("NPC animation owner busy");
                yield break;
            }

            owning = true;
        }

        // CombatIdle (and similar) exit instantly when InCombat is false.
        setInCombat = HasBool(boundAnimator, InCombatHash) &&
                      stateName.IndexOf("Combat", System.StringComparison.OrdinalIgnoreCase) >= 0;

        BeginPose();
        ctx?.Log(
            mode == Mode.HoldUntilEventEnds
                ? $"Hold animation '{stateName}' until event ends."
                : $"Play once '{stateName}'.");

        yield return null;

        if (mode == Mode.HoldUntilEventEnds)
        {
            if (ctx?.Runner != null)
            {
                holdRunner = ctx.Runner;
                if (!heldByEvents.Contains(this))
                    heldByEvents.Add(this);
            }
            else
            {
                RestoreOwnership();
            }

            yield break;
        }

        // Play Once: wait for non-loop clip end or timeout, then restore.
        float elapsed = 0f;
        float limit = Mathf.Max(0.25f, playOnceTimeoutSeconds);
        try
        {
            while (elapsed < limit)
            {
                if (ctx != null && (ctx.IsCancelled || ctx.StopRequested))
                    yield break;

                var info = boundAnimator.GetCurrentAnimatorStateInfo(layer);
                if (info.IsName(stateName) && !info.loop && info.normalizedTime >= 1f)
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            RestoreOwnership();
        }
    }

    public override void Cancel(EventContext ctx)
    {
        RestoreOwnership();
    }

    public static void ReleaseHeldForRunner(EventRunner runner)
    {
        if (runner == null || heldByEvents.Count == 0)
            return;

        for (int i = heldByEvents.Count - 1; i >= 0; i--)
        {
            var action = heldByEvents[i];
            if (action == null || action.holdRunner != runner)
                continue;
            action.RestoreOwnership();
        }
    }

    static void ReleaseHeldForBody(Transform body)
    {
        if (body == null || heldByEvents.Count == 0)
            return;

        for (int i = heldByEvents.Count - 1; i >= 0; i--)
        {
            var action = heldByEvents[i];
            if (action == null || action.boundAnimator == null)
                continue;
            if (!action.boundAnimator.transform.IsChildOf(body) &&
                action.boundAnimator.transform != body)
                continue;
            action.RestoreOwnership();
        }
    }

    void BeginPose()
    {
        if (boundAnimator == null)
            return;

        poseHold = boundAnimator.GetComponent<EventAnimPoseHold>();
        if (poseHold == null)
            poseHold = boundAnimator.gameObject.AddComponent<EventAnimPoseHold>();

        poseHold.animator = boundAnimator;
        poseHold.layer = layer;
        poseHold.stateName = stateName;
        poseHold.setInCombat = setInCombat;
        poseHold.inCombatHash = InCombatHash;
        poseHold.enabled = true;
        poseHold.ApplyNow();
    }

    void RestoreOwnership()
    {
        heldByEvents.Remove(this);
        holdRunner = null;

        if (poseHold != null)
        {
            if (setInCombat && boundAnimator != null)
                boundAnimator.SetBool(InCombatHash, false);

            Object.Destroy(poseHold);
            poseHold = null;
        }

        if (!owning)
            return;

        if (boundDriver != null)
            boundDriver.enabled = true;

        if (boundNpc != null)
            boundNpc.ReleaseAnimationOwner(this);

        ClearLocalBindings();
    }

    void ClearLocalBindings()
    {
        boundDriver = null;
        boundNpc = null;
        boundAnimator = null;
        owning = false;
        setInCombat = false;
    }

    static Animator ResolvePlayerAnimator(Transform body, TopDownAnimDriver driver)
    {
        if (driver != null)
        {
            var anim = driver.GetComponent<Animator>();
            if (anim != null)
                return anim;
        }

        return body.GetComponentInChildren<Animator>(true);
    }

    static bool HasState(Animator animator, string name, int layerIndex)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;
        if (layerIndex < 0 || layerIndex >= animator.layerCount)
            return false;
        return animator.HasState(layerIndex, Animator.StringToHash(name));
    }

    static bool HasBool(Animator animator, int hash)
    {
        if (animator == null)
            return false;
        for (int i = 0; i < animator.parameterCount; i++)
        {
            var p = animator.GetParameter(i);
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }
}
