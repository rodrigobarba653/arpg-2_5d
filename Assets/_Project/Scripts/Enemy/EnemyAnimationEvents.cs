using UnityEngine;

public class EnemyAnimationEvents : MonoBehaviour
{
    EnemyCombatController melee;
    EnemyRangedCombatController ranged;

    void Awake()
    {
        melee = GetComponentInParent<EnemyCombatController>();
        ranged = GetComponentInParent<EnemyRangedCombatController>();
    }

    // ===== MELEE =====

    public void EnableHitbox()
    {
        if (melee)
            melee.EnableHitbox();
    }

    public void DisableHitbox()
    {
        if (melee)
            melee.DisableHitbox();
    }

    public void EnableHitboxInt(int step)
    {
        if (melee)
            melee.EnableHitbox();
    }

    // ===== BOTH =====

    public void EndAttack()
    {
        if (melee)
            melee.EndAttack();

        if (ranged)
            ranged.EndAttack();
    }

    // ===== RANGED =====

    public void Shoot()
    {
        if (ranged)
            ranged.Shoot();
    }
}