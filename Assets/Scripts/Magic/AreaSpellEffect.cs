using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns at a target position (nearest enemy in range, or a fixed distance in
/// front of the caster) and deals damage in a radius after an optional delay.
/// Attach to a Thunder / Meteor / Frost Nova prefab.
///
/// The prefab should typically be just the VFX/audio; the collider logic here
/// is handled via Physics.OverlapSphere so no Collider is required.
/// </summary>
public class AreaSpellEffect : SpellEffect
{
    public enum TargetMode
    {
        /// <summary>Snap to the nearest enemy within <see cref="targetSearchRadius"/>
        /// of the caster. If none, fall back to ForwardOffset.</summary>
        NearestEnemy,
        /// <summary>Always spawn at caster.position + facing * spawnForwardOffset.</summary>
        ForwardOffset,
    }

    [Header("Targeting")]
    public TargetMode targetMode = TargetMode.NearestEnemy;

    [Tooltip("How far the caster can auto-target enemies in NearestEnemy mode.")]
    public float targetSearchRadius = 10f;

    [Tooltip("Distance in front of caster when in ForwardOffset mode or when " +
             "NearestEnemy finds no target.")]
    public float spawnForwardOffset = 4f;

    [Header("Damage")]
    public int damage = 30;

    [Tooltip("Radius around the impact point that gets damaged.")]
    public float damageRadius = 3f;

    [Tooltip("Delay before the damage tick fires (lets the VFX build up).")]
    public float damageDelay = 0.35f;

    [Tooltip("Layers considered when picking up enemies.")]
    public LayerMask hitLayerMask = ~0;

    [Header("Lifetime")]
    [Tooltip("How long to keep the GameObject alive after the damage tick, so " +
             "the VFX can finish playing.")]
    public float lingerAfterDamage = 1.5f;

    [Header("Impact FX (optional)")]
    [Tooltip("Extra VFX spawned at the impact point at damage time.")]
    public GameObject impactVfxPrefab;
    public AudioClip impactSound;

    public override void Init(PlayerHealth caster, MagicDefinition magic)
    {
        if (caster == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = ChooseTargetPosition(caster);
        transform.position = targetPos;

        StartCoroutine(DamageRoutine());
    }

    Vector3 ChooseTargetPosition(PlayerHealth caster)
    {
        var motor = caster.GetComponent<PlayerMotor>();
        Vector2 facing2D = motor != null ? motor.GetFacing2D() : new Vector2(0, 1);
        Vector3 forward = new Vector3(facing2D.x, 0f, facing2D.y).normalized;
        Vector3 fallback = caster.transform.position + forward * spawnForwardOffset;

        if (targetMode == TargetMode.ForwardOffset)
            return fallback;

        // NearestEnemy: search around the caster.
        EnemyHealth nearest = null;
        float nearestSqr = targetSearchRadius * targetSearchRadius;

        var hits = Physics.OverlapSphere(caster.transform.position, targetSearchRadius,
                                         hitLayerMask.value);
        for (int i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null) continue;
            float sqr = (enemy.transform.position - caster.transform.position).sqrMagnitude;
            if (sqr <= nearestSqr)
            {
                nearestSqr = sqr;
                nearest = enemy;
            }
        }

        if (nearest != null)
            return nearest.transform.position;

        return fallback;
    }

    IEnumerator DamageRoutine()
    {
        if (damageDelay > 0f)
            yield return new WaitForSeconds(damageDelay);

        // Impact FX at the strike point.
        if (impactVfxPrefab != null)
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        if (impactSound != null)
            AudioManager.PlaySfxOrFallback(impactSound, transform.position, 1f);

        // Damage everything in the radius.
        var hits = Physics.OverlapSphere(transform.position, damageRadius, hitLayerMask.value);
        var damaged = new System.Collections.Generic.HashSet<EnemyHealth>();
        for (int i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null) continue;
            if (!damaged.Add(enemy)) continue;

            Vector3 dir = (enemy.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();

            enemy.TakeDamage(damage, dir, 1);
        }

        // Keep the GO alive for the tail of the VFX, then clean up.
        if (lingerAfterDamage > 0f)
            yield return new WaitForSeconds(lingerAfterDamage);

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
