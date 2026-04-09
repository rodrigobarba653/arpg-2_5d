using UnityEngine;

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

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float gravity = -20f;
    public float rotationSpeed = 10f;

    [Header("Knockback")]
    [SerializeField] private bool faceHitSourceDuringKnockback = true;

    [Tooltip("Extra pause after knockback ends before the enemy can move again")]
    [SerializeField] private float postKnockbackRecoveryTime = 0.2f;

    [Header("Hit Stun")]
    [SerializeField] private float hitStunTimer;

    float movementLockTimer;

    Vector3 knockbackVelocity;
    float knockbackTimer;
    Vector3 knockbackFaceDirection;

    Vector3 verticalVelocity;
    Vector3 moveDirection;

    void Awake()
    {
        if (!controller)
            controller = GetComponent<CharacterController>();

        var player = GameObject.FindWithTag("Player");

        if (player)
        {
            var pc = player.GetComponent<CharacterController>();

            if (pc)
                Physics.IgnoreCollision(controller, pc, true);
        }
    }

    void Update()
    {
        Move();
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
            {
                hitStunTimer -= Time.deltaTime;

                Vector3 gravityOnly = new Vector3(0f, verticalVelocity.y, 0f);
                controller.Move(gravityOnly * Time.deltaTime);
                return;
            }

            ApplyKnockbackAndGravityOnly();
            return;
        }

        // hit stun = full stop
        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.deltaTime;

            Vector3 gravityOnly = new Vector3(0f, verticalVelocity.y, 0f);
            controller.Move(gravityOnly * Time.deltaTime);
            return;
        }

        // knockback = no normal movement
        if (knockbackTimer > 0f)
        {
            ApplyKnockbackAndGravityOnly();

            if (faceHitSourceDuringKnockback)
                RotateToward(knockbackFaceDirection);

            knockbackTimer -= Time.deltaTime;

            if (knockbackTimer <= 0f)
            {
                knockbackTimer = 0f;
                knockbackVelocity = Vector3.zero;
                movementLockTimer = Mathf.Max(movementLockTimer, postKnockbackRecoveryTime);
            }

            return;
        }

        // small pause after knockback
        if (movementLockTimer > 0f)
        {
            movementLockTimer -= Time.deltaTime;

            Vector3 gravityOnly = new Vector3(0f, verticalVelocity.y, 0f);
            controller.Move(gravityOnly * Time.deltaTime);
            return;
        }

        Vector3 finalMove = moveDirection * moveSpeed;
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
        if (moveDirection.sqrMagnitude < 0.001f)
            return;

        RotateToward(moveDirection);
    }

    void RotateToward(Vector3 dir)
    {
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

    public void SetMoveDirection(Vector3 dir)
    {
        if (moveType == EnemyMoveType.Fixed)
            return;

        if (hitStunTimer > 0f)
            return;

        if (knockbackTimer > 0f)
            return;

        if (movementLockTimer > 0f)
            return;

        dir.y = 0f;
        moveDirection = dir.normalized;
    }

    public void Stop()
    {
        moveDirection = Vector3.zero;
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

    public void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public void ApplyHitStun(float time)
    {
        hitStunTimer = time;
        moveDirection = Vector3.zero;
    }

    public void DoKnockback(Vector3 dir, float force, float time)
    {
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        knockbackVelocity = dir.normalized * force;
        knockbackTimer = time;
        knockbackFaceDirection = -dir.normalized;
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