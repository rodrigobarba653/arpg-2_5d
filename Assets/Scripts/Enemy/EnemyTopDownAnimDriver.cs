using UnityEngine;

public class EnemyTopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;
    public EnemyAI ai;

    [Header("Tuning")]
    public float moveDeadzone = 0.01f;

    float moveDeadzoneSqr;

    Vector3 lastMoveDir = Vector3.forward;

    static readonly int IsMovingHash   = Animator.StringToHash("IsMoving");
    static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");
    static readonly int MoveXHash      = Animator.StringToHash("MoveX");
    static readonly int MoveYHash      = Animator.StringToHash("MoveY");

    void Awake()
    {
        if (!animator)
            animator = GetComponent<Animator>();

        if (!motor)
            motor = GetComponentInParent<EnemyMotor>();

        if (!ai)
            ai = GetComponentInParent<EnemyAI>();

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

        // When moving, use velocity for instant directional response.
        // When idle, read transform.forward from the motor — for Mobile this
        // is the last facing the motor rotated to; for Fixed enemies this
        // tracks the player in real time (rotated by EnemyAI/motor.RotateToward).
        Vector3 finalDir;

        if (isMoving)
        {
            finalDir = dir;
        }
        else
        {
            Vector3 fwd = motor.transform.forward;
            fwd.y = 0f;

            if (fwd.sqrMagnitude > 0.0001f)
                finalDir = fwd.normalized;
            else
                finalDir = lastMoveDir;
        }

        Vector2 dir2D =
            new Vector2(finalDir.x, finalDir.z).normalized;

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsInCombatHash, ai != null && ai.isInCombat);
        animator.SetFloat(MoveXHash, dir2D.x);
        animator.SetFloat(MoveYHash, dir2D.y);
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