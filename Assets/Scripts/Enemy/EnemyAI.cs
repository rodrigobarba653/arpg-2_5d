using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public EnemyMotor motor;
    public Transform player;
    Animator animator;

    EnemyCombatController melee;
    EnemyRangedCombatController ranged;
    EnemyPatrol patrol;
    PlayerCombatController playerCombat;

    [Header("Distances")]
    public float detectDistance = 6f;
    public float stopDistance = 1.5f;

    [Header("Speeds")]
    [Tooltip("Speed used while patrolling (no player in sight).")]
    public float patrolSpeed = 1.5f;

    [Tooltip("Speed used while chasing the player. 0 or less = use EnemyMotor.moveSpeed.")]
    public float chaseSpeed = 0f;

    [Header("Defense")]
    public bool canDefend = true;
    [Range(0, 1)] public float defendChance = 0.3f;
    public float defendCooldown = 3f;
    [HideInInspector] public float lastDefendTime;

    [Header("Defense Range")]
    public float defendDistance = 2f;

    [Header("Defense State")]
    public bool isDefending;
    public float defendDuration = 1.5f;

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
    bool playerWasAttackingLastFrame;

    void Awake()
    {
        if (!motor)
            motor = GetComponent<EnemyMotor>();

        animator = GetComponentInChildren<Animator>();

        melee = GetComponent<EnemyCombatController>();
        ranged = GetComponent<EnemyRangedCombatController>();
        patrol = GetComponent<EnemyPatrol>();

        // Auto-find the player by tag if not assigned (prefab instances don't
        // know about the scene Player).
        if (!player)
        {
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }

        if (player)
            playerCombat = player.GetComponent<PlayerCombatController>();

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

    void Update()
    {
        // No player found yet → patrol if we have a route, otherwise stay idle.
        // (Doesn't return — keeps trying to re-find the player next frame.)
        if (!player)
        {
            var pgo = GameObject.FindWithTag("Player");
            if (pgo != null) player = pgo.transform;

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

        if (playerCombat == null)
            playerCombat = player.GetComponent<PlayerCombatController>();

        float dist = Vector3.Distance(transform.position, player.position);
        bool seesPlayer = dist <= detectDistance;

        // ===== COMBAT STATE =====
        bool combatActionActive =
            isAlerting ||
            isDefending ||
            (melee != null && melee.IsAttacking()) ||
            (ranged != null && ranged.IsAttacking());

        if (seesPlayer || combatActionActive)
        {
            isInCombat = true;
            exitCombatAt = Time.time + combatExitDelay;
        }
        else if (isInCombat && Time.time >= exitCombatAt)
        {
            isInCombat = false;
        }

        // ===== ALERT (SPOTTING) =====
        bool justSpotted = useAlert && seesPlayer && !sawPlayerLastFrame;
        sawPlayerLastFrame = seesPlayer;

        if (justSpotted && !isAlerting && Time.time >= nextAlertAllowedTime)
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

            if (Time.time >= alertEndTime)
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

        // ===== DEFENSE TIMER =====
        if (isDefending)
        {
            defendTimer -= Time.deltaTime;

            motor.Stop();

            if (defendTimer <= 0f)
            {
                EndDefense();
            }

            return;
        }

        // ===== REACTIVE DEFENSE =====
        // Roll once when the player transitions from idle to attacking.
        bool playerIsAttacking = playerCombat != null && playerCombat.IsAttacking();
        bool playerJustStartedAttack = playerIsAttacking && !playerWasAttackingLastFrame;
        playerWasAttackingLastFrame = playerIsAttacking;

        if (canDefend &&
            playerJustStartedAttack &&
            Time.time >= lastDefendTime + defendCooldown &&
            dist <= defendDistance &&
            dist > stopDistance * 0.8f)
        {
            if (Random.value < defendChance)
            {
                StartDefense();
                return;
            }
        }

        // ===== FIXED ENEMY =====
        // Fixed enemies never move, but they rotate to face the player so their
        // ranged attacks / defense / hit reactions align correctly.
        if (motor != null && motor.moveType == EnemyMotor.EnemyMoveType.Fixed)
        {
            motor.Stop();

            if (dist <= detectDistance)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude > 0.001f)
                    motor.RotateToward(toPlayer);
            }

            return;
        }

        // ===== NORMAL AI (Mobile) =====

        if (dist > detectDistance)
        {
            // Out of detect range → patrol if a route is set, otherwise stay idle.
            motor.activeSpeedOverride = patrolSpeed;

            if (patrol != null && patrol.HasRoute)
                patrol.Tick(transform, motor);
            else
                motor.Stop();
            return;
        }

        // Inside detect range → engaging. Tell patrol so it knows to restart later.
        if (patrol != null)
            patrol.OnPatrolPaused();

        // Apply chase speed (or fall back to motor.moveSpeed if not configured).
        if (chaseSpeed > 0f)
            motor.activeSpeedOverride = chaseSpeed;

        if (dist <= stopDistance)
        {
            motor.Stop();

            // Stop the agent path so it doesn't try to drift forward at melee range.
            if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
                motor.agent.ResetPath();

            return;
        }

        // Move toward the player. Use NavMeshAgent for pathfinding if it's set up,
        // otherwise fall back to straight-line steering.
        Vector3 moveDir = player.position - transform.position;
        moveDir.y = 0f;

        if (motor.agent != null && motor.agent.enabled && motor.agent.isOnNavMesh)
        {
            motor.agent.SetDestination(player.position);

            Vector3 desired = motor.agent.desiredVelocity;
            desired.y = 0f;

            if (desired.sqrMagnitude > 0.001f)
                moveDir = desired;
        }

        motor.SetMoveDirection(moveDir);
    }

    void StartAlert()
    {
        isAlerting = true;
        alertEndTime = Time.time + alertDuration;

        if (alertIcon != null)
            alertIcon.SetActive(true);
    }

    void EndAlert()
    {
        isAlerting = false;
        nextAlertAllowedTime = Time.time + reAlertGracePeriod;

        if (alertIcon != null)
            alertIcon.SetActive(false);
    }

    void StartDefense()
    {
        isDefending = true;
        defendTimer = defendDuration;
        lastDefendTime = Time.time;

        if (animator)
            animator.SetBool("isDefending", true);
    }

    void EndDefense()
    {
        isDefending = false;

        if (animator)
            animator.SetBool("isDefending", false);
    }
}
