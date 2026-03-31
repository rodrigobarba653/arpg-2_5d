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
    Vector3 knockbackVelocity;
    float knockbackTimer;

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
        HandleKnockback();
        Move();
    }

    void Move()
    {
        if (!controller)
            return;

        // gravedad siempre
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;

        // si es fijo → no caminar
        if (moveType == EnemyMoveType.Fixed)
        {
            Vector3 gravityMove = verticalVelocity;
            controller.Move(gravityMove * Time.deltaTime);
            return;
        }

        Vector3 finalMove = moveDirection * moveSpeed;
        finalMove.y = verticalVelocity.y;

        controller.Move(finalMove * Time.deltaTime);

        Rotate();
    }

    void Rotate()
    {
        if (moveDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRot =
            Quaternion.LookRotation(moveDirection);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
    }

    // ===== API =====

    public void SetMoveDirection(Vector3 dir)
    {
        if (moveType == EnemyMoveType.Fixed)
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
        return controller.isGrounded;
    }

    void HandleKnockback()
    {
        if (knockbackTimer > 0f)
        {
            controller.Move(knockbackVelocity * Time.deltaTime);

            knockbackTimer -= Time.deltaTime;

            if (knockbackTimer <= 0f)
            {
                knockbackVelocity = Vector3.zero;
            }
        }
    }

    public void DoKnockback(Vector3 dir, float force, float time)
    {
        dir.y = 0f;

        knockbackVelocity = dir.normalized * force;
        knockbackTimer = time;
    }
}