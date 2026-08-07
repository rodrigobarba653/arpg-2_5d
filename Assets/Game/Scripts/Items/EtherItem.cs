using UnityEngine;

[CreateAssetMenu(fileName = "Ether", menuName = "ARPG/Items/Ether")]
public class EtherItem : ItemDefinition
{
    [Header("Ether")]
    [Tooltip("Amount of MP restored when consumed.")]
    public int restoreAmount = 30;

    public override ItemCategory Category => ItemCategory.Ether;

    public override bool IsUsable => true;

    public override bool CanUse(PlayerHealth target)
    {
        return target != null
            && target.currentHealth > 0                  // can't use on dead
            && target.currentMana < target.maxMana;
    }

    public override bool Use(PlayerHealth target)
    {
        if (!CanUse(target)) return false;

        target.RestoreMP(restoreAmount);
        return true;
    }
}
