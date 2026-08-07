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

    // ============================================================
    // ATTACK VARIANTS
    // ============================================================
    [System.Serializable]
    public class AttackVariant
    {
        [Tooltip("Human label for this attack, only shown in the Inspector.")]
        public string id = "Attack";

        [Tooltip("Animator trigger to fire when this attack starts. If empty, " +
                 "uses the default 'Attack' trigger.")]
        public string animatorTrigger = "";

        [Tooltip("Damage this attack deals. If -1, uses the MeleeHitbox's baseDamage.")]
        public int damage = -1;

        [Header("Range Restriction")]
        [Tooltip("Minimum distance to player for this attack to be selectable.")]
        public float minDistance = 0f;

        [Tooltip("Maximum distance to player for this attack to be selectable.")]
        public float maxDistance = 3f;

        [Header("Selection Weight")]
        [Tooltip("Relative probability of being picked when multiple attacks " +
                 "match the range. 0 = never picked automatically.")]
        [Min(0f)] public float weight = 1f;

        [Header("Timing Overrides")]
        [Tooltip("If > 0, overrides the base hitboxEnableDelay for this attack.")]
        public float hitboxEnableDelay = -1f;

        [Tooltip("If > 0, overrides the base hitboxDisableDelay for this attack.")]
        public float hitboxDisableDelay = -1f;

        [Tooltip("If > 0, overrides the base attackEndDelay for this attack.")]
        public float attackEndDelay = -1f;

        [Header("Cooldown")]
        [Tooltip("Multiplier applied to attackCooldown after this attack. " +
                 "1 = same cooldown, 2 = double, 0.5 = half.")]
        [Min(0f)] public float cooldownMultiplier = 1f;
    }

    [Header("Attack Variants (optional)")]
    [Tooltip("If populated, one variant is picked at random (weighted by " +
             "'weight') from the ones whose range matches the current distance " +
             "to the player. If empty, the base attack fields above are used.")]
    public System.Collections.Generic.List<AttackVariant> attackVariants =
        new System.Collections.Generic.List<AttackVariant>();

    AttackVariant currentAttack;

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

            float enableAt = CurrentEnableDelay();
            float disableAt = CurrentDisableDelay();
            float endAt = CurrentEndDelay();

            if (!hitboxEnabledByTimer && elapsed >= enableAt)
            {
                EnableHitbox();
                hitboxEnabledByTimer = true;
            }

            if (!hitboxDisabledByTimer && elapsed >= disableAt)
            {
                DisableHitbox();
                hitboxDisabledByTimer = true;
            }

            if (elapsed >= endAt)
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

        // Pick which attack variant to use (if any are configured). Null =
        // fall back to the base fields — legacy behaviour.
        currentAttack = PickAttackVariant();

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        attackStepDirection = dir.normalized;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        animator.SetBool(IsAttackingHash, true);
        animator.ResetTrigger(HurtTrigger);

        // Fire the variant's animator trigger (or the default one).
        string trigger = (currentAttack != null && !string.IsNullOrEmpty(currentAttack.animatorTrigger))
            ? currentAttack.animatorTrigger
            : "Attack";

        int triggerHash = Animator.StringToHash(trigger);
        animator.ResetTrigger(triggerHash);
        animator.SetTrigger(triggerHash);

        float cooldown = attackCooldown;
        if (currentAttack != null) cooldown *= currentAttack.cooldownMultiplier;
        nextAttackTime = Time.time + cooldown;
    }

    // ============================================================
    // ATTACK VARIANT SELECTION & TIMING HELPERS
    // ============================================================
    AttackVariant PickAttackVariant()
    {
        if (attackVariants == null || attackVariants.Count == 0) return null;
        if (player == null) return null;

        float dist = Vector3.Distance(transform.position, player.position);

        // Collect valid variants (in range + weight > 0).
        float totalWeight = 0f;
        int validCount = 0;
        for (int i = 0; i < attackVariants.Count; i++)
        {
            var v = attackVariants[i];
            if (v == null) continue;
            if (v.weight <= 0f) continue;
            if (dist < v.minDistance || dist > v.maxDistance) continue;
            totalWeight += v.weight;
            validCount++;
        }

        if (validCount == 0) return null;

        // Weighted random pick.
        float roll = Random.value * totalWeight;
        float acc = 0f;
        for (int i = 0; i < attackVariants.Count; i++)
        {
            var v = attackVariants[i];
            if (v == null) continue;
            if (v.weight <= 0f) continue;
            if (dist < v.minDistance || dist > v.maxDistance) continue;
            acc += v.weight;
            if (roll <= acc) return v;
        }

        return attackVariants[0]; // shouldn't reach, but safe fallback
    }

    float CurrentEnableDelay()
    {
        if (currentAttack != null && currentAttack.hitboxEnableDelay > 0f)
            return currentAttack.hitboxEnableDelay;
        return hitboxEnableDelay;
    }

    float CurrentDisableDelay()
    {
        if (currentAttack != null && currentAttack.hitboxDisableDelay > 0f)
            return currentAttack.hitboxDisableDelay;
        return hitboxDisableDelay;
    }

    float CurrentEndDelay()
    {
        if (currentAttack != null && currentAttack.attackEndDelay > 0f)
            return currentAttack.attackEndDelay;
        return attackEndDelay;
    }

    /// <summary>Damage the current attack should deal. -1 means "use MeleeHitbox
    /// baseDamage as-is". Called by EnableHitbox to override the hitbox damage
    /// for the duration of this attack.</summary>
    int CurrentDamageOverride()
    {
        if (currentAttack != null && currentAttack.damage >= 0)
            return currentAttack.damage;
        return -1;
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
        {
            hb.SetOwner(transform);

            // If the current attack variant specifies a damage override, apply it.
            int dmg = CurrentDamageOverride();
            if (dmg >= 0) hb.SetOverrideDamage(dmg);
            else hb.ClearOverrideDamage();
        }

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