using UnityEngine;

public class EnemyCombatController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;
    EnemyAI ai;
    EnemyReactionChart reactions;

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

    [Header("Dash Attack")]
    [Tooltip("Charge at the player instead of taking the short attack step. The idle sprite stays on; the motion and the dash effect carry the attack.")]
    public bool useDashAttack;

    public float dashDistance = 2.6f;
    public float dashDuration = 0.22f;

    [Tooltip("Closest the dash is allowed to finish from the player. The hitbox still connects inside this gap.")]
    public float dashMinDistance = 0.4f;

    public GameObject dashVfxPrefab;
    public float dashVfxScale = 0.55f;

    [Tooltip("How long the dash waits while the charge effect grows. The lunge and the hitbox start after this. 0 skips the wind-up.")]
    public float dashWindup;

    [Tooltip("Played on the body during the wind-up. It goes away when the dash starts.")]
    public GameObject dashChargeVfxPrefab;

    public float dashChargeVfxScale = 0.55f;

    [Tooltip("After the dash connects, hop back so the enemy does not stay on top of the player.")]
    public bool hopBackOnHit = true;
    public float hopBackDistance = 1.15f;
    public float hopBackDuration = 0.16f;

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
    bool orbitArmed;
    float orbitReadyAt;
    float attackStartedAt;
    bool isAttacking;
    bool hitboxEnabledByTimer;
    bool hitboxDisabledByTimer;

    bool attackStepActive;
    bool attackStepStarted;
    Vector3 attackStepDirection;
    float attackStepMoved;
    bool hoppingBack;
    float hopMoved;
    Vector3 hopDir;
    GameObject dashVfxInstance;
    GameObject dashChargeInstance;
    MaterialPropertyBlock chargeBlock;
    Color chargeTintBefore = Color.white;
    Color chargeFlashBefore = new Color(0f, 0f, 0f, 1f);
    bool chargeTinted;

    static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly Color DashChargeTint = new Color(0.35f, 0.62f, 1f, 1f);
    static readonly Color DashChargeFlash = new Color(0.15f, 0.4f, 1f, 1f);

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
        reactions = GetComponent<EnemyReactionChart>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        DisableHitbox();
    }

    void Update()
    {
        if (HitStopperManager.Frozen)
            return;

        if (!player)
            return;

        if (isAttacking)
        {
            TickDashCharge();
            UpdateAttackStepForward();
        }

        UpdateHopBack();

        if (isAttacking && useTimerBasedHitbox)
        {
            float elapsed = EnemyClock.time - attackStartedAt;

            float strikeAt = DashWindup();
            float enableAt = strikeAt + CurrentEnableDelay();
            float disableAt = strikeAt + CurrentDisableDelay();
            float endAt = strikeAt + CurrentEndDelay();

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

        if (isAttacking && EnemyClock.time - attackStartedAt > maxAttackDuration)
            EndAttack();

        if (ai != null && ai.isAlerting)
        {
            DisarmOrbitCountdown();
            return;
        }

        if (ai != null && ai.isDefending)
        {
            DisarmOrbitCountdown();
            return;
        }

        if (isAttacking)
            return;

        if (EnemyClock.time < hitStunUntil)
        {
            DisarmOrbitCountdown();
            return;
        }

        if (motor != null && motor.IsMovementLocked())
            return;

        if (ai != null && !ai.UsesMelee)
            return;

        float dist = Vector3.Distance(transform.position, player.position);
        float range = ai != null ? ai.MeleeRange(this) : attackDistance;
        if (ai != null)
            ai.TickCloseInMix(dist);

        if (dist > range)
        {
            DisarmOrbitCountdown();
            return;
        }

        if (ai != null && ai.ClosesIn)
        {
            // The mix already picked this attack. A Hold, Step Back, or Defend still waits.
            DisarmOrbitCountdown();
            if (reactions != null)
                reactions.Tick();
            if (!ai.CloseInChoseMelee)
                return;
            if (reactions != null && !reactions.AllowsAttack)
                return;
            if (EnemyClock.time < nextAttackTime)
                return;
            if (!EnemyAttackScheduler.Instance.TryReserve(this))
                return;
            StartAttack();
            ai.ConsumeCloseInChoice();
            return;
        }

        if (reactions != null)
        {
            reactions.Tick();
            if (reactions.CloseActive)
            {
                // Keep Distance: the close row owns this range. A matrix hold still waits.
                DisarmOrbitCountdown();
                if (!reactions.AllowsAttack || !reactions.WantsCloseMelee || EnemyClock.time < nextAttackTime)
                    return;
                if (!EnemyAttackScheduler.Instance.TryReserve(this))
                    return;
                StartAttack();
                return;
            }

            if (!reactions.AllowsAttack)
            {
                DisarmOrbitCountdown();
                return;
            }
        }

        // Countdown starts on the orbit, not while still walking in.
        if (ai != null && !ai.InOrbit(dist))
        {
            DisarmOrbitCountdown();
            return;
        }

        if (!orbitArmed)
        {
            orbitArmed = true;
            float fresh = EnemyClock.time + attackCooldown;
            orbitReadyAt = nextAttackTime > EnemyClock.time ? nextAttackTime : fresh;
        }

        if (EnemyClock.time < orbitReadyAt)
            return;

        if (!EnemyAttackScheduler.Instance.TryReserve(this))
            return;

        StartAttack();
    }

    void DisarmOrbitCountdown()
    {
        orbitArmed = false;
    }

    void StartAttack()
    {
        isAttacking = true;
        orbitArmed = false;
        attackStartedAt = EnemyClock.time;
        hitboxEnabledByTimer = false;
        hitboxDisabledByTimer = false;

        attackStepActive = false;
        attackStepStarted = false;
        attackStepMoved = 0f;
        hoppingBack = false;
        hopMoved = 0f;
        EndDashVfx();

        // Pick which attack variant to use (if any are configured). Null =
        // fall back to the base fields — legacy behaviour.
        currentAttack = PickAttackVariant();

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        attackStepDirection = dir.normalized;
        if (reactions != null && reactions.WantsCloseMelee && reactions.WantsCloseStepBack)
            attackStepDirection = -attackStepDirection;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        BeginDashCharge();

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
        nextAttackTime = EnemyClock.time + cooldown;
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

        float elapsed = EnemyClock.time - attackStartedAt;

        if (!attackStepStarted)
        {
            float startAt = useDashAttack ? Mathf.Max(DashWindup(), attackStepDelay) : attackStepDelay;
            if (elapsed < startAt)
            {
                AimDash();
                return;
            }

            attackStepStarted = true;
            attackStepActive = true;
            EndDashCharge();
        }

        if (!attackStepActive)
            return;

        float stepDistance = useDashAttack ? dashDistance : attackStepDistance;
        float stepDuration = useDashAttack ? dashDuration : attackStepDuration;

        if (stepDuration <= 0f || stepDistance <= 0f)
        {
            attackStepActive = false;
            return;
        }

        if (useDashAttack && dashVfxInstance == null)
            BeginDashVfx();
        else if (dashVfxInstance != null)
            dashVfxInstance.transform.position = DashVfxPosition();

        float remainingStep = stepDistance - attackStepMoved;

        if (remainingStep <= 0f)
        {
            attackStepActive = false;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        float stopGap = useDashAttack ? dashMinDistance : minDistanceToPlayerAfterStep;
        float allowedDistance = distToPlayer - stopGap;

        if (allowedDistance <= 0f)
        {
            attackStepActive = false;
            return;
        }

        float speed = stepDistance / stepDuration;
        float moveAmount = speed * EnemyClock.deltaTime;

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
        EndDashCharge();
        EndDashVfx();

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
        EndDashCharge();
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

    /// <summary>Called by MeleeHitbox when this enemy's swing connects with the player.</summary>
    public void NotifyPlayerHit()
    {
        if (!useDashAttack || !hopBackOnHit || hoppingBack)
            return;

        hoppingBack = true;
        attackStepActive = false;
        hopMoved = 0f;
        hopDir = -attackStepDirection;
        EndDashVfx();

        if (motor != null)
            motor.AddMovementLock(hopBackDuration + 0.05f);
    }

    void UpdateHopBack()
    {
        if (!hoppingBack)
            return;

        if (hopBackDuration <= 0f || hopBackDistance <= 0f || hopMoved >= hopBackDistance)
        {
            hoppingBack = false;
            return;
        }

        float speed = hopBackDistance / hopBackDuration;
        float moveAmount = Mathf.Min(speed * EnemyClock.deltaTime, hopBackDistance - hopMoved);

        if (motor != null && motor.controller != null)
            motor.controller.Move(hopDir * moveAmount);
        else
            transform.position += hopDir * moveAmount;

        hopMoved += moveAmount;
    }

    float DashWindup()
    {
        if (!useDashAttack)
            return 0f;
        return Mathf.Max(0f, dashWindup);
    }

    void AimDash()
    {
        if (player == null)
            return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;

        attackStepDirection = dir.normalized;
        if (motor != null)
            motor.RotateToward(attackStepDirection);
    }

    void BeginDashCharge()
    {
        EndDashCharge();
        if (DashWindup() <= 0f)
            return;

        SetChargeTint(true);
        if (dashChargeVfxPrefab == null)
            return;

        dashChargeInstance = Instantiate(dashChargeVfxPrefab, DashChargePosition(), Quaternion.identity);
        dashChargeInstance.transform.localScale = Vector3.one * DashChargeScale(0f);
        HitStopVfx.Arm(dashChargeInstance, DashWindup() + 0.2f);
    }

    void TickDashCharge()
    {
        if (dashChargeInstance == null)
            return;

        float elapsed = EnemyClock.time - attackStartedAt;
        if (elapsed >= DashWindup())
        {
            EndDashCharge();
            return;
        }

        dashChargeInstance.transform.position = DashChargePosition();
        dashChargeInstance.transform.localScale = Vector3.one * DashChargeScale(elapsed);
    }

    float DashChargeScale(float elapsed)
    {
        float duration = Mathf.Max(0.05f, DashWindup());
        float t = Mathf.Clamp01(elapsed / duration);
        float grown = Mathf.SmoothStep(0.35f, 1f, t);
        return Mathf.Max(0.05f, dashChargeVfxScale) * grown;
    }

    void EndDashCharge()
    {
        SetChargeTint(false);
        if (dashChargeInstance == null)
            return;

        Destroy(dashChargeInstance);
        dashChargeInstance = null;
    }

    void SetChargeTint(bool on)
    {
        SpriteRenderer sprite = BodySprite();
        if (sprite == null)
            return;

        if (chargeBlock == null)
            chargeBlock = new MaterialPropertyBlock();

        if (on)
        {
            if (!chargeTinted)
            {
                Material mat = sprite.sharedMaterial;
                if (mat != null && mat.HasProperty(TintColorId))
                    chargeTintBefore = mat.GetColor(TintColorId);
                if (mat != null && mat.HasProperty(FlashColorId))
                    chargeFlashBefore = mat.GetColor(FlashColorId);
            }

            sprite.GetPropertyBlock(chargeBlock);
            chargeBlock.SetColor(TintColorId, DashChargeTint);
            chargeBlock.SetColor(FlashColorId, DashChargeFlash);
            sprite.SetPropertyBlock(chargeBlock);
            chargeTinted = true;
            return;
        }

        if (!chargeTinted)
            return;

        sprite.GetPropertyBlock(chargeBlock);
        chargeBlock.SetColor(TintColorId, chargeTintBefore);
        chargeBlock.SetColor(FlashColorId, chargeFlashBefore);
        sprite.SetPropertyBlock(chargeBlock);
        chargeTinted = false;
    }

    SpriteRenderer BodySprite()
    {
        if (animator == null)
            return null;
        return animator.GetComponent<SpriteRenderer>();
    }

    void BeginDashVfx()
    {
        if (dashVfxPrefab == null)
            return;

        dashVfxInstance = Instantiate(dashVfxPrefab, DashVfxPosition(), Quaternion.identity);
        dashVfxInstance.transform.localScale = Vector3.one * Mathf.Max(0.05f, dashVfxScale);
    }

    Vector3 DashVfxPosition()
    {
        return transform.position + Vector3.up * 0.45f;
    }

    Vector3 DashChargePosition()
    {
        if (animator != null)
        {
            SpriteRenderer sprite = animator.GetComponent<SpriteRenderer>();
            if (sprite != null && sprite.sprite != null)
                return sprite.bounds.center;
        }

        return transform.position + Vector3.up * 0.57f;
    }

    void EndDashVfx()
    {
        if (dashVfxInstance == null)
            return;

        Destroy(dashVfxInstance, 0.55f);
        dashVfxInstance = null;
    }

    public void OnTakeDamage(Vector3 hitDir)
    {
        hitDir.y = 0f;

        hitStunUntil = EnemyClock.time + hitStunTime;
        nextAttackTime = Mathf.Max(nextAttackTime, EnemyClock.time + attackDelayAfterHit);

        attackStepActive = false;
        attackStepStarted = false;
        hoppingBack = false;
        EndDashCharge();
        EndDashVfx();

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