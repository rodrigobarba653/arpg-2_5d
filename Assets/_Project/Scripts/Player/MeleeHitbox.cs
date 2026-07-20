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

    [Range(0f, 1f)]
    [SerializeField] private float hitVolume = 1f;

    [Header("Safety")]
    [SerializeField] private float autoDisableAfter = 0.20f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;
    [SerializeField] private bool alwaysShowDebug = true;
    [SerializeField] private Color debugColor = new Color(1f, 0f, 1f, 0.25f);

    Transform owner;

    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

    private float disableAtTime = -1f;

    private GameObject debugCube;
    private BoxCollider box;

    private int attackStep = 1;

    public void SetAttackStep(int step)
    {
        attackStep = Mathf.Clamp(step, 1, 99);
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

        hitEnemies.Clear();
        hitPlayers.Clear();

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

            if (!hitPlayers.Add(player))
                return;

            if (logDebug)
                Debug.Log($"[Hitbox] Enemy hit player {player.name}", this);

            DoHitStop();
            Vector3 dir = (player.transform.position - owner.position).normalized;
            player.TakeDamage(baseDamage, dir);
            return;
        }

        // ======================
        // OWNER = PLAYER
        // ======================
        if (ownerIsPlayer)
        {
            EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

            if (enemy == null)
                return;

            if (!hitEnemies.Add(enemy))
                return;

            if (logDebug)
                Debug.Log($"[Hitbox] Player hit enemy {enemy.name}", this);

            // 🔥 OBTENER AI
            EnemyAI ai = enemy.GetComponent<EnemyAI>();

            Vector3 dir = (enemy.transform.position - owner.position).normalized;

            // ======================
            // 🛡️ DIRECTIONAL DEFENSE
            // ======================
            // Block only if the hit comes from the enemy's front arc.
            // Hits from behind bypass the block entirely.
            if (ai != null && ai.isDefending)
            {
                Vector3 enemyForward = enemy.transform.forward;
                enemyForward.y = 0f;
                enemyForward.Normalize();

                Vector3 attackFromDir = (owner.position - enemy.transform.position);
                attackFromDir.y = 0f;
                attackFromDir.Normalize();

                float facingDot = Vector3.Dot(enemyForward, attackFromDir);

                bool hitFromFront = facingDot > 0f;

                if (hitFromFront)
                {
                    if (logDebug)
                        Debug.Log("🛡️ BLOCK (front)!", this);

                    // light pushback for feedback, no damage
                    var motor = enemy.GetComponent<EnemyMotor>();
                    if (motor)
                        motor.DoKnockback(dir, 0.5f, 0.1f);

                    DoHitStop();
                    PlayBlockOrHitSfx(enemy.transform.position, blocked: true);
                    return;
                }

                if (logDebug)
                    Debug.Log("⚔️ BACKSTAB through defense!", this);
                // back hit: fall through to normal damage
            }

            // ======================
            // ⚔️ NORMAL DAMAGE
            // ======================
            DoHitStop();
            PlayBlockOrHitSfx(enemy.transform.position, blocked: false);
            enemy.TakeDamage(ResolveDamage(), dir, attackStep);
        }
    }

    int ResolveDamage()
    {
        if (!useEquippedWeaponDamage || owner == null)
            return baseDamage;

        var eq = owner.GetComponentInParent<PlayerEquipment>();
        if (eq != null && eq.HasWeapon)
            return eq.CurrentWeapon.damage;

        return baseDamage;
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

        HitStopperManager.Instance?.DoHitStop(dur);
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