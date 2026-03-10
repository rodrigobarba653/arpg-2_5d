using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerJump jump;

    [Header("Tuning")]
    [SerializeField] private float moveDeadzone = 0.01f;

    private Vector2 moveInput;
    private float moveDeadzoneSqr;

    // dirección guardada cuando saltas
    private Vector2 lockedAirDirection;

    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = ctx.ReadValue<Vector2>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!motor) motor = GetComponentInParent<PlayerMotor>();
        if (!jump) jump = GetComponentInParent<PlayerJump>();

        moveDeadzoneSqr = moveDeadzone * moveDeadzone;
    }

    void Update()
    {
        if (!animator || !motor) return;

        Rigidbody rb = motor.GetComponent<Rigidbody>();

        Vector3 velocity = rb.linearVelocity;
        Vector2 planarVelocity = new Vector2(velocity.x, velocity.z);

        bool isMoving = planarVelocity.sqrMagnitude > moveDeadzoneSqr;

        Vector2 dir;

        // Si estamos en el aire
        if (jump != null && !jump.IsGrounded)
        {
            // la primera vez que dejamos el suelo guardamos la dirección
            if (lockedAirDirection == Vector2.zero)
                lockedAirDirection = motor.GetFacing2D();

            dir = lockedAirDirection;
        }
        else
        {
            // cuando estamos en suelo usamos dirección normal
            dir = motor.GetFacing2D();
            lockedAirDirection = Vector2.zero;
        }

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.down;

        dir.Normalize();

        animator.SetBool("IsMoving", isMoving);
        animator.SetFloat("MoveX", dir.x);
        animator.SetFloat("MoveY", dir.y);
    }
}