using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerJump : MonoBehaviour
{
    [Header("References")]
    public PlayerMotor motor;
    public Animator animator;
    PlayerSwimming swim;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Landing")]
    public float landingLockTime = 0.12f;
    public float minAirTimeForLanding = 0.5f;

    [Header("Debug")]
    public bool debugLog = false;

    [Header("Air")]
    public float airJumpDelay = 0.08f;

    [Header("Jump Lock")]
    public float jumpCooldown = 0.15f;

    float jumpLockTimer;

    private float airTimer;
    private bool inAir;

    private bool isGrounded;
    private bool wasGrounded;

    private bool landingLock;
    private float landingTimer;

    // this tells us whether the player actually pressed jump,
    // so walking off a ledge does NOT play the Jump trigger
    private bool jumpStartedFromGround;

    public bool IsGrounded => isGrounded;

    void Awake()
    {
        if (!motor)
            motor = GetComponent<PlayerMotor>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        swim = GetComponent<PlayerSwimming>();
    }

    void Update()
{
    if (jumpLockTimer > 0f)
        jumpLockTimer -= Time.deltaTime;

    CheckGround();
    HandleLanding();
    HandleAirState();
    UpdateAnimator();
}

    void CheckGround()
    {
        CharacterController cc = motor != null ? motor.GetCharacterController() : null;

        if (cc == null)
        {
            isGrounded = false;
            return;
        }

        bool ccGrounded = cc.isGrounded;

        float probeDistance = 0.35f;
        float radius = Mathf.Max(0.05f, cc.radius * 0.9f);

        Vector3 worldCenter = transform.position + cc.center;
        Vector3 sphereOrigin = worldCenter + Vector3.up * 0.05f;

        bool foundGround = Physics.SphereCast(
            sphereOrigin,
            radius,
            Vector3.down,
            out RaycastHit hit,
            probeDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        bool slopeWalkable = false;

        if (foundGround)
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            slopeWalkable = slopeAngle <= cc.slopeLimit + 0.5f;

            if (debugLog)
                Debug.Log($"Ground hit: {hit.collider.name} | slope: {slopeAngle:F2} | walkable: {slopeWalkable}");
        }

        if ((ccGrounded || slopeWalkable) && motor.GetVerticalVelocity() <= 0.5f)
            isGrounded = true;
        else
            isGrounded = false;
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (swim != null && swim.IsSwimming())
            return;

        if (!ctx.started)
            return;

        if (!isGrounded)
            return;

        if (jumpLockTimer > 0f)
            return;

        if (landingLock)
            return;

        Jump();
    }

    void Jump()
    {
        if (swim != null && swim.IsSwimming())
            return;

        if (debugLog)
            Debug.Log("Jump");

        motor.SetVerticalVelocity(jumpForce);
        jumpLockTimer = jumpCooldown;
        jumpStartedFromGround = true;

        if (animator)
        {
            Vector2 dir = motor.GetFacing2D();

            animator.SetFloat("MoveX", dir.x);
            animator.SetFloat("MoveY", dir.y);

            animator.ResetTrigger("Land");
            animator.SetTrigger("Jump");
        }

        isGrounded = false;
        wasGrounded = false;
    }

    void HandleLanding()
    {
        bool landedThisFrame = !wasGrounded && isGrounded;

        if (landedThisFrame)
        {
            if (airTimer >= minAirTimeForLanding)
            {
                landingLock = true;
                landingTimer = landingLockTime;

                if (motor != null)
                    motor.LockMovement(true);

                if (animator)
                {
                    animator.SetBool("InAir", false);
                    animator.ResetTrigger("Jump");
                    animator.SetTrigger("Land");
                }

                if (debugLog)
                    Debug.Log("Landing (valid)");
            }
            else
            {
                if (animator)
                    animator.SetBool("InAir", false);

                if (debugLog)
                    Debug.Log("Landing ignored (too short)");
            }

            inAir = false;
            airTimer = 0f;
            jumpStartedFromGround = false;
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

            // Only enter InAir after enough real airtime.
            // This removes animation changes for super tiny drops.
            if (!inAir && airTimer >= airJumpDelay)
            {
                inAir = true;

                if (animator)
                {
                    animator.SetBool("InAir", true);

                    if (debugLog)
                    {
                        if (jumpStartedFromGround)
                            Debug.Log("Entered InAir after jump");
                        else
                            Debug.Log("Entered InAir from ledge fall");
                    }
                }
            }
        }
        else
        {
            if (inAir && animator)
                animator.SetBool("InAir", false);

            inAir = false;
            airTimer = 0f;
            jumpStartedFromGround = false;
        }
    }

    void UpdateAnimator()
    {
        if (!animator)
            return;

        animator.SetBool("IsGrounded", isGrounded);
    }

    public void ForceExitAirState()
    {
        inAir = false;
        airTimer = 0f;

        landingLock = false;
        jumpLockTimer = 0f;
        jumpStartedFromGround = false;

        isGrounded = true;
        wasGrounded = true;

        if (animator)
        {
            animator.ResetTrigger("Jump");
            animator.ResetTrigger("Land");
            animator.SetBool("InAir", false);
            animator.SetBool("IsGrounded", true);
        }
    }
}