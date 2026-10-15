using UnityEngine;

public class EnemyRangedCombatController : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;
    EnemyAI ai;
    EnemyCombatController melee;
    EnemyReactionChart reactions;

    public Transform player;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform shootPoint;

    [Tooltip("Damage when the projectile is a player fireball (AxFireball). EnemyProjectile prefabs use their own damage.")]
    public int projectileDamage = 5;

    [Tooltip("World units per second for a fireball shot. 0 keeps the Speed on the fireball prefab. EnemyProjectile prefabs use their own speed.")]
    public float projectileSpeed = 12f;

    [Tooltip("Played at the shoot point when the projectile spawns. Cartoon FX Flash by default.")]
    public GameObject muzzleFlashPrefab;

    [Tooltip("World scale of the muzzle flash.")]
    public float muzzleFlashScale = 0.4f;

    [Tooltip("How much faster the flash plays. 1 is the prefab's own speed.")]
    public float muzzleFlashSpeed = 0.5f;

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

    [Header("Charge")]
    [Tooltip("Play a short muzzle charge during the shot wind-up so the player can see the shot coming. Off leaves other shooters unchanged.")]
    public bool chargeShot;

    [Tooltip("Looping effect at the muzzle while charging. Destroyed when the shot fires.")]
    public GameObject chargeVfxPrefab;

    [Tooltip("World scale of the charge at the moment the shot fires. It grows up to this size.")]
    public float chargeVfxScale = 0.22f;

    [Header("Beam")]
    [Tooltip("Fire a short laser along the aim instead of spawning a projectile. Uses the idle sprite; the beam is the attack.")]
    public bool fireBeam;

    public Color beamColor = new Color(1f, 0.35f, 0.28f, 1f);
    public float beamDuration = 0.14f;
    public float beamWidth = 0.08f;

    [Tooltip("Played where the beam ends.")]
    public GameObject beamImpactPrefab;
    public float beamImpactScale = 0.45f;

    [Header("Safety")]
    [Tooltip("If isAttacking persists longer than this, force EndAttack as a hard safety net.")]
    public float maxAttackDuration = 3f;

    float hitStunUntil;
    float nextAttackTime;
    float attackStartedAt;
    bool isAttacking;
    bool shotFired;
    Vector3 lockedAimDir;
    Vector3 lockedAimPoint;
    bool hasLockedAimPoint;
    GameObject chargeInstance;

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
        melee = GetComponent<EnemyCombatController>();
        reactions = GetComponent<EnemyReactionChart>();
    }

    void Update()
    {
        if (HitStopperManager.Frozen)
            return;

        if (!player)
            return;

        // ===== Timer-based firing =====
        if (isAttacking && useTimerBasedFiring)
        {
            float elapsed = EnemyClock.time - attackStartedAt;

            TickCharge(elapsed);

            if (!shotFired && elapsed >= shootDelay)
                Shoot();

            if (elapsed >= attackEndDelay)
                EndAttack();
        }

        // Safety: kill stuck attacks (timer + events both failed somehow).
        if (isAttacking && EnemyClock.time - attackStartedAt > maxAttackDuration)
            EndAttack();

        if (ai != null && ai.isAlerting)
            return;

        bool reactive = ai != null && ai.ReactiveShooter;

        if (reactive && ai.isDefending)
            return;

        if (isAttacking)
            return;

        if (EnemyClock.time < hitStunUntil)
            return;

        if (motor != null && motor.IsMovementLocked())
            return;

        if (ai != null && !ai.longRangeAttacker)
            return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > attackDistance)
            return;

        // A close swing owns this range, so a shooter does not fire and melee together.
        if (ai != null && ai.MeleeOwnsDistance(dist, melee))
            return;

        if (EnemyClock.time < nextAttackTime)
            return;

        if (reactive && reactions != null)
        {
            reactions.Tick();
            if (reactions.CloseActive)
                return;
            if (!reactions.AllowsRangedAttack)
                return;
        }

        if (!bypassAttackScheduler && !EnemyAttackScheduler.Instance.TryReserve(this))
            return;

        StartAttack();
    }

    void StartAttack()
    {
        isAttacking = true;
        attackStartedAt = EnemyClock.time;
        shotFired = false;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        // Lock aim when the wind-up starts. The beam fires at that spot, so the
        // player can step off it during the charge.
        lockedAimDir = Vector3.zero;
        hasLockedAimPoint = false;

        if (player != null)
        {
            lockedAimPoint = BodyAimPoint(player);
            hasLockedAimPoint = true;

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

        nextAttackTime = EnemyClock.time + attackCooldown;
        BeginCharge();
    }

    void BeginCharge()
    {
        EndCharge();
        if (!chargeShot || chargeVfxPrefab == null || shootPoint == null)
            return;

        chargeInstance = Instantiate(chargeVfxPrefab, shootPoint.position, Quaternion.identity);
        chargeInstance.transform.localScale = Vector3.one * ChargeScale(0f);
        HitStopVfx.Arm(chargeInstance, Mathf.Max(shootDelay, 0.1f) + 1f);
    }

    void TickCharge(float elapsed)
    {
        if (chargeInstance == null || shootPoint == null)
            return;

        chargeInstance.transform.position = shootPoint.position;
        chargeInstance.transform.localScale = Vector3.one * ChargeScale(elapsed);
    }

    float ChargeScale(float elapsed)
    {
        float duration = Mathf.Max(0.05f, shootDelay);
        float t = Mathf.Clamp01(elapsed / duration);
        float grown = Mathf.SmoothStep(0.35f, 1f, t);
        return Mathf.Max(0.05f, chargeVfxScale) * grown;
    }

    void EndCharge()
    {
        if (chargeInstance == null)
            return;

        Destroy(chargeInstance);
        chargeInstance = null;
    }

    // Can be triggered by timer (default) or animation event.
    public void Shoot()
    {
        if (shotFired) return;
        shotFired = true;
        EndCharge();

        if (!shootPoint)
            return;
        if (!fireBeam && !projectilePrefab)
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
        dir.Normalize();

        PlayMuzzleFlash(dir);

        if (fireBeam)
        {
            FireBeam(dir);
            return;
        }

        GameObject p = Instantiate(
            projectilePrefab,
            shootPoint.position,
            Quaternion.identity
        );

        EnemyProjectile proj = p.GetComponent<EnemyProjectile>();
        if (proj)
        {
            proj.Launch(dir);
            return;
        }

        ProjectileSpellEffect fireball = p.GetComponent<ProjectileSpellEffect>();
        if (fireball != null)
            fireball.LaunchFromEnemy(dir, projectileDamage, transform, projectileSpeed);
    }

    Vector3 BodyAimPoint(Transform target)
    {
        Vector3 point = target.position;
        CharacterController body = target.GetComponent<CharacterController>();
        if (body != null)
            point += body.center;
        else
            point += Vector3.up * 0.35f;
        return point;
    }

    void FireBeam(Vector3 fallbackDir)
    {
        Vector3 origin = shootPoint.position;
        Vector3 aim = hasLockedAimPoint ? lockedAimPoint : origin + fallbackDir * attackDistance;
        Vector3 toAim = aim - origin;
        float dist = toAim.magnitude;
        Vector3 dir = dist > 0.001f ? toAim / dist : fallbackDir;
        float range = Mathf.Max(dist, 0.5f);
        Vector3 end = origin + dir * range;

        // Aimed at the body point saved when the charge began. A player who
        // stepped off that line is missed; one who stayed there is hit.
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.2f, dir, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            PlayerHealth health = hitTransform.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                if (Party.Active != null && health != Party.Active)
                    continue;

                end = hits[i].point;
                health.TakeDamage(projectileDamage, dir);
                break;
            }

            if (!hits[i].collider.isTrigger)
            {
                end = hits[i].point;
                break;
            }
        }

        SpawnBeam(origin, end);

        if (beamImpactPrefab != null)
        {
            GameObject impact = Instantiate(beamImpactPrefab, end, Quaternion.LookRotation(dir));
            impact.transform.localScale = Vector3.one * Mathf.Max(0.05f, beamImpactScale);
            Destroy(impact, 1.5f);
        }
    }

    void SpawnBeam(Vector3 origin, Vector3 end)
    {
        var go = new GameObject("EnemyBeam");
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, origin);
        line.SetPosition(1, end);
        line.startWidth = beamWidth;
        line.endWidth = beamWidth * 0.35f;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            line.material = mat;
            Destroy(mat, beamDuration);
        }
        line.startColor = beamColor;
        line.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0.15f);
        Destroy(go, beamDuration);
    }

    void PlayMuzzleFlash(Vector3 dir)
    {
        if (muzzleFlashPrefab == null || shootPoint == null)
            return;

        Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        float scale = Mathf.Max(0.05f, muzzleFlashScale);
        GameObject flash = Instantiate(muzzleFlashPrefab, shootPoint.position, rot);
        flash.transform.localScale = Vector3.one * scale;

        float speed = Mathf.Max(0.1f, muzzleFlashSpeed);
        ParticleSystem[] systems = flash.GetComponentsInChildren<ParticleSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.simulationSpeed = speed;
        }

        Light[] lights = flash.GetComponentsInChildren<Light>();
        for (int i = 0; i < lights.Length; i++)
            lights[i].range *= scale;
    }

    // Can be triggered by timer (default) or animation event.
    public void EndAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        EndCharge();
        if (animator != null) animator.SetBool(IsAttackingHash, false);
        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    public void OnTakeDamage(Vector3 hitDir)
    {
        hitStunUntil = EnemyClock.time + hitStunTime;
        nextAttackTime = Mathf.Max(nextAttackTime, EnemyClock.time + attackDelayAfterHit);

        if (isAttacking)
        {
            isAttacking = false;
            EndCharge();
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
        EndCharge();
        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    void OnDestroy()
    {
        EnemyAttackScheduler.ReleaseIfExists(this);
    }
}
