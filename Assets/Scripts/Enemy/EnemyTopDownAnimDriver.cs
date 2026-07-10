using UnityEngine;

public class EnemyTopDownAnimDriver : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public EnemyMotor motor;
    public EnemyAI ai;

    [Header("Tuning")]
    public float moveDeadzone = 0.01f;

    float moveDeadzoneSqr;

    Vector3 lastMoveDir = Vector3.forward;

    static readonly int IsMovingHash   = Animator.StringToHash("IsMoving");
    static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");
    static readonly int MoveXHash      = Animator.StringToHash("MoveX");
    static readonly int MoveYHash      = Animator.StringToHash("MoveY");

    bool hasIsMoving;
    bool hasIsInCombat;
    bool hasMoveX;
    bool hasMoveY;

    void Awake()
    {
        if (!animator)
            animator = GetComponent<Animator>();

        if (!motor)
            motor = GetComponentInParent<EnemyMotor>();

        if (!ai)
            ai = GetComponentInParent<EnemyAI>();

        moveDeadzoneSqr = moveDeadzone * moveDeadzone;

        if (animator != null)
            CacheParameterPresence();
    }

    void CacheParameterPresence()
    {
        hasIsMoving = false;
        hasIsInCombat = false;
        hasMoveX = false;
        hasMoveY = false;

        var parms = animator.parameters;
        for (int i = 0; i < parms.Length; i++)
        {
            int h = parms[i].nameHash;
            if (h == IsMovingHash)   hasIsMoving = true;
            if (h == IsInCombatHash) hasIsInCombat = true;
            if (h == MoveXHash)      hasMoveX = true;
            if (h == MoveYHash)      hasMoveY = true;
        }

        if (!hasIsInCombat)
            Debug.LogWarning($"[EnemyTopDownAnimDriver] Animator on '{name}' is " +
                             "missing 'IsInCombat' (bool). Without it, the enemy " +
                             "will never transition to attack state and won't deal damage. " +
                             "Add it in the Animator Controller's Parameters tab.", this);

        if (!hasIsMoving)
            Debug.LogWarning($"[EnemyTopDownAnimDriver] Animator on '{name}' is missing 'IsMoving' (bool).", this);

        if (!hasMoveX)
            Debug.LogWarning($"[EnemyTopDownAnimDriver] Animator on '{name}' is missing 'MoveX' (float).", this);

        if (!hasMoveY)
            Debug.LogWarning($"[EnemyTopDownAnimDriver] Animator on '{name}' is missing 'MoveY' (float).", this);
    }

    void Update()
    {
        if (!animator || !motor)
            return;

        float speed = motor.GetSpeed();

        bool isMoving = speed > moveDeadzone;

        Vector3 dir = GetMoveDirection();

        if (dir.sqrMagnitude > 0.0001f)
            lastMoveDir = dir;

        // When moving, use velocity for instant directional response.
        // When idle, read transform.forward from the motor — for Mobile this
        // is the last facing the motor rotated to; for Fixed enemies this
        // tracks the player in real time (rotated by EnemyAI/motor.RotateToward).
        Vector3 finalDir;

        if (isMoving)
        {
            finalDir = dir;
        }
        else
        {
            Vector3 fwd = motor.transform.forward;
            fwd.y = 0f;

            if (fwd.sqrMagnitude > 0.0001f)
                finalDir = fwd.normalized;
            else
                finalDir = lastMoveDir;
        }

        Vector2 dir2D =
            new Vector2(finalDir.x, finalDir.z).normalized;

        if (hasIsMoving)   animator.SetBool(IsMovingHash, isMoving);
        if (hasIsInCombat) animator.SetBool(IsInCombatHash, ai != null && ai.isInCombat);
        if (hasMoveX)      animator.SetFloat(MoveXHash, dir2D.x);
        if (hasMoveY)      animator.SetFloat(MoveYHash, dir2D.y);
    }

    Vector3 GetMoveDirection()
    {
        Vector3 v = motor.controller.velocity;
        v.y = 0f;

        if (v.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return v.normalized;
    }
}