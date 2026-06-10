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

    public bool lockMovementDuringAttack = true;

    [Header("Attack Step Forward")]
    public bool useAttackStepForward = true;
    public float attackStepDistance = 0.35f;
    public float attackStepDuration = 0.12f;
    public float attackStepDelay = 0.05f;

    [Tooltip("Enemy will not step closer than this distance from the player.")]
    public float minDistanceToPlayerAfterStep = 1.0f;

    [Header("Hitbox")]
    public float hitboxForwardDistance = 0.6f;

    [Tooltip("Extra local offset for the melee hitbox. X/Z are used normally. Y is overridden by Hitbox Local Height below.")]
    public Vector3 hitboxLocalOffset;

    [Tooltip("Fixed local Y height for the melee hitbox. Raise this if the hitbox is too low.")]
    public float hitboxLocalHeight = 0.5f;

    [Header("Damage Reaction")]
    public float hitStunTime = 0.3f;
    public float attackDelayAfterHit = 0.45f;

    [Header("Timer-based Hitbox (no anim events needed)")]
    public bool useTimerBasedHitbox = true;
    public float hitboxEnableDelay = 0.20f;
    public float hitboxDisableDelay = 0.35f;
    public float attackEndDelay = 0.55f;

    [Header("Safety")]
    public float maxAttackDuration = 3f;

    float hitStunUntil;
    float nextAttackTime;
    float attackStartedAt;
    bool isAttacking;
    bool hitboxEnabledByTimer;
    bool hitboxDisabledByTimer;

    bool attackStepActive;
    bool attackStepStarted;
    Vector3 attackStepDirection;
    float attackStepMoved;

    static readonly int AttackTrigger = Animator.StringToHash("Attack");
    static readonly int HurtTrigger = Animator.StringToHash("Hurt");
    static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

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

        if (isAttacking)
            UpdateAttackStepForward();

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

        attackStepActive = false;
        attackStepStarted = false;
        attackStepMoved = 0f;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        attackStepDirection = dir.normalized;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        animator.SetBool(IsAttackingHash, true);
        animator.ResetTrigger(HurtTrigger);
        animator.ResetTrigger(AttackTrigger);
        animator.SetTrigger(AttackTrigger);

        nextAttackTime = Time.time + attackCooldown;
    }

    void UpdateAttackStepForward()
    {
        if (!useAttackStepForward)
            return;

        float elapsed = Time.time - attackStartedAt;

        if (!attackStepStarted)
        {
            if (elapsed < attackStepDelay)
                return;

            attackStepStarted = true;
            attackStepActive = true;
        }

        if (!attackStepActive)
            return;

        if (attackStepDuration <= 0f || attackStepDistance <= 0f)
        {
            attackStepActive = false;
            return;
        }

        float remainingStep = attackStepDistance - attackStepMoved;

        if (remainingStep <= 0f)
        {
            attackStepActive = false;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        float allowedDistance = distToPlayer - minDistanceToPlayerAfterStep;

        if (allowedDistance <= 0f)
        {
            attackStepActive = false;
            return;
        }

        float speed = attackStepDistance / attackStepDuration;
        float moveAmount = speed * Time.deltaTime;

        moveAmount = Mathf.Min(moveAmount, remainingStep);
        moveAmount = Mathf.Min(moveAmount, allowedDistance);

        if (motor != null && motor.controller != null)
        {
            motor.controller.Move(attackStepDirection * moveAmount);
        }
        else
        {
            transform.position += attackStepDirection * moveAmount;
        }

attackStepMoved += moveAmount;
    }

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

    public void DisableHitbox()
    {
        if (meleeHitbox)
            meleeHitbox.SetActive(false);
    }

    public void EndAttack()
    {
        if (!isAttacking)
            return;

        isAttacking = false;
        attackStepActive = false;
        attackStepStarted = false;

        if (animator != null)
            animator.SetBool(IsAttackingHash, false);

        DisableHitbox();

        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    public bool IsAttacking() => isAttacking;
    public bool LockMovementDuringAttack => lockMovementDuringAttack;

    void OnDisable()
    {
        EnemyAttackScheduler.ReleaseIfExists(this);
        DisableHitbox();
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

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        dir.Normalize();

        Vector3 localDir = transform.InverseTransformDirection(dir);
        Vector3 forward = new Vector3(localDir.x, 0f, localDir.z).normalized;

        Vector3 finalLocalPosition =
            forward * hitboxForwardDistance + hitboxLocalOffset;

        finalLocalPosition.y = hitboxLocalHeight;

        meleeHitboxTransform.localPosition = finalLocalPosition;
    }

    public void OnTakeDamage(Vector3 hitDir)
    {
        hitDir.y = 0f;

        hitStunUntil = Time.time + hitStunTime;
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + attackDelayAfterHit);

        attackStepActive = false;
        attackStepStarted = false;

        if (isAttacking)
        {
            isAttacking = false;

            if (animator != null)
                animator.SetBool(IsAttackingHash, false);

            EnemyAttackScheduler.ReleaseIfExists(this);
        }

        DisableHitbox();

        if (motor != null)
            motor.Stop();

        if (animator != null)
        {
            Vector3 sourceDir = -hitDir.normalized;

            animator.SetFloat(HitXHash, sourceDir.x);
            animator.SetFloat(HitYHash, sourceDir.z);

            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(HurtTrigger);
            animator.SetTrigger(HurtTrigger);
        }
    }
}