using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator spriteAnimator;
    [SerializeField] private GameObject meleeHitbox;
    [SerializeField] private Transform meleeHitboxTransform;
    [SerializeField] private PlayerMotor motor;

    [Header("Combo")]
    [SerializeField] private int maxCombo = 3;
    [SerializeField] private float comboBufferTime = 0.25f;

    [Header("Combo Lockouts")]
    [SerializeField] private float comboEndCooldown = 0.25f;
    [SerializeField] private float missComboCooldown = 0.15f;

    [Header("Combat Mode")]
    [SerializeField] private float combatTimeout = 3.5f;

    [Header("Attack Lunge")]
    [SerializeField] private bool useAttackLunge = true;
    [SerializeField] private bool lungeStep1 = true;
    [SerializeField] private bool lungeStep2 = true;
    [SerializeField] private bool lungeStep3 = true;

    [System.Serializable]
    private struct LungeParams
    {
        public float speed;
        public float duration;
        public float preStop;
        public float postStop;
    }

    [Header("Lunge Params Per Step")]
    [SerializeField] private LungeParams step1 = new LungeParams { speed = 6.5f, duration = 0.08f, preStop = 0.03f, postStop = 0.04f };
    [SerializeField] private LungeParams step2 = new LungeParams { speed = 6.5f, duration = 0.06f, preStop = 0.02f, postStop = 0.03f };
    [SerializeField] private LungeParams step3 = new LungeParams { speed = 7.5f, duration = 0.08f, preStop = 0.02f, postStop = 0.05f };

    [Header("Roll")]
    [SerializeField] private bool enableRoll = true;
    [SerializeField] private float rollSpeed = 8f;
    [SerializeField] private float rollDuration = 0.25f;
    [SerializeField] private float rollCooldown = 0.15f;

    [Tooltip("If no move input, roll uses last facing direction.")]
    [SerializeField] private bool rollUsesFacingIfNoInput = true;

    [Header("Hitbox Position (in front)")]
    [SerializeField] private float hitboxForwardDistance = 0.60f;
    [SerializeField] private Vector3 hitboxLocalOffset = Vector3.zero;

    // Animator hashes
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int ComboIndexHash  = Animator.StringToHash("ComboIndex");
    private static readonly int InCombatHash    = Animator.StringToHash("InCombat");
    private static readonly int AttackTrigger   = Animator.StringToHash("Attack");
    private static readonly int IsRollingHash   = Animator.StringToHash("IsRolling");
    private static readonly int RollTrigger     = Animator.StringToHash("Roll");

    private int comboIndex = 0;
    private bool isAttacking = false;

    // buffer window
    private float comboBufferUntil = 0f;
    private bool buffered = false;

    // lockout window
    private float lockoutUntil = 0f;

    // combat mode timer
    private float combatTimer = 0f;
    private bool inCombat = false;

    // roll state
    private bool isRolling = false;
    private float rollEndTime = 0f;
    private float rollCooldownUntil = 0f;

    void Awake()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();

        if (!spriteAnimator) Debug.LogError("Assign SpriteBody Animator", this);
        if (!meleeHitbox) Debug.LogError("Assign MeleeHitbox GameObject", this);

        if (!meleeHitboxTransform && meleeHitbox)
            meleeHitboxTransform = meleeHitbox.transform;

        if (spriteAnimator)
        {
            spriteAnimator.SetBool(IsAttackingHash, false);
            spriteAnimator.SetInteger(ComboIndexHash, 0);
            spriteAnimator.SetBool(IsRollingHash, false);
        }

        DisableHitbox();
    }

    void Update()
    {
        // buffer expiration
        if (buffered && Time.time > comboBufferUntil)
            buffered = false;

        // roll auto-end
        if (isRolling && Time.time >= rollEndTime)
            EndRoll();

        // combat mode timeout
        if (inCombat)
        {
            combatTimer -= Time.deltaTime;
            if (combatTimer <= 0f && !isAttacking && !isRolling)
                ExitCombat();
        }
    }

    // =========================
    // INPUT
    // =========================

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (isRolling) return;

        if (!isAttacking && Time.time < lockoutUntil)
            return;

        EnterCombat();

        if (!isAttacking)
        {
            StartAttack1();
            return;
        }

        buffered = true;
        comboBufferUntil = Time.time + comboBufferTime;
    }

    public void OnRoll(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (!enableRoll) return;

        if (isAttacking) return;

        if (Time.time < rollCooldownUntil) return;

        StartRoll();
    }

    // =========================
    // ATTACKS
    // =========================

    private void StartAttack1()
    {
        isAttacking = true;
        comboIndex = 1;
        buffered = false;

        // Lock facing for the whole combo (prevents SpriteBody flipping mid-attack)
        motor?.LockFacing(motor.GetFacing2D());

        motor?.LockMovement(true);
        DoStepLunge(1);

        spriteAnimator?.SetBool(IsAttackingHash, true);
        spriteAnimator?.SetInteger(ComboIndexHash, comboIndex);

        spriteAnimator?.ResetTrigger(AttackTrigger);
        spriteAnimator?.SetTrigger(AttackTrigger);

        combatTimer = combatTimeout;
    }

    private void AdvanceCombo()
    {
        if (comboIndex >= maxCombo) return;

        comboIndex++;
        buffered = false;

        DoStepLunge(comboIndex);

        spriteAnimator?.SetInteger(ComboIndexHash, comboIndex);

        spriteAnimator?.ResetTrigger(AttackTrigger);
        spriteAnimator?.SetTrigger(AttackTrigger);

        combatTimer = combatTimeout;
    }

    private void DoStepLunge(int step)
    {
        if (!useAttackLunge || motor == null) return;

        bool enabled =
            step == 1 ? lungeStep1 :
            step == 2 ? lungeStep2 :
                        lungeStep3;

        if (!enabled) return;

        LungeParams p =
            step == 1 ? step1 :
            step == 2 ? step2 :
                        step3;

        // Facing is locked already, so GetFacing2D returns the locked value
        motor.BeginAttackLunge(motor.GetFacing2D(), p.speed, p.duration, p.preStop, p.postStop);
    }

    // Animation Event placed near the end of Attack1 and Attack2
    public void TryAdvanceCombo()
    {
        if (!isAttacking) return;

        if (buffered && Time.time <= comboBufferUntil && comboIndex < maxCombo)
        {
            AdvanceCombo();
            return;
        }

        lockoutUntil = Time.time + missComboCooldown;
        buffered = false;
    }

    // Animation Event at the end of Attack1/2/3
    public void EndAttack()
    {
        if (!isAttacking) return;

        if (comboIndex >= maxCombo)
            lockoutUntil = Time.time + comboEndCooldown;

        EndCombo();
    }

    private void EndCombo()
    {
        isAttacking = false;
        comboIndex = 0;
        buffered = false;

        spriteAnimator?.SetBool(IsAttackingHash, false);
        spriteAnimator?.SetInteger(ComboIndexHash, 0);

        motor?.LockMovement(false);
        motor?.UnlockFacing();

        DisableHitbox();

        combatTimer = combatTimeout;
    }

    // =========================
    // HITBOX EVENTS
    // =========================

    public void EnableHitboxInt(int step)
    {
        if (!meleeHitbox || !meleeHitboxTransform || motor == null) return;

        PositionHitboxInFront();
        meleeHitbox.SetActive(true);

        var hb = meleeHitbox.GetComponent<MeleeHitbox>();
        if (hb) hb.SetAttackStep(step);
    }

    public void DisableHitbox()
    {
        if (meleeHitbox) meleeHitbox.SetActive(false);
    }

    private void PositionHitboxInFront()
    {
        Vector2 dir2 = motor.GetFacing2D();
        if (dir2.sqrMagnitude < 0.001f) dir2 = Vector2.down;
        dir2.Normalize();

        Vector3 forward = new Vector3(dir2.x, 0f, dir2.y);
        meleeHitboxTransform.localPosition = forward * hitboxForwardDistance + hitboxLocalOffset;
    }

    // =========================
    // ROLL
    // =========================

    private void StartRoll()
    {
        isRolling = true;

        rollEndTime = Time.time + rollDuration;
        rollCooldownUntil = rollEndTime + rollCooldown;

        EnterCombat();

        motor?.LockMovement(true);

        Vector2 rollDir2D = motor != null ? motor.GetMoveInput2D() : Vector2.zero;

        if (rollDir2D.sqrMagnitude < 0.01f && rollUsesFacingIfNoInput && motor != null)
            rollDir2D = motor.GetFacing2D();

        if (rollDir2D.sqrMagnitude < 0.01f)
            rollDir2D = Vector2.down;

        rollDir2D.Normalize();

        motor?.BeginRoll(rollDir2D, rollSpeed, rollDuration);

        spriteAnimator?.SetBool(IsRollingHash, true);
        spriteAnimator?.ResetTrigger(RollTrigger);
        spriteAnimator?.SetTrigger(RollTrigger);

        combatTimer = combatTimeout;
    }

    public void EndRoll()
    {
        isRolling = false;

        motor?.EndRoll();
        motor?.LockMovement(false);
        motor?.UnlockFacing();

        spriteAnimator?.SetBool(IsRollingHash, false);

        combatTimer = combatTimeout;
    }

    // =========================
    // COMBAT MODE
    // =========================

    private void EnterCombat()
    {
        combatTimer = combatTimeout;
        if (inCombat) return;

        inCombat = true;
        spriteAnimator?.SetBool(InCombatHash, true);
    }

    private void ExitCombat()
    {
        inCombat = false;
        spriteAnimator?.SetBool(InCombatHash, false);
    }
}