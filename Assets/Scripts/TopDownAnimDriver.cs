using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Tooltip("Drag the Player root that has PlayerMotor (NOT SpriteBody).")]
    [SerializeField] private PlayerMotor motor;

    [Header("Tuning")]
    [SerializeField] private float moveDeadzone = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logInput = false;

    private Vector2 moveInput;
    private float moveDeadzoneSqr;

    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled)
        {
            moveInput = Vector2.zero;
            if (logInput) Debug.Log("[TopDownAnimDriver] Move canceled -> zero");
            return;
        }

        moveInput = ctx.ReadValue<Vector2>();
        if (logInput) Debug.Log($"[TopDownAnimDriver] Move: {moveInput} phase={ctx.phase}");
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();

        if (!motor) motor = GetComponentInParent<PlayerMotor>();
        if (!motor) Debug.LogError("[TopDownAnimDriver] Missing PlayerMotor reference (assign motor or ensure it's on a parent).", this);

        moveDeadzoneSqr = moveDeadzone * moveDeadzone;
    }

    void Update()
    {
        if (!animator || !motor) return;

        bool isMoving = moveInput.sqrMagnitude > moveDeadzoneSqr;

        // Direction always comes from motor facing (respects facing lock during roll/attack)
        Vector2 dir = motor.GetFacing2D();
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
        dir.Normalize();

        animator.SetBool("IsMoving", isMoving);
        animator.SetFloat("MoveX", dir.x);
        animator.SetFloat("MoveY", dir.y);
    }
}