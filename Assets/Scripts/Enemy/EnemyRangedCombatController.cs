using UnityEngine;

public class EnemyRangedCombatController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;
    EnemyAI ai;

    public Transform player;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform shootPoint;

    [Header("Attack")]
    public float attackDistance = 6f;
    public float attackCooldown = 2f;

    [Tooltip("If true, the enemy stops moving while shooting. Disable to allow kiting/strafing shots.")]
    public bool lockMovementDuringAttack = true;

    [Tooltip("If true this enemy ignores the global EnemyAttackScheduler. " +
             "Useful for Fixed turrets that should fire independently of melee enemies.")]
    public bool bypassAttackScheduler = false;

    [Header("Damage Reaction")]
    public float hitStunTime = 0.3f;

    [Tooltip("Extra time before the enemy can shoot again after being hit.")]
    public float attackDelayAfterHit = 0.45f;

    [Header("Timer-based Firing (no anim events needed)")]
    [Tooltip("If true, Shoot() and EndAttack() fire automatically based on timers " +
             "below. Animation events are still supported (idempotent), but not required.")]
    public bool useTimerBasedFiring = true;

    [Tooltip("Seconds from StartAttack to the projectile spawn (Shoot).")]
    public float shootDelay = 0.3f;

    [Tooltip("Seconds from StartAttack to the end of the attack (EndAttack). Must be >= shootDelay.")]
    public float attackEndDelay = 0.6f;

    [Header("Safety")]
    [Tooltip("If isAttacking persists longer than this, force EndAttack as a hard safety net.")]
    public float maxAttackDuration = 3f;

    float hitStunUntil;
    float nextAttackTime;
    float attackStartedAt;
    bool isAttacking;
    bool shotFired;
    Vector3 lockedAimDir;

    static readonly int AttackTrigger = Animator.StringToHash("RangeAttack");
    static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    static readonly int HurtTrigger = Animator.StringToHash("Hurt");

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        ai = GetComponent<EnemyAI>();
    }

    void Update()
    {
        if (!player)
            return;

        // ===== Timer-based firing =====
        if (isAttacking && useTimerBasedFiring)
        {
            float elapsed = Time.time - attackStartedAt;

            if (!shotFired && elapsed >= shootDelay)
                Shoot();

            if (elapsed >= attackEndDelay)
                EndAttack();
        }

        // Safety: kill stuck attacks (timer + events both failed somehow).
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

        if (!bypassAttackScheduler && !EnemyAttackScheduler.Instance.TryReserve(this))
            return;

        StartAttack();
    }

    void StartAttack()
    {
        isAttacking = true;
        attackStartedAt = Time.time;
        shotFired = false;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        // Lock aim direction at the moment of attack commit.
        // The projectile in Shoot() will use this direction regardless of where
        // the player moves during the wind-up — keeps anim and trajectory aligned.
        lockedAimDir = Vector3.zero;

        if (player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                lockedAimDir = toPlayer.normalized;

                if (motor != null)
                    motor.FaceDirection(lockedAimDir);
            }
        }

        animator.SetBool(IsAttackingHash, true);
        animator.ResetTrigger(HurtTrigger);
        animator.ResetTrigger(AttackTrigger);
        animator.SetTrigger(AttackTrigger);

        nextAttackTime = Time.time + attackCooldown;
    }

    // Can be triggered by timer (default) or animation event.
    public void Shoot()
    {
        if (shotFired) return;
        shotFired = true;

        if (!projectilePrefab || !shootPoint)
            return;

        // Prefer the direction locked at attack start (so the shot matches the
        // wind-up animation). Fall back to current player position if missing.
        Vector3 dir = lockedAimDir;

        if (dir.sqrMagnitude < 0.001f && player != null)
        {
            dir = player.position - shootPoint.position;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        dir.y = 0f;

        GameObject p = Instantiate(
            projectilePrefab,
            shootPoint.position,
            Quaternion.identity
        );

        EnemyProjectile proj = p.GetComponent<EnemyProjectile>();

        if (proj)
            proj.Launch(dir);
    }

    // Can be triggered by timer (default) or animation event.
    public void EndAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        if (animator != null) animator.SetBool(IsAttackingHash, false);
        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    public void OnTakeDamage(Vector3 hitDir)
    {
        hitStunUntil = Time.time + hitStunTime;
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + attackDelayAfterHit);

        if (isAttacking)
        {
            isAttacking = false;
            if (animator != null)
            {
                animator.SetBool(IsAttackingHash, false);
                animator.ResetTrigger(AttackTrigger);
            }
            EnemyAttackScheduler.ReleaseIfExists(this);
        }
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
}
