using UnityEngine;

/// <summary>
/// Revives a fallen character with some HP. Can ONLY be used on a target with
/// 0 HP (currentHealth <= 0).
/// </summary>
[CreateAssetMenu(fileName = "PhoenixDown", menuName = "ARPG/Items/Phoenix Down")]
public class PhoenixDownItem : ItemDefinition
{
    [Header("Phoenix Down")]
    [Tooltip("HP restored on revive. Use a number or a percentage via revivePercent.")]
    public int reviveHP = 50;

    [Tooltip("If > 0, overrides reviveHP and restores this percentage of maxHP (0-1).")]
    [Range(0f, 1f)]
    public float revivePercent = 0f;

    public override ItemCategory Category => ItemCategory.Revive;

    public override bool IsUsable => true;

    public override bool CanUse(PlayerHealth target)
    {
        return target != null && target.currentHealth <= 0;
    }

    public override bool Use(PlayerHealth target)
    {
        if (!CanUse(target)) return false;

        int amount = revivePercent > 0f
            ? Mathf.RoundToInt(target.maxHealth * revivePercent)
            : reviveHP;

        target.Heal(amount);
        return true;
    }
}
