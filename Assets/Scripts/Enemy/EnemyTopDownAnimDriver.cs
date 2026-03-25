using UnityEngine;

public class EnemyTopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;

    [Header("Tuning")]
    public float moveDeadzone = 0.01f;

    float moveDeadzoneSqr;

    Vector3 lastMoveDir = Vector3.forward;

    void Awake()
    {
        if (!animator)
            animator = GetComponent<Animator>();

        if (!motor)
            motor = GetComponentInParent<EnemyMotor>();

        moveDeadzoneSqr = moveDeadzone * moveDeadzone;
    }

    void Update()
    {
        if (!animator || !motor)
            return;

        float speed = motor.GetSpeed();

        bool isMoving = speed > moveDeadzone;

        Vector3 dir = GetMoveDirection();

        if (dir.sqrMagnitude > 0.0001f)
            lastMoveDir = dir;

        Vector3 finalDir =
            isMoving ? dir : lastMoveDir;

        Vector2 dir2D =
            new Vector2(finalDir.x, finalDir.z).normalized;

        animator.SetBool("IsMoving", isMoving);
        animator.SetFloat("MoveX", dir2D.x);
        animator.SetFloat("MoveY", dir2D.y);
    }

    Vector3 GetMoveDirection()
    {
        Vector3 v = motor.controller.velocity;
        v.y = 0f;

        if (v.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return v.normalized;
    }
}