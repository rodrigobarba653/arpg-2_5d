using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerJump jump;
    PlayerClimbing climbing;

    [Header("Tuning")]
    [SerializeField] private float moveDeadzone = 0.01f;

    bool wasGrounded;
    private float moveDeadzoneSqr;

    // dirección guardada cuando saltas
    private Vector2 lockedAirDirection;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!motor) motor = GetComponentInParent<PlayerMotor>();
        if (!jump) jump = GetComponentInParent<PlayerJump>();
        if (!climbing) climbing = GetComponentInParent<PlayerClimbing>();

        moveDeadzoneSqr = moveDeadzone * moveDeadzone;
    }

    void Update()
    {
        if (!animator || !motor)
            return;

        // While climbing, PlayerClimbing owns the animator (IsClimbing + ClimbSpeed).
        // Don't touch IsMoving / MoveX / MoveY here, or Any State transitions tied
        // to those parameters can yank the state out of ClimbIdle.
        if (climbing != null && climbing.IsClimbing())
            return;

        bool rolling = motor.rollActive;

        Vector2 inputDir = motor.GetMoveInput2D();

        bool hasInput = inputDir.sqrMagnitude > moveDeadzoneSqr;

        bool grounded = motor.IsGrounded();

        if (!grounded && wasGrounded)
        {
            lockedAirDirection = motor.GetFacing2D();
        }

        wasGrounded = grounded;

        Vector2 dir;

        if (!grounded && !rolling)
        {
            if (hasInput)
                lockedAirDirection = inputDir;

            dir = lockedAirDirection;
        }
        else if (!rolling)
        {
            if (hasInput)
            {
                dir = inputDir;
                lockedAirDirection = Vector2.zero;
            }
            else
            {
                dir = motor.GetFacing2D();
            }
        }
        else
        {
            // durante roll no cambiar dirección
            dir = motor.GetFacing2D();
        }

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.down;

        dir.Normalize();

        animator.SetBool("IsMoving", hasInput && !rolling);
        animator.SetFloat("MoveX", dir.x);
        animator.SetFloat("MoveY", dir.y);
    }
}