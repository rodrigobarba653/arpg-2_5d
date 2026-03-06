using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float deadZone = 0.15f;

    [Header("Input Snapping (helps diagonals)")]
    public bool snapTo8Directions = true;
    public bool preserveAnalogMagnitude = false;

    [Header("Grounding")]
    public float groundRayLength = 1.2f;
    public float stickToGroundForce = 5f;

    private Rigidbody rb;
    private Camera mainCam;

    private Vector2 moveInput;

    // Facing used by animations and action directions (roll/lunge fallbacks)
    private Vector2 lastNonZeroFacing = Vector2.down;

    // Facing lock (roll/attack)
    private bool facingLocked = false;
    private Vector2 lockedFacing = Vector2.down;

    // Roll state
    private bool rollActive = false;
    private float rollElapsed = 0f;
    private float currentRollDuration = 0.25f;
    private float currentRollSpeed = 8f;
    private Vector3 rollWorldDir = Vector3.zero;

    // Lunge state
    private bool lungeActive = false;
    private float lungeElapsed = 0f;
    private Vector3 lungeWorldDir = Vector3.zero;

    private float currentLungeSpeed;
    private float currentLungeDuration;
    private float currentPreStopTime;
    private float currentPostStopTime;

    // Lock normal movement while attacking / rolling
    private bool movementLocked = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 raw = ctx.ReadValue<Vector2>();

        float mag = raw.magnitude;
        if (mag < deadZone)
        {
            moveInput = Vector2.zero;
            return;
        }

        Vector2 dir = raw.normalized;

        if (snapTo8Directions)
            dir = SnapTo8(dir);

        if (preserveAnalogMagnitude)
        {
            float remapped = Mathf.InverseLerp(deadZone, 1f, mag);
            moveInput = dir * remapped;
        }
        else
        {
            moveInput = dir;
        }

        // Only update facing when not locked
        if (!facingLocked && dir.sqrMagnitude > 0.0001f)
            lastNonZeroFacing = dir;
    }

    void FixedUpdate()
    {
        // Roll overrides everything
        if (rollActive)
        {
            ApplyRollVelocity();
            return;
        }

        // Lunge overrides normal movement
        if (lungeActive)
        {
            ApplyLungeVelocity();
            return;
        }

        // If movement locked (attacking), hold still
        if (movementLocked)
        {
            HoldStill();
            return;
        }

        // Normal movement
        Vector3 move = GetMoveWorld(moveInput);

        if (move.sqrMagnitude > 1f) move.Normalize();
        move *= moveSpeed;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundRayLength))
            move = Vector3.ProjectOnPlane(move, hit.normal);

        Vector3 vel = rb.linearVelocity;
        vel.x = move.x;
        vel.z = move.z;

        if (Physics.Raycast(transform.position, Vector3.down, groundRayLength))
            vel.y = -stickToGroundForce;

        rb.linearVelocity = vel;
    }

    // ===== Public API =====

    public Vector2 GetFacing2D() => facingLocked ? lockedFacing : lastNonZeroFacing;

    public Vector2 GetMoveInput2D() => moveInput;

    public void LockMovement(bool locked)
    {
        movementLocked = locked;
        if (locked) HoldStill();
    }

    public void LockFacing(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = lastNonZeroFacing;
        dir.Normalize();
        lockedFacing = dir;
        facingLocked = true;
    }

    public void UnlockFacing()
    {
        facingLocked = false;

        // If the player is already holding a direction, immediately update facing
        // so the run animation matches movement after roll/attack.
        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Vector2 dir = moveInput.normalized;

            // Safety: keep snap behavior consistent even if OnMove changes later
            if (snapTo8Directions)
                dir = SnapTo8(dir);

            lastNonZeroFacing = dir;
        }
    }

    // =======================
    // ROLL API
    // =======================

    public void BeginRoll(Vector2 rollDir2D, float speed, float duration)
    {
        movementLocked = true;

        currentRollSpeed = speed;
        currentRollDuration = Mathf.Max(0.01f, duration);

        LockFacing(rollDir2D);

        Vector3 world = GetMoveWorld(rollDir2D);
        world.y = 0f;

        if (world.sqrMagnitude < 0.0001f)
            world = GetMoveWorld(lastNonZeroFacing);

        world.y = 0f;
        world.Normalize();
        rollWorldDir = world;

        rollElapsed = 0f;
        rollActive = true;

        HoldStill();
    }

    public void EndRoll()
    {
        rollActive = false;
        rollElapsed = 0f;
        HoldStill();
        // UnlockFacing() is handled by CombatController when roll ends
    }

    private void ApplyRollVelocity()
    {
        rollElapsed += Time.fixedDeltaTime;

        Vector3 vel = rb.linearVelocity;
        vel.x = rollWorldDir.x * currentRollSpeed;
        vel.z = rollWorldDir.z * currentRollSpeed;

        if (Physics.Raycast(transform.position, Vector3.down, groundRayLength))
            vel.y = -stickToGroundForce;

        rb.linearVelocity = vel;

        if (rollElapsed >= currentRollDuration)
        {
            rollActive = false;
            HoldStill();
        }
    }

    // =======================
    // LUNGE API
    // =======================

    public void BeginAttackLunge(Vector2 attackDir2D, float speed, float duration, float preStop, float postStop)
    {
        movementLocked = true;

        LockFacing(attackDir2D);

        Vector3 world = GetMoveWorld(attackDir2D);
        world.y = 0f;

        if (world.sqrMagnitude < 0.0001f)
            world = GetMoveWorld(lastNonZeroFacing);

        world.y = 0f;
        world.Normalize();
        lungeWorldDir = world;

        currentLungeSpeed = speed;
        currentLungeDuration = duration;
        currentPreStopTime = preStop;
        currentPostStopTime = postStop;

        lungeElapsed = 0f;
        lungeActive = true;

        HoldStill();
    }

    private void ApplyLungeVelocity()
    {
        lungeElapsed += Time.fixedDeltaTime;

        float total = currentPreStopTime + currentLungeDuration + currentPostStopTime;

        if (lungeElapsed < currentPreStopTime)
        {
            HoldStill();
        }
        else if (lungeElapsed < currentPreStopTime + currentLungeDuration)
        {
            Vector3 vel = rb.linearVelocity;
            vel.x = lungeWorldDir.x * currentLungeSpeed;
            vel.z = lungeWorldDir.z * currentLungeSpeed;

            if (Physics.Raycast(transform.position, Vector3.down, groundRayLength))
                vel.y = -stickToGroundForce;

            rb.linearVelocity = vel;
        }
        else
        {
            HoldStill();
        }

        if (lungeElapsed >= total)
        {
            lungeActive = false;
            HoldStill();
        }
    }

    private void HoldStill()
    {
        Vector3 vel = rb.linearVelocity;
        vel.x = 0f;
        vel.z = 0f;

        if (Physics.Raycast(transform.position, Vector3.down, groundRayLength))
            vel.y = -stickToGroundForce;

        rb.linearVelocity = vel;
        rb.angularVelocity = Vector3.zero;
    }

    private Vector3 GetMoveWorld(Vector2 input)
    {
        if (mainCam == null) mainCam = Camera.main;

        Vector3 camForward = mainCam.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = mainCam.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        return (camForward * input.y + camRight * input.x);
    }

    private static Vector2 SnapTo8(Vector2 v)
    {
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        float rad = snapped * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}