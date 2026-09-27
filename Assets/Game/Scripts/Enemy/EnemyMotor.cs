using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class EnemyMotor : MonoBehaviour
{
    public enum EnemyMoveType
    {
        Mobile,
        Fixed
    }

    [Header("Type")]
    public EnemyMoveType moveType = EnemyMoveType.Mobile;

    [Header("References")]
    public CharacterController controller;
    public Transform target;
    public NavMeshAgent agent;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float gravity = -20f;
    public float rotationSpeed = 10f;

    [HideInInspector]
    public float activeSpeedOverride = -1f;

    public float GetActiveSpeed()
    {
        return activeSpeedOverride > 0f ? activeSpeedOverride : moveSpeed;
    }

    [Header("Knockback")]
    [SerializeField] private bool faceHitSourceDuringKnockback = true;
    [SerializeField] private float postKnockbackRecoveryTime = 0.2f;

    [Header("Hit Stun")]
    [SerializeField] private float hitStunTimer;

    float movementLockTimer;

    Vector3 knockbackVelocity;
    float knockbackTimer;
    Vector3 knockbackFaceDirection;

    float hitFacingLockTimer;
    Vector3 hitFacingLockDirection;

    Vector3 verticalVelocity;
    Vector3 moveDirection;
    Vector3 aimDirection;
    bool faceMoveSeparately;

    /// <summary>
    /// Set for the current guard only. Blocks rotation from movement, hit-facing,
    /// and knockback. EnemyAI.HoldDefendFacing is what writes the yaw.
    /// Do not also force this rotation in LateUpdate: the sprite billboard runs late,
    /// and a late root turn pulls that sprite off the camera.
    /// </summary>
    public bool lockFacing;

    public bool AnimFacesAim => !lockFacing && faceMoveSeparately && aimDirection.sqrMagnitude > 0.0001f;
    public Vector3 AnimAimDirection => aimDirection;

    void Awake()
    {
        if (!controller)
            controller = GetComponent<CharacterController>();

        if (!agent)
            agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
        }
    }

    void Update()
    {
        Move();
    }

    void LateUpdate()
    {
        if (agent != null && agent.enabled)
        {
            agent.speed = GetActiveSpeed();

            if (agent.isOnNavMesh)
                agent.nextPosition = transform.position;
        }

        // A blocked hit still starts a face-the-attacker snap. While the guard
        // is up, drop that snap here so it cannot yaw the root after the billboard.
        if (lockFacing)
            hitFacingLockTimer = 0f;
        else if (hitFacingLockTimer > 0f && faceHitSourceDuringKnockback)
        {
            FaceDirectionInstant(hitFacingLockDirection);
            hitFacingLockTimer -= Time.deltaTime;
        }

        activeSpeedOverride = -1f;
    }

    void Move()
    {
        if (!controller)
            return;

        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;

        if (moveType == EnemyMoveType.Fixed)
        {
            if (hitStunTimer > 0f)
                hitStunTimer -= Time.deltaTime;

            Vector3 gravityOnly = new Vector3(0f, verticalVelocity.y, 0f);
            controller.Move(gravityOnly * Time.deltaTime);
            return;
        }

        // Knockback first. Hit stun must not block it.
        if (knockbackTimer > 0f)
        {
            ApplyKnockbackAndGravityOnly();

            if (!lockFacing && faceHitSourceDuringKnockback)
                FaceDirectionInstant(knockbackFaceDirection);

            knockbackTimer -= Time.deltaTime;

            if (knockbackTimer <= 0f)
            {
                knockbackTimer = 0f;
                knockbackVelocity = Vector3.zero;
                movementLockTimer = Mathf.Max(movementLockTimer, postKnockbackRecoveryTime);
            }

            return;
        }

        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.deltaTime;

            Vector3 gravityOnly = new Vector3(0f, verticalVelocity.y, 0f);
            controller.Move(gravityOnly * Time.deltaTime);
            return;
        }

        if (movementLockTimer > 0f)
        {
            movementLockTimer -= Time.deltaTime;

            if (!lockFacing && faceHitSourceDuringKnockback)
                FaceDirectionInstant(knockbackFaceDirection);

            Vector3 gravityOnly = new Vector3(0f, verticalVelocity.y, 0f);
            controller.Move(gravityOnly * Time.deltaTime);
            return;
        }

        Vector3 finalMove = moveDirection * GetActiveSpeed();
        finalMove.y = verticalVelocity.y;

        controller.Move(finalMove * Time.deltaTime);

        Rotate();
    }

    void ApplyKnockbackAndGravityOnly()
    {
        Vector3 finalMove = knockbackVelocity;
        finalMove.y = verticalVelocity.y;

        controller.Move(finalMove * Time.deltaTime);
    }

    void Rotate()
    {
        if (lockFacing || hitFacingLockTimer > 0f)
            return;

        if (moveDirection.sqrMagnitude < 0.001f && aimDirection.sqrMagnitude < 0.001f)
            return;

        RotateToward(aimDirection.sqrMagnitude > 0.001f ? aimDirection : moveDirection);
    }

    public void RotateToward(Vector3 dir)
    {
        if (lockFacing || hitFacingLockTimer > 0f)
            return;

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    void FaceDirectionInstant(Vector3 dir)
    {
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public void SetMoveDirection(Vector3 dir)
    {
        ApplyMove(dir, dir);
    }

    /// <summary>Move one way while looking another. Used when holding turns into a strafe.</summary>
    public void SetMoveAndFacing(Vector3 moveDir, Vector3 faceDir)
    {
        ApplyMove(moveDir, faceDir);
    }

    void ApplyMove(Vector3 dir, Vector3 faceDir)
    {
        if (moveType == EnemyMoveType.Fixed)
            return;

        if (knockbackTimer > 0f)
            return;

        if (hitStunTimer > 0f)
            return;

        if (movementLockTimer > 0f)
            return;

        dir.y = 0f;
        faceDir.y = 0f;
        moveDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
        aimDirection = faceDir.sqrMagnitude > 0.0001f ? faceDir.normalized : moveDirection;
        faceMoveSeparately = moveDirection.sqrMagnitude > 0.0001f
            && aimDirection.sqrMagnitude > 0.0001f
            && Vector3.Angle(moveDirection, aimDirection) > 8f;
    }

    public void Stop()
    {
        moveDirection = Vector3.zero;
        aimDirection = Vector3.zero;
        faceMoveSeparately = false;
    }

    public float GetSpeed()
    {
        if (!controller)
            return 0f;

        Vector3 v = controller.velocity;
        v.y = 0f;
        return v.magnitude;
    }

    public bool IsGrounded()
    {
        return controller != null && controller.isGrounded;
    }

    public void ForceFacing(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public void FaceDirection(Vector3 dir)
    {
        if (lockFacing || hitFacingLockTimer > 0f)
            return;

        FaceDirectionInstant(dir);
    }

    public void ApplyHitStun(float time)
    {
        if (knockbackTimer > 0f)
            return;

        hitStunTimer = time;
        moveDirection = Vector3.zero;
    }

    public void DoKnockback(Vector3 dir, float force, float time)
    {
        if (moveType == EnemyMoveType.Fixed)
            return;

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Vector3 knockbackDir = dir.normalized;

        knockbackVelocity = knockbackDir * force;
        knockbackTimer = time;

        // Move away from hit, but face hit source.
        knockbackFaceDirection = -knockbackDir;

        if (lockFacing)
            return;

        // Lock facing until knockback + recovery finishes.
        hitFacingLockDirection = knockbackFaceDirection;
        hitFacingLockTimer = time + postKnockbackRecoveryTime + 0.05f;

        if (faceHitSourceDuringKnockback)
            FaceDirectionInstant(hitFacingLockDirection);

        hitStunTimer = 0f;
        moveDirection = Vector3.zero;
    }

    public void AddMovementLock(float time)
    {
        movementLockTimer = Mathf.Max(movementLockTimer, time);
        moveDirection = Vector3.zero;
    }

    public bool IsInKnockback()
    {
        return knockbackTimer > 0f;
    }

    public bool IsInHitStun()
    {
        return hitStunTimer > 0f;
    }

    public bool IsMovementLocked()
    {
        return movementLockTimer > 0f || knockbackTimer > 0f || hitStunTimer > 0f;
    }
}