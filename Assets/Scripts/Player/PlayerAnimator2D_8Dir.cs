using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator2D_8Dir : MonoBehaviour
{
    Animator animator;
    SpriteRenderer sr;
    PlayerInputActions input;
    PlayerCombat2D combat;


    Vector2 moveInput;
    Vector2 lastDir = Vector2.down; // default

    void Awake()
    {
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        combat = GetComponent<PlayerCombat2D>();


        input = new PlayerInputActions();
        input.Player.Enable();
    }

    void Update()
    {
        moveInput = input.Player.Move.ReadValue<Vector2>();

        // Si estamos en roll, NO recalculamos dirección,
        // pero sí aseguramos que el flip esté correcto
        if (combat != null && combat.IsRolling())
        {
            float currentMoveX = animator.GetFloat("MoveX");

            if (currentMoveX > 0.01f)
                sr.flipX = true;
            else if (currentMoveX < -0.01f)
                sr.flipX = false;

            return;
        }

        bool isMoving = moveInput.sqrMagnitude > 0.01f && !animator.GetBool("IsAttacking");
        animator.SetBool("IsMoving", isMoving);

        if (isMoving)
            lastDir = moveInput.normalized;

        if (lastDir.x > 0.01f)
            sr.flipX = true;
        else if (lastDir.x < -0.01f)
            sr.flipX = false;

        float animX = (lastDir.x >= 0f) ? -lastDir.x : lastDir.x;

        animator.SetFloat("MoveX", animX);
        animator.SetFloat("MoveY", lastDir.y);
    }

    public Vector2 GetLastDirection()
    {
        return lastDir;
    }


    void OnEnable() => input?.Player.Enable();
    void OnDisable() => input?.Player.Disable();
}
