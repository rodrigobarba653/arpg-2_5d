using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerCombat2D : MonoBehaviour
{
    [Header("Roll Settings")]
    [SerializeField] float rollSpeed = 8f;
    [SerializeField] float rollDuration = 0.25f;

    [Header("Combo Settings")]
    [SerializeField] int maxCombo = 3;

    [Header("Combo Buffer")]
    [SerializeField] float comboBufferTime = 0.25f;

    [Header("HitBox")]
    [SerializeField] Transform swordHitbox;

    // ==============================================
    // CONFIGURACIÓN DE HITBOX POR DIRECCIÓN
    // ==============================================

    [System.Serializable]
    public class HitboxConfig
    {
        public Vector3 localPosition;
        public Vector3 size;
    }

    [Header("Hitbox Configurations")]
    [SerializeField] HitboxConfig right;
    [SerializeField] HitboxConfig left;
    [SerializeField] HitboxConfig up;
    [SerializeField] HitboxConfig down;
    [SerializeField] HitboxConfig upRight;
    [SerializeField] HitboxConfig upLeft;
    [SerializeField] HitboxConfig downRight;
    [SerializeField] HitboxConfig downLeft;

    Animator animator;
    PlayerMovement3D movement;
    PlayerInputActions input;
    PlayerAnimator2D_8Dir animator8Dir;
    Rigidbody rb;

    public bool inCombat = false;
    public bool isAttacking = false;
    public bool isRolling = false;

    int comboIndex = 0;
    float comboBufferTimer = 0f;

    float rollTimer;
    Vector3 rollDirection;
    Vector2 rollAnimDirection;

    enum CombatState
    {
        Free,
        Attacking,
        Rolling
    }

    CombatState currentState = CombatState.Free;

    // ==============================================
    // INITIALIZATION
    // ==============================================

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement3D>();
        animator8Dir = GetComponent<PlayerAnimator2D_8Dir>();
        rb = GetComponent<Rigidbody>();

        input = new PlayerInputActions();
        input.Player.Enable();
    }

    void Update()
    {
        if (input.Player.Attack.WasPressedThisFrame())
            TryAttack();

        if (input.Player.Roll.WasPressedThisFrame())
            TryRoll();

        if (comboBufferTimer > 0f)
            comboBufferTimer -= Time.deltaTime;

        HandleRollMovement();
    }

    void LateUpdate()
    {
        if (currentState == CombatState.Attacking &&
            !animator.GetBool("IsAttacking"))
        {
            FinishCombo();
        }
    }

    // ==============================================
    // ATTACK
    // ==============================================

    void TryAttack()
    {
        if (currentState == CombatState.Rolling)
            return;

        if (!inCombat)
            EnterCombatMode();

        if (currentState == CombatState.Free)
        {
            StartFirstAttack();
        }
        else if (currentState == CombatState.Attacking)
        {
            comboBufferTimer = comboBufferTime;
        }
    }

    void StartFirstAttack()
    {
        currentState = CombatState.Attacking;
        isAttacking = true;
        movement.canMove = false;

        comboIndex = 1;

        animator.SetBool("IsAttacking", true);
        animator.SetInteger("ComboIndex", comboIndex);
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");
    }

    void AdvanceCombo()
    {
        comboIndex++;

        animator.SetInteger("ComboIndex", comboIndex);
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");
    }

    public void TryAdvanceCombo()
    {
        if (comboBufferTimer > 0f && comboIndex < maxCombo)
        {
            comboBufferTimer = 0f;
            AdvanceCombo();
        }
        else
        {
            FinishCombo();
        }
    }

    public void FinishCombo()
    {
        currentState = CombatState.Free;
        isAttacking = false;
        comboIndex = 0;
        comboBufferTimer = 0f;

        movement.canMove = true;

        animator.SetBool("IsAttacking", false);
        animator.SetInteger("ComboIndex", 0);
    }

    // ==============================================
    // ROLL
    // ==============================================

    void TryRoll()
    {
        if (currentState != CombatState.Free)
            return;

        StartRoll();
    }

    void StartRoll()
    {
        currentState = CombatState.Rolling;
        isRolling = true;

        movement.isExternalMovement = true;
        movement.canMove = false;

        rollTimer = rollDuration;

        Vector2 moveInput = input.Player.Move.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude > 0.01f)
            rollAnimDirection = moveInput.normalized;
        else
            rollAnimDirection = animator8Dir.GetLastDirection();

        rollDirection = new Vector3(rollAnimDirection.x, 0f, rollAnimDirection.y);

        animator.SetFloat("MoveX", rollAnimDirection.x);
        animator.SetFloat("MoveY", rollAnimDirection.y);

        animator.SetBool("IsRolling", true);
        animator.ResetTrigger("Roll");
        animator.SetTrigger("Roll");
    }

    void HandleRollMovement()
    {
        if (currentState != CombatState.Rolling)
            return;

        rollTimer -= Time.deltaTime;
        rb.linearVelocity = rollDirection * rollSpeed;

        if (rollTimer <= 0f)
            EndRoll();
    }

    public void EndRoll()
    {
        currentState = CombatState.Free;
        isRolling = false;

        movement.isExternalMovement = false;
        movement.canMove = true;

        rb.linearVelocity = Vector3.zero;
        animator.SetBool("IsRolling", false);
    }

    void EnterCombatMode()
    {
        inCombat = true;
        animator.SetBool("InCombat", true);
    }

    public bool IsRolling()
    {
        return currentState == CombatState.Rolling;
    }

    // ==============================================
    // HITBOX SYSTEM
    // ==============================================

    public void EnableSwordHitbox()
    {
        PositionHitbox();
        swordHitbox.gameObject.SetActive(true);
    }

    public void DisableSwordHitbox()
    {
        swordHitbox.gameObject.SetActive(false);
    }

    void PositionHitbox()
    {
        Vector2 dir = animator8Dir.GetLastDirection().normalized;
        HitboxConfig config = GetConfigFromDirection(dir);

        if (config == null) return;

        swordHitbox.localPosition = config.localPosition;

        BoxCollider box = swordHitbox.GetComponent<BoxCollider>();
        box.size = config.size;
    }

    HitboxConfig GetConfigFromDirection(Vector2 dir)
    {
        if (dir.x > 0.5f && Mathf.Abs(dir.y) < 0.5f) return right;
        if (dir.x < -0.5f && Mathf.Abs(dir.y) < 0.5f) return left;
        if (dir.y > 0.5f && Mathf.Abs(dir.x) < 0.5f) return up;
        if (dir.y < -0.5f && Mathf.Abs(dir.x) < 0.5f) return down;

        if (dir.x > 0 && dir.y > 0) return upRight;
        if (dir.x < 0 && dir.y > 0) return upLeft;
        if (dir.x > 0 && dir.y < 0) return downRight;

        return downLeft;
    }

    void OnEnable() => input?.Player.Enable();
    void OnDisable() => input?.Player.Disable();
}
