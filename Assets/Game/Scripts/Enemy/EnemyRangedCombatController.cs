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

    [Tooltip("How far from the beam a body still gets hit. The line stays drawn for the whole shot.")]
    public float beamHitRadius = 0.4f;

    [Tooltip("Played where the beam ends.")]
    public GameObject beamImpactPrefab;
    public float beamImpactScale = 0.45f;

    [Header("Aim Marker")]
    [Tooltip("Circle on the ground around the player during the wind-up. It follows them and shrinks, then freezes. Other shooters leave this off.")]
    public bool showAimMarker;

    [Tooltip("Seconds before the shot when the circle stops following. Movement after this is what leaves the shot.")]
    public float aimLockLead = 0.04f;

    [Tooltip("Circle size at the start of the wind-up.")]
    public float aimMarkerStartRadius = 1.35f;

    [Tooltip("Circle size when it locks. Sized so standing or walking stays inside, and a roll leaves it.")]
    public float aimMarkerRadius = 0.5f;

    [Tooltip("Height of the ring above the player's feet.")]
    public float aimMarkerHeight = 0.06f;

    [Header("Safety")]
    [Tooltip("If isAttacking persists longer than this, force EndAttack as a hard safety net.")]
    public float maxAttackDuration = 3f;

    float hitStunUntil;
    float nextAttackTime;
    bool orbitArmed;
    float orbitReadyAt;
    float attackStartedAt;
    bool isAttacking;
    bool shotFired;
    Vector3 lockedAimDir;
    Vector3 lockedAimPoint;
    Vector3 lockedGroundPoint;
    Vector3 lockedBodyPoint;
    bool hasLockedAimPoint;
    bool aimFrozen;
    GameObject chargeInstance;
    LineRenderer aimRing;

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
            TickAim(elapsed);

            if (!shotFired && elapsed >= shootDelay)
                Shoot();

            if (elapsed >= attackEndDelay)
                EndAttack();
        }

        // Safety: kill stuck attacks (timer + events both failed somehow).
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

        if (ai != null && !ai.longRangeAttacker)
            return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (ai != null)
            ai.TickCloseInMix(dist);

        if (dist > attackDistance)
        {
            DisarmOrbitCountdown();
            return;
        }

        bool insidePunch = ai != null && ai.MeleeOwnsDistance(dist, melee);
        bool closesIn = ai != null && ai.ClosesIn;

        if (reactions != null)
            reactions.Tick();

        bool reactionShot = reactions != null && reactions.WantsShoot;
        bool closeInShot = closesIn && insidePunch && (ai.CloseInChoseShoot || reactionShot);

        if (reactions != null)
        {
            bool closeOwnsGun = reactions.CloseActive && !reactions.WantsCloseShoot;
            if (!reactions.AllowsRangedAttack || closeOwnsGun)
            {
                // A pending melee roll is not a shot, so a Hold must not wipe the gun timer.
                if (!closesIn || !insidePunch || closeInShot)
                    DisarmOrbitCountdown();
                return;
            }
        }

        // Keep Distance stays quiet inside the punch unless the close row rolled Shoot.
        // Close In shoots in here only when this roll is a shot, or the chart rolled Shoot.
        if (insidePunch && !closeInShot && (reactions == null || !reactions.WantsCloseShoot))
        {
            if (!closesIn)
                DisarmOrbitCountdown();
            return;
        }

        bool mobile = motor != null && motor.moveType != EnemyMotor.EnemyMoveType.Fixed;
        bool holdFireUntilOrbit = mobile && (ai == null || !ai.ClosesIn);
        if (holdFireUntilOrbit && ai != null && !ai.InOrbit(dist))
        {
            DisarmOrbitCountdown();
            return;
        }

        if (mobile)
        {
            if (!orbitArmed)
            {
                orbitArmed = true;
                float fresh = EnemyClock.time + attackCooldown;
                orbitReadyAt = nextAttackTime > EnemyClock.time ? nextAttackTime : fresh;
            }

            if (EnemyClock.time < orbitReadyAt)
                return;
        }
        else if (EnemyClock.time < nextAttackTime)
        {
            return;
        }

        if (!bypassAttackScheduler && !EnemyAttackScheduler.Instance.TryReserve(this))
            return;

        StartAttack();
        if (closesIn && insidePunch)
            ai.ConsumeCloseInChoice();
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
        shotFired = false;

        if (motor != null && lockMovementDuringAttack)
            motor.Stop();

        // Without a marker the aim locks now, for the whole wind-up.
        // With a marker it keeps following until aimLockLead seconds remain.
        lockedAimDir = Vector3.zero;
        hasLockedAimPoint = false;
        aimFrozen = !showAimMarker;

        if (player != null)
        {
            if (showAimMarker)
                CaptureTrackedAim(player);
            else
                LockBodyAim(player);
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
        EndAimMarker();

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

    void TickAim(float elapsed)
    {
        if (!showAimMarker || shotFired)
            return;

        float lead = Mathf.Clamp(aimLockLead, 0f, Mathf.Max(0f, shootDelay));
        float lockAt = shootDelay - lead;

        if (!aimFrozen && player != null)
            CaptureTrackedAim(player);

        if (!aimFrozen && elapsed >= lockAt)
            aimFrozen = true;

        float shrinkTime = Mathf.Max(0.05f, lockAt);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / shrinkTime));
        float radius = Mathf.Lerp(aimMarkerStartRadius, aimMarkerRadius, t);
        DrawAimRing(radius);
    }

    void LockBodyAim(Transform target)
    {
        lockedAimPoint = BodyAimPoint(target);
        hasLockedAimPoint = true;
        FaceTarget(target.position);
    }

    void CaptureTrackedAim(Transform target)
    {
        lockedGroundPoint = target.position + Vector3.up * aimMarkerHeight;
        lockedBodyPoint = BodyAimPoint(target);
        lockedAimPoint = lockedBodyPoint;
        hasLockedAimPoint = true;
        FaceTarget(target.position);
    }

    void FaceTarget(Vector3 worldPoint)
    {
        Vector3 toPlayer = worldPoint - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f)
            return;

        lockedAimDir = toPlayer.normalized;
        if (motor != null)
            motor.FaceDirection(lockedAimDir);
    }

    void DrawAimRing(float radius)
    {
        if (aimRing == null)
        {
            var go = new GameObject("EnemyAimRing");
            aimRing = go.AddComponent<LineRenderer>();
            aimRing.loop = true;
            aimRing.positionCount = 40;
            aimRing.useWorldSpace = true;
            aimRing.widthMultiplier = 0.07f;
            aimRing.numCornerVertices = 2;
            aimRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aimRing.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                aimRing.material = mat;
            }

            Color color = beamColor;
            color.a = 0.95f;
            aimRing.startColor = color;
            aimRing.endColor = color;
        }

        int count = aimRing.positionCount;
        float reach = Mathf.Max(0.05f, radius);
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 point = lockedGroundPoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
            aimRing.SetPosition(i, point);
        }
    }

    void EndAimMarker()
    {
        if (aimRing == null)
            return;

        if (aimRing.material != null)
            Destroy(aimRing.material);
        Destroy(aimRing.gameObject);
        aimRing = null;
    }

    bool PlayerCrossesBeam(Vector3 origin, Vector3 end)
    {
        if (player == null)
            return false;

        Vector3 body = BodyAimPoint(player);
        Vector3 segment = end - origin;
        float lengthSq = segment.sqrMagnitude;
        if (lengthSq < 0.0001f)
            return false;

        float t = Mathf.Clamp01(Vector3.Dot(body - origin, segment) / lengthSq);
        Vector3 closest = origin + segment * t;
        float bodyRadius = 0.25f;
        CharacterController bodyCollider = player.GetComponent<CharacterController>();
        if (bodyCollider == null)
            bodyCollider = player.GetComponentInChildren<CharacterController>();
        if (bodyCollider != null)
            bodyRadius = bodyCollider.radius;

        return Vector3.Distance(body, closest) <= Mathf.Max(0.05f, beamHitRadius) + bodyRadius;
    }

    void DamageMarkedPlayer(Vector3 dir, PlayerHealth alreadyFound)
    {
        PlayerHealth marked = alreadyFound;
        if (marked == null && player != null)
            marked = player.GetComponentInParent<PlayerHealth>();
        if (marked == null && player != null)
            marked = player.GetComponentInChildren<PlayerHealth>();
        if (marked == null)
            return;
        if (Party.Active != null && marked != Party.Active)
            return;

        marked.TakeDamage(projectileDamage, dir);
    }

    bool PlayerInAimMarker()
    {
        if (player == null)
            return false;

        Vector3 from = player.position;
        Vector3 to = lockedGroundPoint;
        from.y = 0f;
        to.y = 0f;
        return Vector3.Distance(from, to) <= Mathf.Max(0.05f, aimMarkerRadius);
    }

    bool WallBlocks(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 to = targetPoint - origin;
        float dist = to.magnitude;
        if (dist < 0.05f)
            return false;

        Vector3 dir = to / dist;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.2f, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;
            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
                continue;
            if (hitTransform.GetComponentInParent<PlayerHealth>() != null)
                continue;
            // Enemy shots pass through other enemies; only environment blocks.
            if (IsOtherEnemyCollider(hitTransform))
                continue;
            if (hits[i].collider.isTrigger)
                continue;

            // The ring sits on the floor. That floor is not a wall between the shot and the player.
            if (hits[i].normal.y > 0.45f)
                continue;

            return true;
        }

        return false;
    }

    static bool IsOtherEnemyCollider(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        if (hitTransform.GetComponentInParent<EnemyHealth>() != null)
            return true;

        if (hitTransform.GetComponentInParent<EnemyMotor>() != null)
            return true;

        Transform root = hitTransform.root;
        return root != null && root.CompareTag("Enemy");
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
        if (showAimMarker && lockedBodyPoint.sqrMagnitude > 0.001f)
            aim = lockedBodyPoint;

        Vector3 toAim = aim - origin;
        float dist = toAim.magnitude;
        Vector3 dir = dist > 0.001f ? toAim / dist : fallbackDir;
        float range = Mathf.Max(dist, 0.5f);
        Vector3 end = origin + dir * range;

        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.2f, dir, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        PlayerHealth beamVictim = null;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            // Friendly fire off: beams pass through other enemies.
            if (IsOtherEnemyCollider(hitTransform))
                continue;

            PlayerHealth health = hitTransform.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                if (Party.Active != null && health != Party.Active)
                    continue;

                if (showAimMarker)
                {
                    beamVictim = health;
                    continue;
                }

                end = hits[i].point;
                health.TakeDamage(projectileDamage, dir);
                break;
            }

            if (hits[i].collider.isTrigger || hits[i].normal.y > 0.45f)
                continue;

            end = hits[i].point;
            break;
        }

        if (showAimMarker)
        {
            bool inMarker = PlayerInAimMarker() && player != null && !WallBlocks(origin, BodyAimPoint(player));
            bool onBeam = beamVictim == null && PlayerCrossesBeam(origin, end);
            if (inMarker || beamVictim != null || onBeam)
                DamageMarkedPlayer(dir, beamVictim);
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
        line.endWidth = beamWidth;
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
        EndAimMarker();
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
            EndAimMarker();
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
        EndAimMarker();
        EnemyAttackScheduler.ReleaseIfExists(this);
    }

    void OnDestroy()
    {
        EnemyAttackScheduler.ReleaseIfExists(this);
    }
}
