using UnityEngine;

/// <summary>
/// Automatic soft targeting / melee magnetism for beat-'em-up style combos.
/// Acquires a single ComboTarget from player intent, then steers each attack's
/// EXISTING lunge toward that enemy. Attack 1 stays inside a steering cone.
/// Once a swing actually connects, later combo steps (any step, including ones
/// added later) aim straight at that enemy and close the gap so the combo can latch.
/// Does not add a second magnet force and does not own knockback.
/// </summary>
[DisallowMultipleComponent]
public class MeleeSoftTargeting : MonoBehaviour
{
    [Header("Enable")]
    [SerializeField] bool enableSoftTargeting = true;

    [Header("Acquisition")]
    [Tooltip("Max distance to consider an enemy when starting a combo.")]
    [SerializeField] float acquisitionRadius = 4.5f;

    [Tooltip("Half-angle of the forward cone (degrees) relative to attack intent.")]
    [Range(1f, 180f)]
    [SerializeField] float acquisitionHalfAngle = 55f;

    [Tooltip("Score weight for facing alignment. Higher = prefer enemies in front.")]
    [SerializeField] float weightAngle = 1.35f;

    [Tooltip("Score weight for distance. Higher = prefer closer enemies.")]
    [SerializeField] float weightDistance = 0.85f;

    [Header("Steering (into existing lunge)")]
    [Tooltip("Max degrees the Attack1 lunge may deviate from intent toward the target.")]
    [SerializeField] float maxSteerAngleStep1 = 28f;

    [SerializeField] float maxSteerAngleStep2 = 42f;
    [SerializeField] float maxSteerAngleStep3 = 55f;

    [Header("Lunge Distance Correction")]
    [Tooltip("Extra gap beyond hitbox reach + enemy body radius. " +
             "Desired center spacing = hitboxReach + enemyRadius + this - closeIn.")]
    [SerializeField] float attackSpacingPadding = 0.1f;

    [Tooltip("Meters to step inside the point where the hitbox would just touch the hurt volume. " +
             "The hurtbox is larger than the sprite, so 0 leaves a visible gap.")]
    [SerializeField] float closeIn = 0.9f;

    [Tooltip("Closest center-to-center distance a lunge will aim for.")]
    [SerializeField] float minCenterSpacing = 0.7f;

    [Tooltip("Used when the ComboTarget has no CharacterController radius.")]
    [SerializeField] float fallbackEnemyRadius = 0.4f;

    [Tooltip("Minimum lunge scale when a soft target exists. 0 = allow no positional lunge " +
             "if already in attack range. Normal attacks without a target always use scale 1.")]
    [Range(0f, 1f)]
    [SerializeField] float minLungeScale = 0f;

    [Tooltip("Maximum lunge scale to close a gap (relative to the attack's normal lunge).")]
    [SerializeField] float maxLungeScale = 1.4f;

    [Tooltip("Extra max-scale allowance per combo step after the first. Ignored once latched.")]
    [SerializeField] float comboStepDistanceBoost = 0.12f;

    [Header("Latch (after a connecting hit)")]
    [Tooltip("Once a swing damages an enemy, later steps of this combo aim straight at that enemy.")]
    [SerializeField] bool latchAfterHit = true;

    [Tooltip("Max lunge scale used to close the remaining gap after a hit. " +
             "Higher than the normal cap so a short follow-up step can still reach attack spacing.")]
    [SerializeField] float latchMaxLungeScale = 8f;

    [Header("Combo Target Retention")]
    [Tooltip("Drop ComboTarget if farther than this from the player.")]
    [SerializeField] float retentionRadius = 6.5f;

    [Tooltip("Drop ComboTarget if outside this half-angle from current attack intent.")]
    [Range(1f, 180f)]
    [SerializeField] float retentionHalfAngle = 100f;

    [Header("Debug")]
    [SerializeField] bool drawGizmosWhenSelected = true;

    PlayerMotor motor;
    PlayerCombatController combat;

    EnemyHealth comboTarget;
    bool latched;

    // Debug / last resolve snapshot
    Vector3 debugIntentWorld = Vector3.forward;
    Vector3 debugAimWorld = Vector3.forward;
    float debugAcquisitionHalfAngle;
    float debugRetentionHalfAngle;
    float debugAttackSpacing;
    float debugDesiredTravel;
    bool debugHasTarget;

    public EnemyHealth ComboTarget => comboTarget;
    public bool HasComboTarget => IsUsable(comboTarget);
    public bool IsLatched => latched && IsUsable(comboTarget);
    public bool Enabled => enableSoftTargeting;

    public struct SoftAim
    {
        public Vector2 facing2D;
        public float speedScale;
        public bool steered;
    }

    void Awake()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!combat) combat = GetComponent<PlayerCombatController>();
    }

    public void ClearComboTarget()
    {
        comboTarget = null;
        latched = false;
        debugHasTarget = false;
    }

    /// <summary>
    /// A swing damaged this enemy. The first connecting hit of the combo locks
    /// that enemy; every later step moves directly onto them.
    /// </summary>
    public void NotifyConnectedHit(EnemyHealth enemy)
    {
        if (!enableSoftTargeting || !latchAfterHit || !IsUsable(enemy))
            return;

        if (latched && IsUsable(comboTarget))
            return;

        comboTarget = enemy;
        latched = true;
        debugHasTarget = true;
    }

    /// <summary>
    /// Attack1: acquire ComboTarget from intent, return facing for LockFacing + lunge.
    /// </summary>
    public SoftAim BeginCombo(Vector2 intentFacing2D, float baseSpeed, float baseDuration, float easeOutPower)
    {
        ClearComboTarget();

        if (!enableSoftTargeting || motor == null)
            return Passthrough(intentFacing2D);

        Vector3 intentWorld = motor.FacingToWorld(intentFacing2D);
        debugIntentWorld = intentWorld;
        debugAcquisitionHalfAngle = acquisitionHalfAngle;
        debugRetentionHalfAngle = retentionHalfAngle;

        EnemyHealth best = FindBestAcquisitionTarget(intentWorld);
        if (best == null)
            return Passthrough(intentFacing2D);

        comboTarget = best;
        return BuildAim(intentFacing2D, intentWorld, 1, baseSpeed, baseDuration, easeOutPower);
    }

    /// <summary>
    /// Later combo steps: keep ComboTarget if valid; steer existing lunge toward it.
    /// </summary>
    public SoftAim ResolveStep(Vector2 intentFacing2D, int comboStep,
        float baseSpeed, float baseDuration, float easeOutPower)
    {
        if (!enableSoftTargeting || motor == null)
            return Passthrough(intentFacing2D);

        Vector3 intentWorld = motor.FacingToWorld(intentFacing2D);
        debugIntentWorld = intentWorld;
        debugAcquisitionHalfAngle = acquisitionHalfAngle;
        debugRetentionHalfAngle = retentionHalfAngle;

        if (!HasLockedTarget(intentWorld))
        {
            bool wasLatched = latched;
            ClearComboTarget();

            // A dropped latch (dead or out of range) does not pick a new victim.
            // Otherwise every step can still acquire, including attacks added later.
            if (!wasLatched)
            {
                EnemyHealth best = FindBestAcquisitionTarget(intentWorld);
                if (best != null)
                    comboTarget = best;
            }
        }

        if (!IsUsable(comboTarget))
            return Passthrough(intentFacing2D);

        return BuildAim(intentFacing2D, intentWorld, comboStep, baseSpeed, baseDuration, easeOutPower);
    }

    bool HasLockedTarget(Vector3 intentWorld)
    {
        if (!IsUsable(comboTarget))
            return false;

        if (latched)
            return FlatDistance(comboTarget) <= retentionRadius;

        return IsWithinRetention(comboTarget, intentWorld);
    }

    float FlatDistance(EnemyHealth enemy)
    {
        return FlatDelta(enemy.transform.position, transform.position).magnitude;
    }

    SoftAim BuildAim(Vector2 intentFacing2D, Vector3 intentWorld, int comboStep,
        float baseSpeed, float baseDuration, float easeOutPower)
    {
        Vector3 toTarget = FlatDelta(comboTarget.transform.position, transform.position);
        float dist = toTarget.magnitude;
        if (dist < 0.001f)
        {
            // On top of the target — no directional cue; keep facing, cancel travel.
            debugHasTarget = true;
            debugDesiredTravel = 0f;
            debugAttackSpacing = ResolveAttackSpacing(comboTarget);
            return new SoftAim
            {
                facing2D = intentFacing2D.sqrMagnitude > 0.0001f
                    ? intentFacing2D.normalized
                    : Vector2.down,
                speedScale = minLungeScale,
                steered = true
            };
        }

        Vector3 desiredDir = toTarget / dist;
        // A connecting hit means "go to them." Otherwise keep the per-step steer cone.
        // Steps past 3 reuse the widest cone so new attacks still soft-target.
        Vector3 steeredWorld = latched
            ? desiredDir
            : SteerToward(intentWorld, desiredDir, GetMaxSteer(comboStep));

        debugAimWorld = steeredWorld;
        debugHasTarget = true;

        Vector2 facing2D = motor.WorldToFacing(steeredWorld);
        float speedScale = ComputeSpeedScale(dist, comboTarget, comboStep, baseSpeed, baseDuration, easeOutPower, latched);

        return new SoftAim
        {
            facing2D = facing2D,
            speedScale = speedScale,
            steered = true
        };
    }

    SoftAim Passthrough(Vector2 intentFacing2D)
    {
        debugHasTarget = false;
        if (motor != null)
        {
            debugIntentWorld = motor.FacingToWorld(intentFacing2D);
            debugAimWorld = debugIntentWorld;
        }

        return new SoftAim
        {
            facing2D = intentFacing2D.sqrMagnitude > 0.0001f
                ? intentFacing2D.normalized
                : Vector2.down,
            speedScale = 1f,
            steered = false
        };
    }

    EnemyHealth FindBestAcquisitionTarget(Vector3 intentWorld)
    {
        var list = EnemyHealth.All;
        if (list == null || list.Count == 0)
            return null;

        float cosLimit = Mathf.Cos(acquisitionHalfAngle * Mathf.Deg2Rad);
        float bestScore = float.NegativeInfinity;
        EnemyHealth best = null;

        Vector3 origin = transform.position;

        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth enemy = list[i];
            if (!IsUsable(enemy))
                continue;

            Vector3 delta = FlatDelta(enemy.transform.position, origin);
            float dist = delta.magnitude;
            if (dist > acquisitionRadius || dist < 0.05f)
                continue;

            Vector3 dir = delta / dist;
            float cos = Vector3.Dot(intentWorld, dir);
            if (cos < cosLimit)
                continue;

            float angleDeg = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
            float angleNorm = 1f - (angleDeg / Mathf.Max(1f, acquisitionHalfAngle));
            float distNorm = 1f - (dist / Mathf.Max(0.01f, acquisitionRadius));
            float score = (weightAngle * angleNorm) + (weightDistance * distNorm);

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    bool IsWithinRetention(EnemyHealth enemy, Vector3 intentWorld)
    {
        Vector3 delta = FlatDelta(enemy.transform.position, transform.position);
        float dist = delta.magnitude;
        if (dist > retentionRadius)
            return false;

        if (dist < 0.05f)
            return true;

        float cos = Vector3.Dot(intentWorld, delta / dist);
        float cosLimit = Mathf.Cos(retentionHalfAngle * Mathf.Deg2Rad);
        return cos >= cosLimit;
    }

    static bool IsUsable(EnemyHealth enemy)
    {
        return enemy != null && !enemy.IsDead && enemy.isActiveAndEnabled;
    }

    static Vector3 FlatDelta(Vector3 to, Vector3 from)
    {
        Vector3 d = to - from;
        d.y = 0f;
        return d;
    }

    float GetMaxSteer(int step)
    {
        if (step <= 1) return maxSteerAngleStep1;
        if (step == 2) return maxSteerAngleStep2;
        return maxSteerAngleStep3;
    }

    static Vector3 SteerToward(Vector3 intent, Vector3 desired, float maxDegrees)
    {
        intent.y = 0f;
        desired.y = 0f;
        if (intent.sqrMagnitude < 0.0001f) return desired.normalized;
        if (desired.sqrMagnitude < 0.0001f) return intent.normalized;

        intent.Normalize();
        desired.Normalize();

        float angle = Vector3.Angle(intent, desired);
        if (angle <= 0.01f) return desired;
        if (angle <= maxDegrees) return desired;

        return Vector3.Slerp(intent, desired, maxDegrees / angle).normalized;
    }

    float ComputeSpeedScale(float centerDist, EnemyHealth enemy, int comboStep,
        float baseSpeed, float baseDuration, float easeOutPower, bool directLatch)
    {
        float spacing = ResolveAttackSpacing(enemy);
        debugAttackSpacing = spacing;

        // How far we still need to travel to sit at correct attack spacing (not onto enemy center).
        float desiredTravel = Mathf.Max(0f, centerDist - spacing);
        debugDesiredTravel = desiredTravel;

        float nominal = EstimateLungeDistance(baseSpeed, baseDuration, easeOutPower);
        if (nominal < 0.01f)
            return desiredTravel <= 0f ? minLungeScale : 1f;

        // Already in range → shrink/cancel positional lunge (anim still plays).
        if (desiredTravel <= 0.001f)
            return minLungeScale;

        float scale = desiredTravel / nominal;
        float up = directLatch
            ? Mathf.Max(maxLungeScale, latchMaxLungeScale)
            : maxLungeScale + comboStepDistanceBoost * Mathf.Max(0, comboStep - 1);
        return Mathf.Clamp(scale, minLungeScale, up);
    }

    /// <summary>
    /// Desired player↔enemy center spacing so the melee hitbox connects naturally:
    /// hitbox reach + enemy body radius + padding.
    /// </summary>
    float ResolveAttackSpacing(EnemyHealth enemy)
    {
        float reach = 0.6f;
        if (combat != null)
            reach = combat.GetMeleeHitboxReach();

        float enemyRadius = EstimateHorizontalRadius(enemy != null ? enemy.transform : null);
        float spacing = reach + enemyRadius + attackSpacingPadding - Mathf.Max(0f, closeIn);
        return Mathf.Max(minCenterSpacing, spacing);
    }

    float EstimateHorizontalRadius(Transform t)
    {
        if (t == null)
            return fallbackEnemyRadius;

        // Prefer the real hurt volume. The CharacterController is a small
        // movement capsule and makes a locked combo think it is already in range.
        var hurt = FindHurtbox(t);
        if (hurt != null)
        {
            Vector3 ext = hurt.bounds.extents;
            float r = Mathf.Max(ext.x, ext.z);
            if (r > 0.05f)
                return r;
        }

        var cc = t.GetComponent<CharacterController>();
        if (cc != null)
        {
            float scale = Mathf.Max(t.lossyScale.x, t.lossyScale.z);
            return Mathf.Max(0.05f, cc.radius * scale);
        }

        return fallbackEnemyRadius;
    }

    static Collider FindHurtbox(Transform root)
    {
        if (root == null) return null;
        var cols = root.GetComponentsInChildren<Collider>(true);
        Collider named = null;
        Collider best = null;
        float bestArea = 0f;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null || c.isTrigger) continue;
            if (c is CharacterController) continue;
            if (c.gameObject.name == "Hurtbox")
                named = c;
            Vector3 e = c.bounds.extents;
            float area = e.x * e.z;
            if (area > bestArea)
            {
                bestArea = area;
                best = c;
            }
        }
        return named != null ? named : best;
    }

    /// <summary>
    /// Approximate horizontal travel for a lunge with optional ease-out.
    /// speed *= 1 - t^p  →  integral = p/(p+1). Constant speed → 1.
    /// </summary>
    static float EstimateLungeDistance(float speed, float duration, float easeOutPower)
    {
        if (speed <= 0f || duration <= 0f)
            return 0f;

        float factor = 1f;
        if (easeOutPower >= 1f)
            factor = easeOutPower / (easeOutPower + 1f);

        return speed * duration * factor;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmosWhenSelected)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.05f;
        Vector3 intent = debugIntentWorld.sqrMagnitude > 0.0001f
            ? debugIntentWorld.normalized
            : transform.forward;
        intent.y = 0f;
        if (intent.sqrMagnitude < 0.0001f) intent = Vector3.forward;
        intent.Normalize();

        // Acquisition disc + cone
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.2f);
        DrawWireDisc(origin, acquisitionRadius);
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
        DrawCone(origin, intent, acquisitionHalfAngle, acquisitionRadius);

        // Retention cone (wider)
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        DrawCone(origin, intent, retentionHalfAngle, retentionRadius);

        // Intent / aim arrows
        Gizmos.color = Color.white;
        Gizmos.DrawRay(origin, intent * 1.5f);

        Vector3 aim = debugAimWorld.sqrMagnitude > 0.0001f ? debugAimWorld.normalized : intent;
        aim.y = 0f;
        if (aim.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
            Gizmos.DrawRay(origin, aim.normalized * 2f);
        }

        if (debugHasTarget && IsUsable(comboTarget))
        {
            Vector3 tp = comboTarget.transform.position;
            tp.y = origin.y;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(origin, tp);
            Gizmos.DrawWireSphere(tp, 0.35f);

            // Desired attack spacing around the enemy (stop outside this ring).
            if (debugAttackSpacing > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.55f);
                DrawWireDisc(tp, debugAttackSpacing);
            }

            // Desired travel remaining along aim.
            if (debugDesiredTravel > 0.01f && debugAimWorld.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
                Gizmos.DrawRay(origin, debugAimWorld.normalized * debugDesiredTravel);
            }
        }
    }

    static void DrawWireDisc(Vector3 center, float radius)
    {
        const int seg = 48;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    static void DrawCone(Vector3 origin, Vector3 forward, float halfAngleDeg, float length)
    {
        forward.y = 0f;
        forward.Normalize();
        Quaternion left = Quaternion.AngleAxis(-halfAngleDeg, Vector3.up);
        Quaternion right = Quaternion.AngleAxis(halfAngleDeg, Vector3.up);
        Vector3 a = left * forward * length;
        Vector3 b = right * forward * length;
        Gizmos.DrawRay(origin, a);
        Gizmos.DrawRay(origin, b);
        Gizmos.DrawLine(origin + a, origin + b);
    }
}
