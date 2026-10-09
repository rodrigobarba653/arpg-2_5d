using System.Collections;
using UnityEngine;

/// <summary>
/// Shared brain for every enemy, including Soldier-Melee and Soldier-Defender.
/// Those two prefabs use this same script. The animator controller on the body
/// is what changes their clips. Turning Can Defend on is what makes an enemy
/// raise a guard: it shows Defend on the reaction chart, and a Defend roll
/// calls StartDefense. Soldier-Melee can defend if that toggle is switched on.
/// Long Range Attacker shows the shot and the directional muzzle. Melee Attack
/// chooses whether this enemy also swings: always, only up close, or never.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public EnemyMotor motor;
    public Transform player;
    Animator animator;

    EnemyCombatController melee;
    EnemyRangedCombatController ranged;
    EnemyPatrol patrol;
    EnemyReactionChart reactions;

    [Header("Distances")]
    [Tooltip("Notice range in the front half. The back half uses Detect Back Distance.")]
    public float detectDistance = 6f;

    [Tooltip("Notice range in the back half. Kept shorter than the front so the enemy is easier to approach from behind.")]
    public float detectBackDistance = 3f;

    [Tooltip("After the enemy has noticed the player, they keep chasing until the player is this far away.")]
    public float disengageDistance = 12f;

    public float stopDistance = 1.5f;

    [Tooltip("If true, this enemy takes a slot on a circle around the player " +
             "(shared with every other engaging enemy of the same target) " +
             "instead of pathing straight to the player's exact position. " +
             "Stops enemies from stacking on top of each other when several " +
             "are chasing at once. Disable for enemies that should always " +
             "approach head-on (e.g. a solo boss).")]
    public bool surroundPlayer = true;

    [Header("Speeds")]
    [Tooltip("Speed used while patrolling (no player in sight).")]
    public float patrolSpeed = 1.5f;

    [Tooltip("Speed used while chasing the player. 0 or less = use EnemyMotor.moveSpeed.")]
    public float chaseSpeed = 0f;

    [Tooltip("Fraction of chase speed when the enemy is not closing in. That covers circling a surround slot, the hold sidestep, and stepping away.")]
    [Range(0.15f, 1f)]
    public float strafeSpeedScale = 0.4f;

    public enum DefendCoverage
    {
        Front,
        AllDirections
    }

    [Header("Defense")]
    [Tooltip("Shows Defend on the reaction chart. A Defend roll raises the guard. Off, and that column is hidden and its points fold back into Hold.")]
    public bool canDefend = true;
    [Tooltip("Front blocks the half in front of the direction they set the guard. All Directions blocks a hit from anywhere.")]
    public DefendCoverage defendCoverage = DefendCoverage.Front;

    [Header("Defense State")]
    public bool isDefending;
    public float defendDuration = 1.5f;

    [Tooltip("How long the sprite jitters when a hit is blocked.")]
    public float blockShakeDuration = 0.22f;

    [Tooltip("How far the sprite jitters, in local units, when a hit is blocked.")]
    public float blockShakeDistance = 0.24f;

    public enum MeleeAttackMode
    {
        Yes,
        [InspectorName("Only When Close")]
        OnlyWhenClose,
        No
    }

    [Header("Melee Attack")]
    [Tooltip("Yes swings at the melee attack distance. Only When Close swings inside Melee Close Distance. No never swings.")]
    public MeleeAttackMode meleeAttack = MeleeAttackMode.Yes;

    [Tooltip("Swing range when Melee Attack is Only When Close.")]
    public float meleeCloseDistance = 1.5f;

    public enum LongRangeFireMode
    {
        [InspectorName("Timed")]
        Timed,
        [InspectorName("Timed+Reactive")]
        Reactive
    }

    [Header("Long Range")]
    [Tooltip("Shows the ranged attack and the directional shoot point. Mobile or Fixed stays on the motor.")]
    public bool longRangeAttacker;

    [Tooltip("Timed shoots on the cooldown and ignores the reaction chart. Timed+Reactive keeps that cooldown, and the chart allows or blocks the shot.")]
    public LongRangeFireMode longRangeFire = LongRangeFireMode.Timed;

    public bool ReactiveShooter => longRangeAttacker && longRangeFire == LongRangeFireMode.Reactive;

    public bool UsesMelee => meleeAttack != MeleeAttackMode.No;

    public float MeleeRange(EnemyCombatController combat)
    {
        if (meleeAttack == MeleeAttackMode.OnlyWhenClose)
            return meleeCloseDistance;

        return combat != null ? combat.attackDistance : meleeCloseDistance;
    }

    public bool MeleeOwnsDistance(float dist, EnemyCombatController combat)
    {
        return UsesMelee && dist <= MeleeRange(combat);
    }

    [Header("Alert (Spotted Player)")]
    [Tooltip("If true, the enemy freezes and shows alertIcon for alertDuration seconds the first time the player enters detectDistance.")]
    public bool useAlert = true;

    [Tooltip("Child GameObject (usually a '!' sprite) shown during the alert.")]
    public GameObject alertIcon;

    public float alertDuration = 1f;

    [Tooltip("Grace time after losing sight before another alert can fire (prevents flicker at the edge of detect range).")]
    public float reAlertGracePeriod = 0.5f;

    public bool isAlerting;
    float alertEndTime;
    float nextAlertAllowedTime;
    bool sawPlayerLastFrame;

    [Header("Combat State")]
    [Tooltip("Time after losing sight (and finishing any combat action) before the enemy " +
             "returns to non-combat anims (Idle / Move).")]
    public float combatExitDelay = 3f;

    [HideInInspector] public bool isInCombat;
    float exitCombatAt;

    float defendTimer;
    Vector3 defendFacing;
    Coroutine blockShakeRoutine;
    Vector3 blockShakeRestLocal;
    bool blockShakeHasRest;

    Transform lastRegisteredPlayer; // who we're currently registered with in EnemySurroundGroup

    bool chasing;
    bool strafing;
    float strafeSign = 1f;
    float strafePhase;
    float strafeFlipAt;
    float strafeStuck;

    public bool IsChasing => chasing;

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        // The alert marker has its own Animator. The body driver points at the
        // sprite that plays Idle, Walk, Attack, Hurt, and Defend.
        EnemyTopDownAnimDriver driver = GetComponentInChildren<EnemyTopDownAnimDriver>();
        animator = driver != null && driver.animator != null
            ? driver.animator
            : GetComponentInChildren<Animator>();

        melee = GetComponent<EnemyCombatController>();
        ranged = GetComponent<EnemyRangedCombatController>();
        patrol = GetComponent<EnemyPatrol>();
        reactions = GetComponent<EnemyReactionChart>();

        // Auto-find the player. Prefer PersistentPlayer.Instance (works even
        // if the player is disabled during a scene transition); fall back to
        // FindWithTag for legacy setups without PersistentPlayer.
        if (!player)
            player = ResolvePlayerTransform();

        PropagatePlayerToCombat();

        if (alertIcon != null)
            alertIcon.SetActive(false);
    }

    void PropagatePlayerToCombat()
    {
        // Share the resolved Player reference with the combat controllers so
        // the user doesn't have to assign it twice.
        if (player == null) return;

        if (melee != null && melee.player == null)
            melee.player = player;

        if (ranged != null && ranged.player == null)
            ranged.player = player;
    }

    static Transform ResolvePlayerTransform()
    {
        // Best: the persistent singleton — works on disabled GameObjects too
        // (the teleport flow disables the player briefly during scene changes).
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.transform;

        // Fallback: tag-based search (only returns active GameObjects).
        var pgo = GameObject.FindWithTag("Player");
        return pgo != null ? pgo.transform : null;
    }

    void Update()
    {
        if (HitStopperManager.Frozen)
            return;

        if (motor != null)
            motor.speedScale = 1f;

        // No player found yet → patrol if we have a route, otherwise stay idle.
        // (Doesn't return — keeps trying to re-find the player next frame.)
        if (!player)
        {
            player = ResolvePlayerTransform();

            if (player != null)
                PropagatePlayerToCombat();
        }

        if (!player)
        {
            if (patrol != null && patrol.HasRoute)
            {
                motor.activeSpeedOverride = patrolSpeed;
                patrol.Tick(transform, motor);
            }
            else
            {
                motor.Stop();
            }
            return;
        }

        if (lastRegisteredPlayer != null && lastRegisteredPlayer != player)
            EnemySurroundGroup.Unregister(lastRegisteredPlayer, this);

        float dist = FlatDistance(player.position, transform.position);
        bool seesPlayer = UpdateChase(player.position, dist);

        // ===== COMBAT STATE =====
        bool combatActionActive =
            isAlerting ||
            isDefending ||
            (melee != null && melee.IsAttacking()) ||
            (ranged != null && ranged.IsAttacking());

        if (seesPlayer || combatActionActive)
        {
            isInCombat = true;
            exitCombatAt = EnemyClock.time + combatExitDelay;
        }
        else if (isInCombat && EnemyClock.time >= exitCombatAt)
        {
            isInCombat = false;
        }

        // ===== ALERT (SPOTTING) =====
        bool justSpotted = useAlert && seesPlayer && !sawPlayerLastFrame;
        sawPlayerLastFrame = seesPlayer;

        if (justSpotted && !isAlerting && EnemyClock.time >= nextAlertAllowedTime)
            StartAlert();

        if (isAlerting)
        {
            motor.Stop();

            // Even while alerting, Fixed enemies snap to look at the player.
            if (motor != null && motor.moveType == EnemyMotor.EnemyMoveType.Fixed && seesPlayer)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.001f)
                    motor.RotateToward(toPlayer);
            }

            if (EnemyClock.time >= alertEndTime)
                EndAlert();

            return;
        }

        // ===== ATTACK LOCK =====
        // If a combat controller is mid-attack and has movement-lock enabled,
        // stop the motor and let the animation play out without overrides.
        bool meleeLocking = melee != null && melee.IsAttacking() && melee.LockMovementDuringAttack;
        bool rangedLocking = ranged != null && ranged.IsAttacking() && ranged.LockMovementDuringAttack;

        if (meleeLocking || rangedLocking)
        {
            motor.Stop();
            return;
        }

        // While the guard is up, hold still and do not roll again, including the
        // frame it ends. The chart samples again on the next frame.
        if (isDefending)
        {
            defendTimer -= EnemyClock.deltaTime;
            HoldDefendFacing();

            if (defendTimer <= 0f)
                EndDefense();

            return;
        }

        // Once per frame. Combat also calls Tick, and the chart ignores a second
        // call in the same frame, so script order does not change the roll.
        if (reactions != null)
            reactions.Tick();

        if (canDefend && reactions != null && reactions.WantsDefend)
        {
            StartDefense();
            return;
        }

        // ===== FIXED ENEMY =====
        // Fixed enemies never move, but they rotate to face the player so their
        // ranged attacks / defense / hit reactions align correctly.
        if (motor != null && motor.moveType == EnemyMotor.EnemyMoveType.Fixed)
        {
            motor.Stop();

            if (seesPlayer)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude > 0.001f)
                    motor.RotateToward(toPlayer);
            }

            return;
        }

        // ===== NORMAL AI (Mobile) =====

        if (!seesPlayer)
        {
            // Out of detect range → patrol if a route is set, otherwise stay idle.
            motor.activeSpeedOverride = patrolSpeed;

            if (lastRegisteredPlayer != null)
            {
                EnemySurroundGroup.Unregister(lastRegisteredPlayer, this);
                lastRegisteredPlayer = null;
            }

            if (patrol != null && patrol.HasRoute)
                patrol.Tick(transform, motor);
            else
                motor.Stop();
            return;
        }

        // Inside detect range → engaging. Tell patrol so it knows to restart later.
        if (patrol != null)
            patrol.OnPatrolPaused();

        if (surroundPlayer && lastRegisteredPlayer != player)
        {
            EnemySurroundGroup.Register(player, this);
            lastRegisteredPlayer = player;
        }

        // Apply chase speed (or fall back to motor.moveSpeed if not configured).
        if (chaseSpeed > 0f)
            motor.activeSpeedOverride = chaseSpeed;
        else
            motor.activeSpeedOverride = -1f;

        bool holding = reactions != null && (reactions.CloseActive ? reactions.WantsCloseHold : reactions.WantsHold);
        bool steppingBack = reactions != null && (reactions.CloseActive ? reactions.WantsCloseStepBack : reactions.WantsStepBack);

        if (holding)
        {
            if (!strafing)
            {
                strafing = true;
                strafeSign = Random.value < 0.5f ? -1f : 1f;
                strafePhase = Random.Range(0f, Mathf.PI * 2f);
                strafeFlipAt = 0f;
                strafeStuck = 0f;
            }

            LooseStrafe(player.position);
            motor.speedScale = StrafeScale(true);
            return;
        }

        strafing = false;

        if (steppingBack)
        {
            float backTo = stopDistance + reactions.StepBackDistance;
            if (dist >= backTo)
            {
                motor.Stop();
                if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
                    motor.agent.ResetPath();
                return;
            }

            Vector3 away = transform.position - player.position;
            away.y = 0f;
            motor.speedScale = StrafeScale(false);
            motor.SetMoveDirection(away);
            return;
        }

        if (dist <= stopDistance)
        {
            motor.Stop();

            // Stop the agent path so it doesn't try to drift forward at melee range.
            if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
                motor.agent.ResetPath();

            return;
        }

        // Move toward the player's surround slot (or their exact position if
        // surroundPlayer is off / we're the only one engaging). Use NavMeshAgent
        // for pathfinding if it's set up, otherwise fall back to straight-line
        // steering.
        Vector3 targetPos = player.position;
        if (surroundPlayer)
            targetPos += EnemySurroundGroup.GetSurroundOffset(player, this, stopDistance);

        Vector3 moveDir = targetPos - transform.position;
        moveDir.y = 0f;

        if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
        {
            motor.agent.SetDestination(targetPos);

            Vector3 desired = motor.agent.desiredVelocity;
            desired.y = 0f;

            if (desired.sqrMagnitude > 0.001f)
                moveDir = desired;
        }

        if (MoveIsStrafe(moveDir))
            motor.speedScale = StrafeScale(false);

        motor.SetMoveDirection(moveDir);
    }

    /// <summary>
    /// Closing in stays at chase speed. Sidesteps, backing away, and orbiting a slot do not.
    /// Hold can be slower still when the chart's hold fraction is below Strafe Speed.
    /// </summary>
    float StrafeScale(bool holding)
    {
        float scale = strafeSpeedScale;
        if (holding && reactions != null)
            scale = Mathf.Min(scale, reactions.HoldStrafeSpeed);
        return Mathf.Clamp(scale, 0.15f, 1f);
    }

    bool MoveIsStrafe(Vector3 moveDir)
    {
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude < 0.0001f || player == null)
            return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return false;

        return Vector3.Dot(moveDir.normalized, toPlayer.normalized) < 0.45f;
    }

    bool UpdateChase(Vector3 playerPos, float dist)
    {
        if (!chasing)
        {
            if (InsideNoticeShape(playerPos, dist))
                chasing = true;
        }
        else if (dist > disengageDistance)
        {
            chasing = false;
        }

        return chasing;
    }

    void LooseStrafe(Vector3 playerPos)
    {
        Vector3 toPlayer = playerPos - transform.position;
        toPlayer.y = 0f;
        float d = toPlayer.magnitude;
        Vector3 radial = d > 0.05f ? toPlayer / d : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, radial) * strafeSign;

        if (EnemyClock.time >= strafeFlipAt && ShouldFlipStrafe(side))
        {
            strafeSign = -strafeSign;
            strafeFlipAt = EnemyClock.time + 0.45f;
            strafeStuck = 0f;
            side = -side;
        }

        // Preferred distance slowly breathes, and the pull toward it is weak,
        // so the path wanders instead of riding stopDistance.
        float breathe = Mathf.Sin(EnemyClock.time * 0.45f + strafePhase);
        float preferred = stopDistance * Mathf.Lerp(0.75f, 1.6f, (breathe + 1f) * 0.5f);
        float inward = Mathf.Clamp((d - preferred) * 0.1f, -0.3f, 0.3f);
        Vector3 move = side + radial * inward;

        motor.SetMoveAndFacing(move, radial);

        if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
            motor.agent.ResetPath();
    }

    bool ShouldFlipStrafe(Vector3 side)
    {
        side.y = 0f;
        if (side.sqrMagnitude < 0.0001f)
            return false;
        side.Normalize();

        // Already grinding against something: turn around even if the probe misses.
        if (motor.GetSpeed() < 0.2f)
        {
            strafeStuck += EnemyClock.deltaTime;
            if (strafeStuck > 0.2f && !StrafeBlocked(-side))
                return true;
        }
        else
        {
            strafeStuck = 0f;
        }

        return StrafeBlocked(side) && !StrafeBlocked(-side);
    }

    bool StrafeBlocked(Vector3 dir)
    {
        float body = 0.35f;
        if (motor.controller != null)
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            body = motor.controller.radius * scale + 0.12f;
        }

        Vector3 origin = transform.position + Vector3.up * 0.45f + dir * body;
        int count = Physics.RaycastNonAlloc(origin, dir, StrafeHits, 0.55f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = StrafeHits[i].transform;
            if (hit == null || hit == transform || hit.IsChildOf(transform))
                continue;
            if (player != null && (hit == player || hit.IsChildOf(player)))
                continue;
            return true;
        }

        return false;
    }

    static readonly RaycastHit[] StrafeHits = new RaycastHit[8];

    bool InsideNoticeShape(Vector3 playerPos, float dist)
    {
        Vector3 to = playerPos - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f)
            return true;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;

        bool inFront = Vector3.Dot(fwd.normalized, to.normalized) >= 0f;
        float range = inFront ? detectDistance : detectBackDistance;
        return dist <= range;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnValidate()
    {
        detectDistance = Mathf.Max(0.1f, detectDistance);
        detectBackDistance = Mathf.Clamp(detectBackDistance, 0.1f, detectDistance);
        disengageDistance = Mathf.Max(detectDistance, disengageDistance);

        EnemyReactionChart chart = GetComponent<EnemyReactionChart>();
        if (chart != null)
            chart.RefreshShares();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.forward;
        fwd.Normalize();

        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.9f);
        DrawArc(origin, fwd, detectDistance, -90f, 90f);
        Gizmos.color = new Color(0.3f, 0.75f, 1f, 0.9f);
        DrawArc(origin, fwd, detectBackDistance, 90f, 270f);
        Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.35f);
        DrawArc(origin, fwd, Mathf.Max(detectDistance, disengageDistance), 0f, 360f);
    }

    static void DrawArc(Vector3 origin, Vector3 forward, float radius, float fromDeg, float toDeg)
    {
        const int seg = 24;
        float span = toDeg - fromDeg;
        Vector3 prev = origin + (Quaternion.AngleAxis(fromDeg, Vector3.up) * forward) * radius;
        for (int i = 1; i <= seg; i++)
        {
            float ang = fromDeg + span * (i / (float)seg);
            Vector3 next = origin + (Quaternion.AngleAxis(ang, Vector3.up) * forward) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    /// <summary>
    /// True when this guard absorbs a hit from sourceWorldPosition.
    /// MeleeHitbox and EnemyHealth both call this, so a block is decided in one place.
    /// Front uses the facing captured when this guard started, not the current sprite facing.
    /// </summary>
    public bool BlocksAttackFrom(Vector3 sourceWorldPosition)
    {
        if (!isDefending)
            return false;

        if (defendCoverage == DefendCoverage.AllDirections)
            return true;

        Vector3 fwd = defendFacing;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f)
            fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f)
            return true;

        Vector3 from = sourceWorldPosition - transform.position;
        from.y = 0f;
        if (from.sqrMagnitude < 0.001f)
            return true;

        return Vector3.Dot(fwd.normalized, from.normalized) > 0f;
    }

    public Vector3 DefendFacing => defendFacing.sqrMagnitude > 0.001f ? defendFacing : transform.forward;

    void OnDisable()
    {
        if (isDefending)
            EndDefense();

        StopBlockShake();

        chasing = false;
        if (lastRegisteredPlayer != null)
        {
            EnemySurroundGroup.Unregister(lastRegisteredPlayer, this);
            lastRegisteredPlayer = null;
        }
    }

    void OnDestroy()
    {
        if (lastRegisteredPlayer != null)
            EnemySurroundGroup.Unregister(lastRegisteredPlayer, this);
    }

    void StartAlert()
    {
        isAlerting = true;
        alertEndTime = EnemyClock.time + alertDuration;

        if (alertIcon != null)
            alertIcon.SetActive(true);
    }

    void EndAlert()
    {
        isAlerting = false;
        nextAlertAllowedTime = EnemyClock.time + reAlertGracePeriod;

        if (alertIcon != null)
            alertIcon.SetActive(false);
    }

    /// <summary>A hit was blocked. Jitters the sprite. Real damage does not call this.</summary>
    public void NotifyBlockedHit()
    {
        if (!isDefending || animator == null)
            return;

        if (blockShakeRoutine != null)
            StopCoroutine(blockShakeRoutine);

        blockShakeRoutine = StartCoroutine(BlockShake());
    }

    IEnumerator BlockShake()
    {
        Transform body = animator.transform;
        if (!blockShakeHasRest)
        {
            blockShakeRestLocal = body.localPosition;
            blockShakeHasRest = true;
        }

        float duration = Mathf.Max(0.01f, blockShakeDuration);
        float distance = Mathf.Max(0f, blockShakeDistance);
        float t = 0f;

        while (t < duration)
        {
            t += EnemyClock.deltaTime;
            float damp = 1f - Mathf.Clamp01(t / duration);
            Vector2 offset = Random.insideUnitCircle * distance * damp;
            body.localPosition = blockShakeRestLocal + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        body.localPosition = blockShakeRestLocal;
        blockShakeHasRest = false;
        blockShakeRoutine = null;
    }

    void StopBlockShake()
    {
        if (blockShakeRoutine != null)
        {
            StopCoroutine(blockShakeRoutine);
            blockShakeRoutine = null;
        }

        if (blockShakeHasRest && animator != null)
            animator.transform.localPosition = blockShakeRestLocal;

        blockShakeHasRest = false;
    }

    /// <summary>
    /// A hit that got through the guard ends it so the hurt reaction can play.
    /// The same swing does not raise the guard again.
    /// </summary>
    public void BreakDefense()
    {
        if (reactions != null)
            reactions.CancelReaction();

        if (isDefending)
            EndDefense();
    }

    void StartDefense()
    {
        isDefending = true;
        defendTimer = defendDuration;

        Vector3 facing = transform.forward;
        facing.y = 0f;
        defendFacing = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
        HoldDefendFacing();

        if (animator)
            animator.SetBool("isDefending", true);
    }

    void EndDefense()
    {
        isDefending = false;

        if (motor != null)
            motor.lockFacing = false;

        if (animator)
            animator.SetBool("isDefending", false);
    }

    /// <summary>
    /// Keeps the root yaw on the direction chosen for this guard.
    /// The sprite still billboards. DefendFacing only picks which defend pose plays.
    /// The next StartDefense captures a new direction.
    /// </summary>
    void HoldDefendFacing()
    {
        if (motor == null)
            return;

        motor.Stop();
        motor.lockFacing = true;
        motor.ForceFacing(defendFacing);
    }
}
