using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class MeleeHitbox : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int baseDamage = 10;

    [Tooltip("If true and the owner has PlayerEquipment with a weapon equipped, " +
             "use that weapon's damage instead of baseDamage.")]
    [SerializeField] private bool useEquippedWeaponDamage = true;

    [Header("Hit Stop")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField] private float hitStopStep1 = 0.04f;
    [SerializeField] private float hitStopStep2 = 0.06f;
    [SerializeField] private float hitStopStep3 = 0.08f;

    [Header("Hit SFX")]
    [Tooltip("Played at the enemy's position when the hit lands. " +
             "One per combo step if you want variation (otherwise it falls back to index 0).")]
    [SerializeField] private AudioClip[] hitSounds = new AudioClip[3];

    [Tooltip("Optional separate sound played when the hit is blocked by an enemy's guard.")]
    [SerializeField] private AudioClip blockSound;

    [Header("Block Clash")]
    [SerializeField] private float blockEnemyPushForce = 1.25f;
    [SerializeField] private float blockEnemyPushTime = 0.08f;
    [SerializeField] private float blockPlayerPushSpeed = 5f;
    [SerializeField] private float blockPlayerPushTime = 0.12f;

    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    [Header("Safety")]
    [SerializeField] private float autoDisableAfter = 0.20f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;
    [SerializeField] private bool alwaysShowDebug = true;
    [SerializeField] private Color debugColor = new Color(1f, 0f, 1f, 0.25f);

    Transform owner;

    private readonly HashSet<int> hitEnemyIds = new HashSet<int>();
    private readonly HashSet<int> hitPlayerIds = new HashSet<int>();

    private float disableAtTime = -1f;

    private GameObject debugCube;
    private BoxCollider box;

    private int attackStep = 1;

    // Knockback for the current swing (set by PlayerCombatController per combo step).
    private bool hasAttackerKnockback;
    private bool knockbackPush;
    private float knockbackForce;
    private float knockbackDuration;

    public void SetAttackStep(int step)
    {
        attackStep = Mathf.Clamp(step, 1, 99);
    }

    /// <summary>
    /// Per-swing knockback owned by the attacker (combo step). When set, EnemyHealth
    /// uses these values instead of its own per-step fallback tables.
    /// </summary>
    public void SetKnockback(bool pushEnemy, float force, float duration)
    {
        hasAttackerKnockback = true;
        knockbackPush = pushEnemy;
        knockbackForce = Mathf.Max(0f, force);
        knockbackDuration = Mathf.Max(0f, duration);
    }

    public void ClearKnockback()
    {
        hasAttackerKnockback = false;
        knockbackPush = false;
        knockbackForce = 0f;
        knockbackDuration = 0f;
    }

    /// <summary>
    /// Start a fresh hit window for this swing. Each enemy/player can be damaged
    /// at most once until the next BeginHitWindow (multi-target still allowed).
    /// </summary>
    public void BeginHitWindow(int step)
    {
        SetAttackStep(step);
        hitEnemyIds.Clear();
        hitPlayerIds.Clear();

        if (box == null)
            box = GetComponent<BoxCollider>();
        if (box)
            box.enabled = true;

        disableAtTime = (autoDisableAfter > 0f) ? Time.time + autoDisableAfter : -1f;
        UpdateDebugCubeActive();
    }

    public void SetOwner(Transform t)
    {
        owner = t;
    }

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        if (alwaysShowDebug)
            CreateDebugCube();

        box.enabled = false;
    }

    void OnEnable()
    {
        if (logDebug)
            Debug.Log($"[Hitbox] ENABLED on {name} | owner={(owner ? owner.name : "NULL")} | step={attackStep}", this);

        // Fresh window whenever the hitbox GameObject turns on.
        hitEnemyIds.Clear();
        hitPlayerIds.Clear();

        box.enabled = true;

        disableAtTime = (autoDisableAfter > 0f) ? Time.time + autoDisableAfter : -1f;

        UpdateDebugCubeActive();
    }

    void OnDisable()
    {
        if (box) box.enabled = false;
        disableAtTime = -1f;

        UpdateDebugCubeActive();
    }

    void Update()
    {
        if (disableAtTime > 0f && Time.time >= disableAtTime)
        {
            box.enabled = false;
            disableAtTime = -1f;

            if (logDebug)
                Debug.Log("[Hitbox] AUTO DISABLED collider (failsafe)", this);

            UpdateDebugCubeActive();
        }

        if (debugCube && box)
        {
            debugCube.transform.localPosition = box.center;
            debugCube.transform.localScale = box.size;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (logDebug)
            Debug.Log($"[Hitbox] TRIGGER with: {other.name}", this);

        if (owner == null)
            TryResolveOwner();

        if (!IsOwnerTheActivePlayer())
            return;

        if (BelongsToAx())
            LogAxHit(other);

        if (owner == null)
            return;

        // ignorar al propio dueño
        if (other.transform == owner || other.transform.IsChildOf(owner))
            return;

        bool ownerIsEnemy = owner.GetComponent<EnemyHealth>() != null;
        bool ownerIsPlayer = owner.GetComponent<PlayerHealth>() != null;

        // ======================
        // OWNER = ENEMY
        // ======================
        if (ownerIsEnemy)
        {
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();

            if (player == null)
                return;

            // Only damage the ACTIVE party member. The dormant player's colliders
            // may still be enabled (pickup triggers, hitboxes, etc.) which would
            // otherwise let one enemy attack damage both characters at once.
            if (Party.Active != null && player != Party.Active)
            {
                if (logDebug)
                    Debug.Log($"[Hitbox] Enemy hit dormant '{player.name}' — ignored (only active takes damage).", this);
                return;
            }

            if (!hitPlayerIds.Add(player.GetInstanceID()))
                return;

            if (logDebug)
                Debug.Log($"[Hitbox] Enemy hit player {player.name}", this);

            DoHitStop();
            Vector3 dir = (player.transform.position - owner.position).normalized;
            player.TakeDamage(ResolveDamage(), dir);
            return;
        }

        // ======================
        // OWNER = PLAYER
        // ======================
        if (ownerIsPlayer)
        {
            // Only solid hurtboxes / CharacterController count.
            // Enemy MeleeHitbox (and other triggers) stay active often and would
            // register "phantom" hits far from the visible body.
            if (other.isTrigger)
            {
                if (logDebug)
                    Debug.Log($"[Hitbox] Ignoring trigger collider '{other.name}' (not a hurtbox).", this);
                return;
            }

            EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                return;

            if (!hitEnemyIds.Add(enemy.GetInstanceID()))
                return;

            if (logDebug)
                Debug.Log($"[Hitbox] Player hit enemy {enemy.name}", this);

            // 🔥 OBTENER AI
            EnemyAI ai = enemy.GetComponent<EnemyAI>();

            Vector3 dir = (enemy.transform.position - owner.position).normalized;

            // Player melee that the guard absorbs never reaches EnemyHealth.
            // BlocksAttackFrom is the only test: front arc, or 360 when coverage says so.
            // The clash push below is only this path. A hit that gets through falls
            // through to TakeDamage, which then drops the guard.
            if (ai != null && ai.BlocksAttackFrom(owner.position))
            {
                if (logDebug)
                    Debug.Log("🛡️ BLOCK!", this);

                ai.NotifyBlockedHit();

                var motor = enemy.GetComponent<EnemyMotor>();
                if (motor)
                    motor.DoKnockback(dir, blockEnemyPushForce, blockEnemyPushTime);

                Vector3 pushPlayer = owner.position - enemy.transform.position;
                pushPlayer.y = 0f;
                PlayerMotor playerMotor = owner.GetComponentInParent<PlayerMotor>();
                if (playerMotor != null)
                    playerMotor.BeginKnockback(pushPlayer, blockPlayerPushSpeed, blockPlayerPushTime);

                DoHitStop();
                PlayBlockOrHitSfx(enemy.transform.position, blocked: true);
                return;
            }

            // ======================
            // ⚔️ NORMAL DAMAGE
            // ======================
            DoHitStop();
            PlayBlockOrHitSfx(enemy.transform.position, blocked: false);

            if (hasAttackerKnockback)
                enemy.TakeDamage(ResolveDamage(), dir, attackStep, knockbackPush, knockbackForce, knockbackDuration);
            else
                enemy.TakeDamage(ResolveDamage(), dir, attackStep);

            owner.GetComponent<MeleeSoftTargeting>()?.NotifyConnectedHit(enemy);
        }
    }

    int overrideDamage = -1;

    /// <summary>Set a damage value that overrides both baseDamage and the
    /// equipped-weapon damage until <see cref="ClearOverrideDamage"/> is called.
    /// Used by <see cref="EnemyCombatController"/> to swap in per-attack damage
    /// for the duration of a variant.</summary>
    public void SetOverrideDamage(int value) => overrideDamage = value;
    public void ClearOverrideDamage() => overrideDamage = -1;

    int ResolveDamage()
    {
        if (overrideDamage >= 0) return overrideDamage;

        if (!useEquippedWeaponDamage || owner == null)
            return baseDamage;

        var eq = owner.GetComponentInParent<PlayerEquipment>();
        if (eq != null && eq.HasWeapon)
            return eq.TotalDamage;  // weapon base + Blade part bonus

        return baseDamage;
    }

    void TryResolveOwner()
    {
        var health = GetComponentInParent<PlayerHealth>();
        if (health != null)
            owner = health.transform;
    }

    bool IsOwnerTheActivePlayer()
    {
        var health = owner != null
            ? owner.GetComponentInParent<PlayerHealth>()
            : GetComponentInParent<PlayerHealth>();
        if (health == null)
            return true;
        return Party.Active == null || Party.Active == health;
    }

    bool BelongsToAx()
    {
        var health = owner != null
            ? owner.GetComponentInParent<PlayerHealth>()
            : GetComponentInParent<PlayerHealth>();

        if (health != null && health.character != null &&
            string.Equals(health.character.id, "Ax", System.StringComparison.OrdinalIgnoreCase))
            return true;

        Transform t = owner != null ? owner : transform;
        while (t != null)
        {
            if (t.name.IndexOf("Ax", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }

    void LogAxHit(Collider other)
    {
        if (other == null) return;
        var enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null) return;
        Debug.LogWarning($"[Ax Hitbox] Hit enemy '{enemy.name}' via '{other.name}' (combo step {attackStep}).", this);
    }

    void PlayBlockOrHitSfx(Vector3 worldPos, bool blocked)
    {
        if (AudioManager.Instance == null) return;

        AudioClip clip;

        if (blocked && blockSound != null)
        {
            clip = blockSound;
        }
        else
        {
            if (hitSounds == null || hitSounds.Length == 0) return;

            int idx = Mathf.Clamp(attackStep - 1, 0, hitSounds.Length - 1);
            clip = hitSounds[idx];

            if (clip == null) clip = hitSounds[0];
        }

        if (clip == null) return;

        AudioManager.Instance.PlaySFXAt(clip, worldPos, hitVolume);
    }

    void DoHitStop()
    {
        if (!enableHitStop)
            return;

        float dur =
            attackStep == 3 ? hitStopStep3 :
            attackStep == 2 ? hitStopStep2 :
                              hitStopStep1;

        // Use Unity's overloaded == null (not ?. operator) so a destroyed
        // HitStopperManager doesn't throw MissingReferenceException — which
        // would interrupt the damage flow before TakeDamage runs.
        var mgr = HitStopperManager.Instance;
        if (mgr != null) mgr.DoHitStop(dur);
    }

    private void CreateDebugCube()
    {
        debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debugCube.name = "Hitbox_Debug";

        var c = debugCube.GetComponent<Collider>();
        if (c) Destroy(c);

        debugCube.transform.SetParent(transform);
        debugCube.transform.localPosition = Vector3.zero;
        debugCube.transform.localRotation = Quaternion.identity;
        debugCube.transform.localScale = Vector3.one;

        var renderer = debugCube.GetComponent<MeshRenderer>();

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        mat.color = debugColor;
        renderer.material = mat;

        UpdateDebugCubeActive();
    }

    private void UpdateDebugCubeActive()
    {
        if (!debugCube) return;
        debugCube.SetActive(alwaysShowDebug || gameObject.activeSelf);
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (!col) return;

        Gizmos.color = Color.magenta;

        var old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(col.center, col.size);
        Gizmos.matrix = old;
    }
}