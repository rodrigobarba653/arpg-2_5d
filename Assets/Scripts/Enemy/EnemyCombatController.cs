using UnityEngine;

public class EnemyCombatController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;
    EnemyAI ai;

    public GameObject meleeHitbox;
    public Transform meleeHitboxTransform;

    public Transform player;

    [Header("Attack")]
    public float attackDistance = 1.4f;
    public float attackCooldown = 1.2f;

    [Tooltip("If true, the enemy stops moving during the attack animation (no root motion). " +
             "Disable for attacks where the enemy can keep moving (e.g. ranged/long-reach).")]
    public bool lockMovementDuringAttack = true;

    [Header("Hitbox")]
    public float hitboxForwardDistance = 0.6f;
    public Vector3 hitboxLocalOffset;

    [Header("Damage Reaction")]
    public float hitStunTime = 0.3f;

    [Tooltip("Extra time before the enemy can attack again after being hit")]
    public float attackDelayAfterHit = 0.45f;

    [Header("Timer-based Hitbox (no anim events needed)")]
    [Tooltip("If true, the hitbox enables/disables and the attack ends automatically " +
             "based on the timers below. Animation events are still supported (idempotent).")]
    public bool useTimerBasedHitbox = true;

    [Tooltip("Seconds from StartAttack until the hitbox enables (wind-up).")]
    public float hitboxEnableDelay = 0.20f;

    [Tooltip("Seconds from StartAttack until the hitbox disables.")]
    public float hitboxDisableDelay = 0.35f;

    [Tooltip("Seconds from StartAttack until EndAttack fires (full attack length). " +
             "Should be >= hitboxDisableDelay.")]
    public float attackEndDelay = 0.55f;

    [Header("Safety")]
    [Tooltip("If isAttacking persists longer than this, force EndAttack as a hard safety net.")]
    public float maxAttackDuration = 3f;

    float hitStunUntil;
    float nextAttackTime;
    float attackStartedAt;
    bool isAttacking;
    bool hitboxEnabledByTimer;
    bool hitboxDisabledByTimer;

    static readonly int AttackTrigger = Animator.StringToHash("Attack");
    static readonly int HurtTrigger = Animator.StringToHash("Hurt");
    static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

    // IMPORTANT: these must match your animator parameter names
    static readonly int HitXHash = Animator.StringToHash("HitX");
    static readonly int HitYHash = Animator.StringToHash("HitY");

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        ai = GetComponent<EnemyAI>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        DisableHitbox();
    }

    void Update()
    {
        if (!player)
            return;

        // ===== Timer-based hitbox =====
        if (isAttacking && useTimerBasedHitbox)
        {
            float elapsed = Time.time - attackStartedAt;

            if (!hitboxEnabledByTimer && elapsed >= hitboxEnableDelay)
            {
                EnableHitbox();
                hitboxEnabledByTimer = true;
            }

            if (!hitboxDisabledByTimer && elapsed >= hitboxDisableDelay)
            {
                DisableHitbox();
                hitboxDisabledByTimer = true;
            }

            if (elapsed >= attackEndDelay)
                EndAttack();
        }

        // Safety: hard timeout in case both timer and events somehow fail.
        if (isAttacking && Time.time - attackStartedAt > maxAttackDuration)
            EndAttack();

        if (ai != null && ai.isAlerting)
            return;

        if (ai != null && ai.isDefending)
            return;

        if (isAttacking)
            return;

        if (Time.time < hitStunUntil)
            return;

        if (motor != null && motor.IsMovementLocked())
            return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > attackDistance)
            return;

        if (Time.time < nextAttackTime)
            return;

        if (!EnemyAttackScheduler.Instance.TryReserve(this))
            return;

        StartAttack();
    }

    void StartAttack()
    {
        isAttacking = true;
        attackStartedAt = Time.time;
        hitboxEnabledByTimer = false;
        hitboxDisabledByTimer = false;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        animator.SetBool(IsAttackingHash, true);
        animator.ResetTrigger(HurtTrigger);
        animator.ResetTrigger(AttackTrigger);
        animator.SetTrigger(AttackTrigger);

        nextAttackTime = Time.time + attackCooldown;
    }

    // ANIMATION EVENT
    public void EnableHitbox()
    {
        if (!meleeHitbox)
            return;

        PositionHitbox();

        var hb = meleeHitbox.GetComponent<MeleeHitbox>();

        if (hb)
            hb.SetOwner(transform);

        meleeHitbox.SetActive(true);
    }

    // ANIMATION EVENT
    public void DisableHitbox()
    {
        if (meleeHitbox)
            meleeHitbox.SetActive(false);
    }

    // Can be triggered by timer (default) or animation event.
    public void EndAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        if (animator != null) animator.SetBool(IsAttackingHash, false);

        // Make sure the hitbox is off even if DisableHitbox timer didn't fire yet.
        DisableHitbox();

        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    public bool IsAttacking() => isAttacking;
    public bool LockMovementDuringAttack => lockMovementDuringAttack;

    void OnDisable()
    {
        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    void OnDestroy()
    {
        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    void PositionHitbox()
    {
        if (!player || !meleeHitboxTransform)
            return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        dir.Normalize();

        Vector3 localDir = transform.InverseTransformDirection(dir);
        Vector3 forward = new Vector3(localDir.x, 0f, localDir.z).normalized;

        meleeHitboxTransform.localPosition =
            forward * hitboxForwardDistance + hitboxLocalOffset;
    }

    public void OnTakeDamage(Vector3 hitDir)
    {
        hitDir.y = 0f;

        hitStunUntil = Time.time + hitStunTime;

        // prevent immediate re-attack after damage
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + attackDelayAfterHit);

        // cancel attack if needed
        if (isAttacking)
        {
            isAttacking = false;
            animator.SetBool(IsAttackingHash, false);
            EnemyAttackScheduler.ReleaseIfExists(this);
        }

        DisableHitbox();

        if (motor != null)
            motor.Stop();

        if (animator != null)
        {
            // Convert world hit direction into animator 2D values
            // hitDir = direction enemy is pushed toward
            // we want the hurt source direction, so use -hitDir
            Vector3 sourceDir = -hitDir.normalized;

            animator.SetFloat(HitXHash, sourceDir.x);
            animator.SetFloat(HitYHash, sourceDir.z);

            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(HurtTrigger);
            animator.SetTrigger(HurtTrigger);
        }
    }
}