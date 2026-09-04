using UnityEngine;

/// <summary>
/// Projectile that moves forward from the caster and damages the first enemy
/// it hits. Attach to the prefab of a Fireball / IceLance / MagicMissile spell.
/// The GameObject should also have a trigger Collider (SphereCollider works
/// great) and any visual particle system.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ProjectileSpellEffect : SpellEffect
{
    [Header("Motion")]
    [Tooltip("Speed in world units per second.")]
    public float speed = 12f;

    [Tooltip("Seconds before the projectile self-destructs (in case it doesn't " +
             "hit anything).")]
    public float lifetime = 3f;

    [Tooltip("How far in front of the caster to spawn.")]
    public float spawnForwardOffset = 0.8f;

    [Tooltip("Height above the caster's feet where the projectile spawns.")]
    public float spawnHeight = 1.2f;

    [Tooltip("Optional layers to consider as hittable. Leave 'Nothing' to hit " +
             "anything with an EnemyHealth in the hierarchy.")]
    public LayerMask hitLayerMask = ~0;

    [Tooltip("If true, the projectile destroys itself on the first enemy hit. " +
             "If false, it keeps going through and can hit multiple enemies " +
             "(each enemy only once).")]
    public bool destroyOnHit = true;

    [Header("Impact FX (optional)")]
    public GameObject impactVfxPrefab;
    public AudioClip impactSound;

    Vector3 direction;
    PlayerHealth casterRef;
    int resolvedDamage;
    readonly System.Collections.Generic.HashSet<EnemyHealth> hitEnemies = new System.Collections.Generic.HashSet<EnemyHealth>();

    public override void Init(PlayerHealth caster, SpellItem spell)
    {
        if (caster == null)
        {
            Destroy(gameObject);
            return;
        }

        casterRef = caster;

        // Damage = caster's CURRENT weapon damage × this spell's multiplier —
        // see SpellItem.ComputeDamage. Resolved once here, not re-read per hit,
        // so mid-flight equipment changes don't retroactively alter an
        // already-fired projectile.
        resolvedDamage = spell != null ? spell.ComputeDamage(caster) : 0;

        // Ignore all colliders on the caster and on any other party member so
        // the projectile doesn't self-collide on spawn or hit the other player.
        var myCols = GetComponentsInChildren<Collider>();
        var casterCols = caster.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < myCols.Length; i++)
            for (int j = 0; j < casterCols.Length; j++)
                if (myCols[i] != null && casterCols[j] != null)
                    Physics.IgnoreCollision(myCols[i], casterCols[j], true);

        if (PartyRoot.Instance != null && PartyRoot.Instance.AllSlots != null)
        {
            var slots = PartyRoot.Instance.AllSlots;
            for (int m = 0; m < slots.Count; m++)
            {
                var slot = slots[m];
                if (slot.root == null) continue;
                if (slot.root == caster.gameObject) continue;
                var memberCols = slot.root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < myCols.Length; i++)
                    for (int j = 0; j < memberCols.Length; j++)
                        if (myCols[i] != null && memberCols[j] != null)
                            Physics.IgnoreCollision(myCols[i], memberCols[j], true);
            }
        }

        // Compute the forward direction based on the caster's 2D facing (2.5D game).
        var motor = caster.GetComponent<PlayerMotor>();
        Vector2 facing2D = motor != null ? motor.GetFacing2D() : Vector2.zero;
        direction = new Vector3(facing2D.x, 0f, facing2D.y);

        // Fallback 1: caster's transform forward flattened to XZ plane.
        if (direction.sqrMagnitude < 0.001f)
        {
            Vector3 f = caster.transform.forward;
            f.y = 0f;
            direction = f;
        }

        // Fallback 2: hardcoded +Z if the caster somehow has no valid forward.
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        direction.Normalize();

        // Position the projectile in front of the caster at chest height.
        Vector3 origin = caster.transform.position + Vector3.up * spawnHeight;
        transform.position = origin + direction * spawnForwardOffset;

        // Face the direction of travel (in case the model needs orientation).
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);

        // Auto-cleanup so a projectile that flies off into the void doesn't leak.
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other) => HandleCollision(other);
    void OnCollisionEnter(Collision collision) => HandleCollision(collision.collider);

    void HandleCollision(Collider other)
    {
        if (other == null) return;

        // Layer mask filter — the projectile only reacts to layers in the mask.
        if (((1 << other.gameObject.layer) & hitLayerMask.value) == 0) return;

        // Never collide with the caster itself (defensive — Physics.IgnoreCollision
        // in Init should already prevent this).
        if (casterRef != null && other.transform.IsChildOf(casterRef.transform)) return;

        // Try to damage an enemy if there's one on this collider (or its parents).
        var enemy = other.GetComponentInParent<EnemyHealth>();
        bool didDamage = false;
        if (enemy != null && hitEnemies.Add(enemy))
        {
            Vector3 dir = direction;
            if (dir.sqrMagnitude < 0.001f)
                dir = (enemy.transform.position - transform.position).normalized;

            enemy.TakeDamage(resolvedDamage, dir, 1);
            didDamage = true;
        }

        // If the projectile is set to pass-through and we didn't damage anything
        // new, ignore this hit entirely — otherwise we'd explode on the first
        // wall behind an already-damaged enemy.
        if (!destroyOnHit && !didDamage) return;

        // Explode: spawn impact FX + sound, then destroy on hit if configured.
        if (impactVfxPrefab != null)
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        if (impactSound != null)
            AudioManager.PlaySfxOrFallback(impactSound, transform.position, 1f);

        if (destroyOnHit) Destroy(gameObject);
    }
}
