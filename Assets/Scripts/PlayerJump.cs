using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerJump : MonoBehaviour
{
    [Header("References")]
    public PlayerMotor motor;
    private Rigidbody rb;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Air Physics")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Ground Check")]
    public float groundRayLength = 0.25f;
    public LayerMask groundMask;

    [Header("Landing")]
    public float landingLockTime = 0.12f;
    public float minFallSpeedForLandingImpact = 2f;

    [Header("Debug")]
    public bool debugLog = false;

    private bool isGrounded;
    private bool jumpHeld;
    private bool wasGrounded;
    private bool landingLock;
    private float landingTimer;

    private float lastFrameYVelocity;

    public bool IsGrounded => isGrounded;
    public bool IsLandingLocked => landingLock;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (!motor)
            motor = GetComponent<PlayerMotor>();
    }

    void Update()
    {
        CheckGround();
        HandleBetterGravity();
        HandleLanding();

        lastFrameYVelocity = rb.linearVelocity.y;
    }

    void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, groundRayLength, groundMask))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    void HandleBetterGravity()
    {
        if (isGrounded && rb.linearVelocity.y <= 0f)
            return;

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.deltaTime;
        }
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            jumpHeld = true;

            if (!isGrounded || landingLock)
                return;

            Jump();
        }

        if (ctx.canceled)
        {
            jumpHeld = false;
        }
    }

    void Jump()
    {
        if (debugLog)
            Debug.Log("Jump!");

        Vector3 vel = rb.linearVelocity;
        vel.y = jumpForce;
        rb.linearVelocity = vel;

        isGrounded = false;
        wasGrounded = false;
    }

    void HandleLanding()
    {
        bool landedThisFrame = !wasGrounded && isGrounded;
        bool wasActuallyFalling = lastFrameYVelocity < -minFallSpeedForLandingImpact;

        if (landedThisFrame && wasActuallyFalling)
        {
            landingLock = true;
            landingTimer = landingLockTime;

            if (motor != null)
                motor.LockMovement(true);

            if (debugLog)
                Debug.Log($"Landing impact! fallSpeed={lastFrameYVelocity}");
        }

        if (landingLock)
        {
            landingTimer -= Time.deltaTime;

            if (landingTimer <= 0f)
            {
                landingLock = false;

                if (motor != null)
                    motor.LockMovement(false);

                if (debugLog)
                    Debug.Log("Landing lock ended");
            }
        }

        wasGrounded = isGrounded;
    }
}