using UnityEngine;

[CreateAssetMenu(fileName = "Potion", menuName = "ARPG/Items/Potion")]
public class PotionItem : ItemDefinition
{
    [Header("Potion")]
    [Tooltip("Amount of HP restored when consumed.")]
    public int healAmount = 30;

    public override ItemCategory Category => ItemCategory.Potion;

    public override bool IsUsable => true;

    public override bool CanUse(PlayerHealth target)
    {
        return target != null
            && target.currentHealth > 0                    // can't use on dead → use Phoenix Down
            && target.currentHealth < target.maxHealth;    // already full = useless
    }

    public override bool Use(PlayerHealth target)
    {
        if (!CanUse(target)) return false;

        target.Heal(healAmount);
        return true;
    }
}
