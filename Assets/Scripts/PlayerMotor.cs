using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Rigidbody rb;
    public Camera mainCam;
    public PlayerJump jump;

    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float gravity = -20f;
    public float deadZone = 0.15f;

    [Header("Direction")]
    public bool snapTo8Directions = true;
    public bool preserveAnalogMagnitude = false;

    [Header("Roll")]
    public bool rollActive;
    public float rollTimer;
    public float rollDuration;
    [Range(1f, 3f)] public float rollSpeedMultiplier = 1.2f;

    [Header("Air Control")]
    [Range(0f, 1f)]
    public float airControl = 0.4f;
    Vector3 airMoveDirection;

    // ATTACK LUNGE
    bool attackLungeActive;
    float attackLungeTimer;
    float attackLungeSpeed;
    Vector3 attackLungeDir;

    Vector3 rollDirection;
    
    private Vector2 moveInput;
    private Vector2 lastNonZeroFacing = Vector2.down;
    private Vector3 verticalVelocity;
    private Vector2 rawInput;
    public bool movementLocked = false;
    


    void Awake()
    {
        if (!controller)
            controller = GetComponent<CharacterController>();

        if (!jump)
            jump = GetComponent<PlayerJump>();

        if (!rb)
            rb = GetComponent<Rigidbody>();

        if (!mainCam)
            mainCam = Camera.main;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        rawInput = ctx.ReadValue<Vector2>();
    }

    void Update()
    {
        if (!controller)
            return;

        UpdateInput();

        Move();
    }

    void Move()
    {
        Vector3 moveWorld = Vector3.zero;
        float speed = moveSpeed;

        // ---------- ROLL ----------
        if (rollActive)
        {
            rollTimer -= Time.deltaTime;

            moveWorld = rollDirection;
            speed = moveSpeed * rollSpeedMultiplier;

            if (rollTimer <= 0f)
            {
                rollActive = false;
                movementLocked = false;
            }
        }

        // ---------- ATTACK LUNGE ----------
        else if (attackLungeActive)
        {
            attackLungeTimer -= Time.deltaTime;

            moveWorld = attackLungeDir;
            speed = attackLungeSpeed;

            if (attackLungeTimer <= 0f)
            {
                attackLungeActive = false;
            }
        }

        // ---------- NORMAL ----------
        else if (!movementLocked)
        {
            Vector3 targetMove = GetMoveWorld(moveInput);

            if (targetMove.sqrMagnitude > 1f)
                targetMove.Normalize();

            if (controller.isGrounded)
            {
                moveWorld = targetMove;
                airMoveDirection = moveWorld;
            }
            else
            {
                airMoveDirection = Vector3.Lerp(
                    airMoveDirection,
                    targetMove,
                    airControl
                );

                moveWorld = airMoveDirection;
            }
        }

        // ---------- GRAVITY ----------
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = moveWorld * speed;
        finalMove.y = verticalVelocity.y;

        controller.Move(finalMove * Time.deltaTime);
    }

    Vector3 GetMoveWorld(Vector2 input)
    {
        if (!mainCam)
            mainCam = Camera.main;

        if (!mainCam)
            return new Vector3(input.x, 0f, input.y);

        Vector3 camForward = mainCam.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = mainCam.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        return camForward * input.y + camRight * input.x;
    }

    static Vector2 SnapTo8(Vector2 v)
    {
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        float rad = snapped * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    // ===== API simple para animaciones =====

    public Vector2 GetMoveInput2D()
    {
        return moveInput;
    }

    public Vector2 GetFacing2D()
    {
        return lastNonZeroFacing;
    }

    public float GetRealSpeed()
    {
        if (!controller)
            return 0f;

        Vector3 v = controller.velocity;
        v.y = 0f;
        return v.magnitude;
    }

    public CharacterController GetCharacterController()
    {
        return controller;
    }

    public bool IsGrounded()
    {
        return controller != null && controller.isGrounded;
    }

    public float GetVerticalVelocity()
    {
        return verticalVelocity.y;
    }

    public void SetVerticalVelocity(float y)
    {
        verticalVelocity.y = y;
    }

    void UpdateInput()
    {
        if (movementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        float mag = rawInput.magnitude;

        if (mag < deadZone)
        {
            moveInput = Vector2.zero;
            return;
        }

        Vector2 dir = rawInput.normalized;

        if (snapTo8Directions)
            dir = SnapTo8(dir);

        if (preserveAnalogMagnitude)
        {
            float remapped = Mathf.InverseLerp(deadZone, 1f, mag);
            moveInput = dir * remapped;
        }
        else
        {
            moveInput = dir;
        }

        if (moveInput.sqrMagnitude > 0.0001f)
            lastNonZeroFacing = moveInput;
    }

    // ===== compatibilidad temporal =====

    public void LockMovement(bool locked)
    {
        movementLocked = locked;
    }
    public void LockFacing(Vector2 dir) { }
    public void UnlockFacing() { }


    // ROLL
    public void BeginRoll(Vector2 rollDir2D, float speed, float duration)
    {
        // ❌ no rodar si está en el aire
        if (jump != null && !jump.IsGrounded)
            return;
        
        // ❌ no rodar si ya está rodando
        if (rollActive)
            return;


        movementLocked = true;
        rollActive = true;

        rollDuration = duration;
        rollTimer = duration;

        Vector2 dir2D = rollDir2D;

        if (dir2D.sqrMagnitude < 0.01f)
            dir2D = lastNonZeroFacing;

        dir2D.Normalize();

        rollDirection = GetMoveWorld(dir2D);
        rollDirection.y = 0f;
        rollDirection.Normalize();
    }
    public void EndRoll()
    {
        rollActive = false;
        movementLocked = false;
    }
    public void BeginAttackLunge(Vector2 attackDir2D, float speed, float duration, float preStop, float postStop)
    {
        if (attackDir2D.sqrMagnitude < 0.01f)
            attackDir2D = lastNonZeroFacing;

        attackDir2D.Normalize();

        attackLungeDir = GetMoveWorld(attackDir2D);
        attackLungeDir.y = 0f;
        attackLungeDir.Normalize();

        attackLungeSpeed = speed;
        attackLungeTimer = duration;

        attackLungeActive = true;
    }
}