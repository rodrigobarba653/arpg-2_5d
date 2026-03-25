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
        if (jumpLockTimer > 0f)
            jumpLockTimer -= Time.deltaTime;

        CheckGround();
        HandleAirState();
        HandleLanding();
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
        if (ctx.started)
        {
            if (!isGrounded)
                return;

            if (jumpLockTimer > 0f)
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
        jumpLockTimer = jumpCooldown;

        if (animator)
        {
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

        if (landedThisFrame)
        {
            if (airTimer >= minAirTimeForLanding)
            {
                landingLock = true;
                landingTimer = landingLockTime;

                if (motor != null)
                    motor.LockMovement(true);

                if (animator)
                    animator.SetTrigger("Land");

                if (debugLog)
                    Debug.Log("Landing (valid)");
            }
            else
            {
                if (debugLog)
                    Debug.Log("Landing ignored (too short)");
            }

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

            if (!inAir && (airTimer > airJumpDelay || motor.GetVerticalVelocity() < -2f))
            {
                inAir = true;

                if (animator)
                {
                    animator.SetTrigger("Jump");

                    if (debugLog)
                        Debug.Log("Air state");
                }
            }
        }
        else
        {
            inAir = false;
            airTimer = 0f;
        }
    }

    void UpdateAnimator()
    {
        if (!animator)
            return;

        animator.SetBool("IsGrounded", isGrounded);
    }
}