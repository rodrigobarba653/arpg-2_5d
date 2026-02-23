using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;

    [Header("Tuning")]
    public float moveDeadzone = 0.01f;

    [Header("Debug")]
    public bool logInput = false;

    private Vector2 moveInput;
    private Vector2 lastDir = Vector2.down;

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
        // Auto-grab animator if you forgot to assign it
        if (!animator) animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (!animator) return;

        bool isMoving = moveInput.sqrMagnitude > (moveDeadzone * moveDeadzone);

        if (isMoving)
            lastDir = moveInput.normalized;

        animator.SetBool("IsMoving", isMoving);
        animator.SetFloat("MoveX", lastDir.x);
        animator.SetFloat("MoveY", lastDir.y);
    }
}