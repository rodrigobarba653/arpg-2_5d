using UnityEngine;

/// <summary>
/// A consumable item that teaches its <see cref="magicToTeach"/> to the target
/// character on use. Consumed from the inventory only if the character didn't
/// already know the spell.
///
/// Create via: Assets → Create → ARPG → Items → Spell Book.
/// </summary>
[CreateAssetMenu(fileName = "SpellBook", menuName = "ARPG/Items/Spell Book")]
public class SpellBookItem : ItemDefinition
{
    [Header("Spell Book")]
    public MagicDefinition magicToTeach;

    public override ItemCategory Category => ItemCategory.Craft;

    public override bool IsUsable => magicToTeach != null;

    public override bool CanUse(PlayerHealth target)
    {
        if (target == null || magicToTeach == null) return false;
        var magic = target.GetComponent<PlayerMagic>();
        if (magic == null) return false;

        // Character must be compatible with this spell's restrictions.
        if (!magicToTeach.CanBeEquippedBy(target.character)) return false;

        // And not already know it (avoid wasting a spell book).
        return !magic.HasLearned(magicToTeach);
    }

    public override bool Use(PlayerHealth target)
    {
        if (target == null || magicToTeach == null) return false;
        var magic = target.GetComponent<PlayerMagic>();
        if (magic == null) return false;
        return magic.Learn(magicToTeach);
    }
}
