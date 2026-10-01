using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerClimbing : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor motor;
    public PlayerJump jump;
    public Animator animator;
    PlayerCombatController combat;

    [Header("Climb Settings")]
    public float climbSpeed = 1.7f;

    [Tooltip("Vertical input magnitude needed to start climbing while standing inside a ladder.")]
    [Range(0.05f, 1f)]
    public float enterUpThreshold = 0.4f;

    [Tooltip("How long the player smoothly aligns to the ladder's X/Z center on enter.")]
    public float snapDuration = 0.08f;

    [Tooltip("Cooldown after exiting a ladder before it can grab the player again.")]
    public float ignoreDuration = 0.5f;

    [Header("Top Exit")]
    [Tooltip("Y threshold relative to ladder topPoint at which the player is auto-ejected to the floor above.")]
    public float topExitYThreshold = 0.15f;

    [Header("Bottom Exit")]
    [Tooltip("If the player descends below this offset from the bottomPoint AND is grounded, they leave the ladder cleanly.")]
    public float bottomExitYThreshold = 0.15f;

    [Header("Jump Off")]
    [Tooltip("Optional InputAction to drop off the ladder. If empty, falls back to default Interact bindings (Space / X / A).")]
    public InputActionReference jumpOffAction;

    [Tooltip("Vertical velocity given to the player when jumping off the ladder.")]
    public float jumpOffForce = 4f;

    [Header("Animation")]
    public string climbStateName = "Climb-Up";
    public string climbEndStateName = "ClimbEnd";
    public string isClimbingBool = "isClimbing";
    public string climbSpeedFloat = "ClimbSpeed";
    public string climbEndSpeedFloat = "ClimbEndSpeed";

    [Tooltip("How long the climb-off move takes if the clip length cannot be read.")]
    public float climbEndDuration = 0.6f;

    [Tooltip("Print the ClimbSpeed value to the console each frame while climbing.")]
    public bool debugClimbSpeed = false;

    bool isClimbing;
    Ladder currentLadder;
    Ladder candidateLadder;
    bool ignoreLadder;
    float ignoreTimer;
    Coroutine snapRoutine;
    float originalGravity;

    int climbStateHash;
    int climbEndStateHash;
    int isClimbingHash;
    int climbSpeedHash;
    int climbEndSpeedHash;

    InputAction defaultJumpOff;
    InputAction climbDownAction;

    Ladder topMountLadder;
    bool topMountReady;
    bool topMountFromLanding;
    float topMountSettleTimer;
    Ladder bottomApproach;
    Coroutine climbEndRoutine;
    bool playingClimbEnd;
    float dismountIdleTimer;
    Vector2 dismountIdleDir;

    public bool IsClimbing() => isClimbing;

    public bool TryGetDismountIdle(out Vector2 dir)
    {
        if (dismountIdleTimer <= 0f)
        {
            dir = default;
            return false;
        }

        dir = dismountIdleDir;
        return true;
    }

    void Awake()
    {
        if (!motor)    motor    = GetComponent<PlayerMotor>();
        if (!jump)     jump     = GetComponent<PlayerJump>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        combat = GetComponent<PlayerCombatController>();

        if (motor != null)
            originalGravity = motor.gravity;

        climbStateHash    = Animator.StringToHash(climbStateName);
        climbEndStateHash = Animator.StringToHash(climbEndStateName);
        isClimbingHash    = Animator.StringToHash(isClimbingBool);
        climbSpeedHash    = Animator.StringToHash(climbSpeedFloat);
        climbEndSpeedHash = Animator.StringToHash(climbEndSpeedFloat);
    }

    void OnEnable()
    {
        if (jumpOffAction != null && jumpOffAction.action != null)
            jumpOffAction.action.Enable();

        if (climbDownAction == null)
        {
            // Keyboard I, PlayStation Triangle (buttonNorth). Xbox Y uses the same button.
            climbDownAction = new InputAction("ClimbDown", InputActionType.Button);
            climbDownAction.AddBinding("<Keyboard>/i");
            climbDownAction.AddBinding("<Gamepad>/buttonNorth");
        }
        climbDownAction.Enable();
    }

    void OnDisable()
    {
        climbDownAction?.Disable();
    }

    void Update()
    {
        // Ignore timer
        if (ignoreLadder)
        {
            ignoreTimer -= Time.deltaTime;
            if (ignoreTimer <= 0f)
                ignoreLadder = false;
        }

        if (dismountIdleTimer > 0f)
        {
            dismountIdleTimer -= Time.deltaTime;
            ApplyDismountIdle();
            if (motor != null)
                motor.LockMovement(true);
            if (dismountIdleTimer <= 0f && motor != null)
                motor.LockMovement(false);
        }

        if (topMountSettleTimer > 0f)
            topMountSettleTimer -= Time.deltaTime;

        if (playingClimbEnd)
            return;

        if (!isClimbing)
        {
            TryStartClimbDown();
            TryStartClimb();
            return;
        }

        // Active climb
        motor.SetVerticalVelocity(0f);

        HandleClimbMovement();
        CheckTopExit();
        CheckBottomExit();
        CheckJumpOff();
    }

    // =========================
    // CANDIDATE TRACKING (called by Ladder)
    // =========================
    public void SetTopMount(Ladder ladder, bool entered)
    {
        // Climbing off toggles the controller and re-fires this trigger.
        // Keep tracking the overlap, but do not treat it as walking onto the ledge.
        if (topMountSettleTimer > 0f)
        {
            if (entered)
                topMountLadder = ladder;
            else if (topMountLadder == ladder)
                topMountLadder = null;
            topMountReady = false;
            return;
        }

        if (entered)
        {
            topMountLadder = ladder;
            topMountReady = !topMountFromLanding;
        }
        else if (topMountLadder == ladder)
        {
            topMountLadder = null;
            topMountReady = false;
            topMountFromLanding = false;
        }
    }

    public void SetBottomApproach(Ladder ladder, bool entered)
    {
        if (entered)
            bottomApproach = ladder;
        else if (bottomApproach == ladder)
            bottomApproach = null;
    }

    public void SetCandidateLadder(Ladder ladder, bool entered)
    {
        if (entered)
        {
            candidateLadder = ladder;
        }
        else
        {
            if (candidateLadder == ladder)
                candidateLadder = null;

            // Note: we do NOT exit climb just because the trigger left.
            // Exit happens via top, bottom or jump-off explicitly.
        }
    }

    // =========================
    // START
    // =========================
    void TryStartClimb()
    {
        if (ignoreLadder) return;
        if (motor == null) return;

        bool action = WasClimbActionPressed();
        bool up = motor.GetRawInput().y >= enterUpThreshold;

        // Overlapping the rungs: push up to grab. The action only starts a
        // climb from the bottom, so it does not fight the climb-down at the top.
        if (candidateLadder != null && up)
        {
            EnterClimb(candidateLadder);
            return;
        }

        if (!action)
            return;

        if (bottomApproach != null)
            EnterClimb(bottomApproach);
        else if (candidateLadder != null && NearBottom(candidateLadder))
            EnterClimb(candidateLadder);
    }

    bool NearBottom(Ladder ladder)
    {
        if (ladder == null || ladder.bottomPoint == null)
            return true;
        return transform.position.y <= ladder.bottomPoint.position.y + 1.25f;
    }

    bool WasClimbActionPressed()
    {
        if (WasClimbDownPressed())
            return true;

        // Same button doors and pickups use: E / Enter, Cross on PlayStation.
        return InteractInput.WasPressedThisFrame(null);
    }

    void TryStartClimbDown()
    {
        if (topMountLadder == null) return;
        if (playingClimbEnd) return;
        if (motor == null) return;

        // Finishing a climb leaves the player inside this trigger. That stay
        // does not reverse. Pushing down, or leaving and stepping back on, does.
        bool movingBack = motor.GetRawInput().y <= -enterUpThreshold;
        if (topMountFromLanding && !movingBack) return;
        if (!topMountReady && !movingBack) return;

        topMountReady = false;
        topMountFromLanding = false;
        if (climbEndRoutine != null)
            StopCoroutine(climbEndRoutine);
        climbEndRoutine = StartCoroutine(PlayClimbEnd(topMountLadder, true));
    }

    bool WasClimbDownPressed()
    {
        return climbDownAction != null && climbDownAction.WasPressedThisFrame();
    }

    public void EnterClimb(Ladder ladder)
    {
        if (isClimbing || ignoreLadder) return;
        if (ladder == null) return;

        isClimbing = true;
        currentLadder = ladder;
        IgnoreLadderSolids(ladder, true);

        motor.SetVerticalVelocity(0f);

        combat?.CancelCombatImmediate();
        jump?.ForceExitAirState();

        motor.gravity = 0f;
        motor.SetVerticalVelocity(0f);
        motor.LockMovement(true);

        // Lock visual facing toward the ladder's configured direction so the
        // player sprite always shows the climbing pose regardless of the
        // direction they were facing when they entered.
        motor.LockFacing(ladder.GetClimbFacing2D());

        // Smooth snap to the ladder X/Z over snapDuration.
        if (snapRoutine != null) StopCoroutine(snapRoutine);
        snapRoutine = StartCoroutine(SnapToLadder(ladder));

        // Animator (defensive)
        if (animator != null)
        {
            animator.ResetTrigger("Jump");
            animator.ResetTrigger("Land");
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Roll");

            // Write facing into MoveX/MoveY so any directional blend (Idle/Walk/Climb)
            // shows the correct sprite, then go straight into the climb state.
            ApplyClimbFacing(ladder);
            animator.SetBool("IsMoving", false);

            PlayState(ResolveClimbState(), 0f);

            animator.SetBool(isClimbingHash, true);
            animator.SetFloat(climbSpeedHash, 0f);

            animator.Update(0f);
        }
    }

    IEnumerator SnapToLadder(Ladder ladder)
    {
        Vector3 startPos  = transform.position;
        Vector3 line = ladder.bottomPoint != null ? ladder.bottomPoint.position : ladder.transform.position;
        Vector3 targetXZ  = new Vector3(line.x, startPos.y, line.z);

        if (snapDuration <= 0f)
        {
            PlaceFeet(targetXZ);
            yield break;
        }

        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / snapDuration);

            Vector3 cur = transform.position;
            Vector3 newPos = Vector3.Lerp(startPos, new Vector3(targetXZ.x, cur.y, targetXZ.z), k);
            PlaceFeet(new Vector3(newPos.x, cur.y, newPos.z));

            yield return null;
        }

        snapRoutine = null;
    }

    // =========================
    // EXIT
    // =========================
    public void ExitClimb()
    {
        if (!isClimbing) return;

        Ladder leaving = currentLadder;
        isClimbing = false;
        currentLadder = null;
        IgnoreLadderSolids(leaving, false);

        if (snapRoutine != null) { StopCoroutine(snapRoutine); snapRoutine = null; }

        if (motor != null)
        {
            motor.gravity = originalGravity;
            motor.LockMovement(false);
            motor.UnlockFacing();
        }

        if (animator != null)
        {
            animator.SetBool(isClimbingHash, false);
            animator.SetFloat(climbSpeedHash, 0f);
        }
    }

    // =========================
    // MOVEMENT
    // =========================
    void HandleClimbMovement()
    {
        Vector2 input = motor.GetRawInput();
        float v = input.y;

        // Deadzone snap
        if      (v >  0.15f) v =  1f;
        else if (v < -0.15f) v = -1f;
        else                 v =  0f;

        Vector3 move = Vector3.up * v * climbSpeed;
        CharacterController cc = motor.GetCharacterController();
        if (cc == null || !CanDriveController())
            return;

        if (!cc.enabled)
            cc.enabled = true;

        cc.Move(move * Time.deltaTime);

        if (currentLadder != null)
        {
            // Leaving the top starts in front of the column, and Move can slide off it.
            Vector3 onLine = ClimbPointAtHeight(currentLadder, transform.position.y);
            if ((onLine - transform.position).sqrMagnitude > 0.0001f)
                PlaceFeet(onLine);
        }

        if (animator != null)
            animator.SetFloat(climbSpeedHash, v);

        if (debugClimbSpeed)
            Debug.Log($"[PlayerClimbing] input.y={input.y:F2}  ClimbSpeed={v:F2}");
    }

    void IgnoreLadderSolids(Ladder ladder, bool ignore)
    {
        if (ladder == null || motor == null)
            return;

        CharacterController cc = motor.GetCharacterController();
        if (cc == null)
            return;

        Collider[] cols = ladder.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || cols[i].isTrigger || cols[i] == cc)
                continue;
            Physics.IgnoreCollision(cc, cols[i], ignore);
        }
    }

    void CheckTopExit()
    {
        if (currentLadder == null || currentLadder.topPoint == null) return;

        if (transform.position.y >= currentLadder.topPoint.position.y - topExitYThreshold)
            ExitAtTop();
    }

    void CheckBottomExit()
    {
        // If grounded and below the bottom point → user reached the floor, leave the ladder.
        if (currentLadder == null) return;

        bool grounded = motor.IsGrounded();
        if (!grounded) return;

        // If a bottomPoint was set, require the player to be near it.
        if (currentLadder.bottomPoint != null)
        {
            if (transform.position.y > currentLadder.bottomPoint.position.y + bottomExitYThreshold)
                return; // still up the ladder
        }

        // Only auto-exit at bottom if the player is pulling down or not pressing up.
        Vector2 input = motor.GetRawInput();
        if (input.y > 0.1f) return; // still wants to go up

        Vector2 stepIdle = -currentLadder.GetClimbFacing2D();
        ExitClimb();
        ApplyIgnoreCooldown();
        HoldDismountIdle(stepIdle);

        int idle = Animator.StringToHash("Idle");
        if (animator != null && HasState(idle))
        {
            animator.Play(idle, 0, 0f);
            animator.Update(0f);
        }
    }

    void HoldDismountIdle(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.down;

        dismountIdleDir = dir;
        dismountIdleTimer = 0.08f;
        if (motor != null)
        {
            motor.SetFacing(dir);
            motor.LockMovement(true);
        }
        ApplyDismountIdle();
    }

    void ApplyDismountIdle()
    {
        if (animator == null)
            return;

        animator.SetBool("IsMoving", false);
        animator.SetFloat("MoveX", dismountIdleDir.x);
        animator.SetFloat("MoveY", dismountIdleDir.y);
    }
    void CheckJumpOff()
    {
        bool pressed = WasJumpOffPressed();
        if (!pressed) return;

        ExitClimb();
        ApplyIgnoreCooldown();

        if (motor != null && jumpOffForce > 0f)
            motor.SetVerticalVelocity(jumpOffForce);
    }

    bool WasJumpOffPressed()
    {
        if (jumpOffAction != null && jumpOffAction.action != null)
            return jumpOffAction.action.WasPressedThisFrame();

        // Fallback: reuse the generic Interact action (Space / E / X gamepad).
        return InteractInput.WasPressedThisFrame(null);
    }

    // =========================
    // TOP EXIT WITH EJECTION
    // =========================
    void ExitAtTop()
    {
        if (playingClimbEnd || currentLadder == null)
            return;

        if (climbEndRoutine != null)
            StopCoroutine(climbEndRoutine);
        climbEndRoutine = StartCoroutine(PlayClimbEnd(currentLadder, false));
    }

    IEnumerator PlayClimbEnd(Ladder ladder, bool reverse)
    {
        playingClimbEnd = true;
        IgnoreLadderSolids(ladder, true);

        if (!isClimbing)
        {
            isClimbing = true;
            currentLadder = ladder;
            motor.gravity = 0f;
            motor.SetVerticalVelocity(0f);
            motor.LockMovement(true);
            combat?.CancelCombatImmediate();
            jump?.ForceExitAirState();
        }

        ApplyClimbFacing(ladder);

        Vector3 from = transform.position;
        Vector3 to = reverse ? FeetOnLadder(ladder) : FeetOnPlatform(ladder);

        if (reverse)
            PlaceFeet(FeetOnPlatform(ladder));

        from = transform.position;

        if (animator != null)
        {
            animator.SetFloat(climbEndSpeedHash, reverse ? -1f : 1f);
            animator.SetBool(isClimbingHash, true);
            PlayState(ResolveEndState(), reverse ? 1f : 0f);
            animator.Update(0f);
        }

        float duration = climbEndDuration;
        if (animator != null)
        {
            float clip = animator.GetCurrentAnimatorStateInfo(0).length;
            if (clip > 0.05f)
                duration = clip;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            PlaceFeet(Vector3.Lerp(from, to, Mathf.Clamp01(t / duration)));
            motor.SetVerticalVelocity(0f);
            yield return null;
        }

        PlaceFeet(to);

        if (reverse)
        {
            yield return EaseOntoRungs(ladder, to);
            playingClimbEnd = false;
            climbEndRoutine = null;
            yield break;
        }

        playingClimbEnd = false;
        climbEndRoutine = null;

        if (ladder.ladderTrigger != null)
            ladder.ladderTrigger.enabled = false;

        ExitClimb();
        PlaceFeet(to);
        ApplyIgnoreCooldown();
        topMountFromLanding = true;
        topMountSettleTimer = ignoreDuration;
        StartCoroutine(ReenableLadder(ladder));
    }

    IEnumerator EaseOntoRungs(Ladder ladder, Vector3 from)
    {
        Vector3 onRungs = from;
        if (ladder.topPoint != null)
            onRungs.y = ladder.topPoint.position.y - topExitYThreshold - 0.08f;
        onRungs = ClimbPointAtHeight(ladder, onRungs.y);

        currentLadder = ladder;
        isClimbing = true;
        ApplyClimbFacing(ladder);

        const float blend = 0.16f;
        if (animator != null)
        {
            int climb = ResolveClimbState();
            if (climb != 0)
                animator.CrossFade(climb, blend, 0, 0f);
            animator.SetBool(isClimbingHash, true);
            animator.SetFloat(climbSpeedHash, 0f);
        }

        float u = 0f;
        while (u < blend)
        {
            u += Time.deltaTime;
            PlaceFeet(Vector3.Lerp(from, onRungs, Mathf.Clamp01(u / blend)));
            if (motor != null)
                motor.SetVerticalVelocity(0f);
            yield return null;
        }

        PlaceFeet(onRungs);
    }

    Vector3 FeetOnPlatform(Ladder ladder)
    {
        return ladder.GetTopDismount();
    }

    Vector3 FeetOnLadder(Ladder ladder)
    {
        if (ladder.topPoint != null)
            return ClimbPointAtHeight(ladder, ladder.topPoint.position.y);
        return transform.position;
    }

    Vector3 ClimbPointAtHeight(Ladder ladder, float y)
    {
        Vector3 bottom = ladder.bottomPoint != null
            ? ladder.bottomPoint.position
            : ladder.transform.position;
        Vector3 top = ladder.topPoint != null
            ? ladder.topPoint.position
            : bottom;

        float span = top.y - bottom.y;
        float k = Mathf.Abs(span) > 0.01f ? Mathf.InverseLerp(bottom.y, top.y, y) : 0f;
        Vector3 point = Vector3.Lerp(bottom, top, Mathf.Clamp01(k));
        point.y = y;
        return point;
    }

    void ApplyClimbFacing(Ladder ladder)
    {
        if (ladder == null)
            return;

        Vector2 face = ladder.GetClimbFacing2D();
        motor.LockFacing(face);
        if (animator == null)
            return;

        animator.SetFloat("MoveX", face.x);
        animator.SetFloat("MoveY", face.y);
    }

    void PlaceFeet(Vector3 feet)
    {
        CharacterController cc = motor != null ? motor.GetCharacterController() : null;
        if (cc == null)
        {
            transform.position = feet;
            return;
        }

        if (cc.enabled)
            cc.enabled = false;
        transform.position = feet;
        if (CanDriveController())
            cc.enabled = true;
    }

    bool CanDriveController()
    {
        var control = GetComponent<PartyMemberControl>();
        return control == null || control.isControlled;
    }

    void PlayState(int hash, float normalizedTime)
    {
        if (animator != null && hash != 0 && HasState(hash))
            animator.Play(hash, 0, normalizedTime);
    }

    int ResolveClimbState()
    {
        if (HasState(climbStateHash))
            return climbStateHash;
        int fallback = Animator.StringToHash("Climb-Up");
        return HasState(fallback) ? fallback : 0;
    }

    int ResolveEndState()
    {
        if (HasState(climbEndStateHash))
            return climbEndStateHash;
        int fallback = Animator.StringToHash("ClimbEnd");
        return HasState(fallback) ? fallback : 0;
    }

    IEnumerator ReenableLadder(Ladder ladderRef)
    {
        yield return new WaitForSeconds(0.5f);

        if (ladderRef != null && ladderRef.ladderTrigger != null)
            ladderRef.ladderTrigger.enabled = true;
    }

    void ApplyIgnoreCooldown()
    {
        ignoreLadder = true;
        ignoreTimer = ignoreDuration;
        topMountReady = false;
    }

    bool HasState(int stateHash)
    {
        if (animator == null) return false;
        return animator.HasState(0, stateHash);
    }
}
