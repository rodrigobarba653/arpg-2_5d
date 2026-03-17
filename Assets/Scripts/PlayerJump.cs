using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerJump : MonoBehaviour
{
    [Header("References")]
    public PlayerMotor motor;
    public Animator animator;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Landing")]
    public float landingLockTime = 0.12f;

    [Header("Debug")]
    public bool debugLog = false;

    [Header("Air")]
    public float airJumpDelay = 0.08f; // tiempo antes de activar jump idle

    private float airTimer;
    private bool inAir;

    private bool isGrounded;
    private bool wasGrounded;
    private bool jumpHeld;

    private bool landingLock;
    private float landingTimer;

    public bool IsGrounded => isGrounded;

    void Awake()
    {
        if (!motor)
            motor = GetComponent<PlayerMotor>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        CheckGround();

        HandleAirState();
        HandleLanding();

        UpdateAnimator();
    }

    void CheckGround()
    {
        bool ccGrounded = motor.IsGrounded();

        // grounded estable (evita falso grounded en el aire)
        if (ccGrounded && motor.GetVerticalVelocity() <= 0f)
            isGrounded = true;
        else
            isGrounded = false;
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (!isGrounded)
                return;

            if (landingLock)
                return;

            Jump();
        }
    }

    void Jump()
    {
        if (debugLog)
            Debug.Log("Jump");

        motor.SetVerticalVelocity(jumpForce);

        if (animator)
        {
            // ✅ usar dirección actual aunque no haya input
            Vector2 dir = motor.GetFacing2D();

            animator.SetFloat("MoveX", dir.x);
            animator.SetFloat("MoveY", dir.y);

            animator.SetTrigger("Jump");
        }

        isGrounded = false;
        wasGrounded = false;
    }

    void HandleLanding()
    {
        bool landedThisFrame = !wasGrounded && isGrounded;

        // Solo hacer landing real si antes SI entro a estado de aire
        if (landedThisFrame && inAir)
        {
            landingLock = true;
            landingTimer = landingLockTime;

            if (motor != null)
                motor.LockMovement(true);

            if (animator)
                animator.SetTrigger("Land");

            inAir = false;
            airTimer = 0f;
        }
        else if (landedThisFrame)
        {
            // Cayó muy poquito / escalón / no alcanzó airJumpDelay
            // No bloquear, no hacer landing anim
            inAir = false;
            airTimer = 0f;
        }

        if (landingLock)
        {
            landingTimer -= Time.deltaTime;

            if (landingTimer <= 0f)
            {
                landingLock = false;

                if (motor != null)
                    motor.LockMovement(false);
            }
        }

        wasGrounded = isGrounded;
    }

    void HandleAirState()
    {
        if (!isGrounded)
        {
            airTimer += Time.deltaTime;

            // esperar un poco antes de activar animación de aire
            if (!inAir && airTimer > airJumpDelay)
            {
                inAir = true;

                if (animator)
                {
                    animator.SetTrigger("Jump");

                    if (debugLog)
                        Debug.Log("Air jump trigger");
                }
            }
        }
        else
        {
            airTimer = 0f;
            inAir = false;
        }
    }

    void UpdateAnimator()
    {
        if (!animator)
            return;

        animator.SetBool("IsGrounded", isGrounded);
    }
}