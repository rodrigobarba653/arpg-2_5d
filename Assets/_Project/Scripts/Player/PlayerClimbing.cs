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
    public float climbSpeed = 2f;

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
    public string climbStateName = "ClimbingUp";
    public string isClimbingBool = "isClimbing";
    public string climbSpeedFloat = "ClimbSpeed";

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
    int isClimbingHash;
    int climbSpeedHash;

    InputAction defaultJumpOff;

    public bool IsClimbing() => isClimbing;

    void Awake()
    {
        if (!motor)    motor    = GetComponent<PlayerMotor>();
        if (!jump)     jump     = GetComponent<PlayerJump>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        combat = GetComponent<PlayerCombatController>();

        if (motor != null)
            originalGravity = motor.gravity;

        climbStateHash  = Animator.StringToHash(climbStateName);
        isClimbingHash  = Animator.StringToHash(isClimbingBool);
        climbSpeedHash  = Animator.StringToHash(climbSpeedFloat);
    }

    void OnEnable()
    {
        if (jumpOffAction != null && jumpOffAction.action != null)
            jumpOffAction.action.Enable();
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

        if (!isClimbing)
        {
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
        if (candidateLadder == null) return;
        if (ignoreLadder) return;
        if (motor == null) return;

        Vector2 input = motor.GetRawInput();
        if (input.y < enterUpThreshold) return;

        EnterClimb(candidateLadder);
    }

    public void EnterClimb(Ladder ladder)
    {
        if (isClimbing || ignoreLadder) return;
        if (ladder == null) return;

        isClimbing = true;
        currentLadder = ladder;

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
            Vector2 face = ladder.GetClimbFacing2D();
            animator.SetFloat("MoveX", face.x);
            animator.SetFloat("MoveY", face.y);
            animator.SetBool("IsMoving", false);

            if (HasState(climbStateHash))
                animator.Play(climbStateHash, 0, 0f);

            animator.SetBool(isClimbingHash, true);
            animator.SetFloat(climbSpeedHash, 0f);

            animator.Update(0f);
        }
    }

    IEnumerator SnapToLadder(Ladder ladder)
    {
        Vector3 startPos  = transform.position;
        Vector3 targetXZ  = new Vector3(ladder.transform.position.x, startPos.y, ladder.transform.position.z);

        if (snapDuration <= 0f)
        {
            transform.position = targetXZ;
            yield break;
        }

        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / snapDuration);

            Vector3 cur = transform.position;
            Vector3 newPos = Vector3.Lerp(startPos, new Vector3(targetXZ.x, cur.y, targetXZ.z), k);
            transform.position = new Vector3(newPos.x, cur.y, newPos.z);

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

        isClimbing = false;
        currentLadder = null;

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
        motor.GetCharacterController().Move(move * Time.deltaTime);

        if (animator != null)
            animator.SetFloat(climbSpeedHash, v);

        if (debugClimbSpeed)
            Debug.Log($"[PlayerClimbing] input.y={input.y:F2}  ClimbSpeed={v:F2}");
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

        ExitClimb();
        ApplyIgnoreCooldown();
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
        Ladder ladderRef = currentLadder;

        if (ladderRef != null && ladderRef.ladderTrigger != null)
            ladderRef.ladderTrigger.enabled = false;

        ApplyIgnoreCooldown();
        ExitClimb();

        if (ladderRef != null)
        {
            CharacterController cc = motor.GetCharacterController();

            // 1) small vertical lift to clear the ledge edge
            if (ladderRef.topExitLift > 0f)
                cc.Move(Vector3.up * ladderRef.topExitLift);

            // 2) push along the ladder's exit direction (NOT player.forward)
            Vector3 exitDir = ladderRef.GetTopExitDir();
            if (ladderRef.topExitForward > 0f)
                cc.Move(exitDir * ladderRef.topExitForward);

            StartCoroutine(ReenableLadder(ladderRef));
        }
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
    }

    bool HasState(int stateHash)
    {
        if (animator == null) return false;
        return animator.HasState(0, stateHash);
    }
}
