using UnityEngine;

public enum WeaponPartKind
{
    // Sword parts
    Handle,  // grip — affects attackSpeed
    Guard,   // crossguard — affects defense
    Blade,   // edge — affects damage

    // Glove parts
    Knuckle, // strike surface — affects damage
    Palm,    // padding — affects defense
    Wrist,   // brace — affects attackSpeed
}

/// <summary>
/// Each WeaponPartKind contributes ONE stat. Used by PlayerEquipment to sum
/// the contributions from any combination of parts.
/// </summary>
public enum WeaponPartRole
{
    Damage,
    Defense,
    Speed,
}

public static class WeaponPartKindExtensions
{
    public static WeaponPartRole GetRole(this WeaponPartKind kind)
    {
        switch (kind)
        {
            case WeaponPartKind.Blade:
            case WeaponPartKind.Knuckle:
                return WeaponPartRole.Damage;
            case WeaponPartKind.Guard:
            case WeaponPartKind.Palm:
                return WeaponPartRole.Defense;
            case WeaponPartKind.Handle:
            case WeaponPartKind.Wrist:
                return WeaponPartRole.Speed;
            default:
                return WeaponPartRole.Damage;
        }
    }
}

/// <summary>
/// A part that can be slotted into a WeaponItem. Each kind contributes ONE
/// fixed stat to the wielder's combat numbers:
///   - Blade  → damageBonus
///   - Guard  → defenseBonus
///   - Handle → attackSpeedBonus
///
/// The non-relevant stat fields are ignored at runtime so the level designer
/// only has to fill in the one that matches the part's kind.
/// </summary>
[CreateAssetMenu(fileName = "WeaponPart", menuName = "ARPG/Items/Weapon Part")]
public class WeaponPart : ItemDefinition
{
    [Header("Part")]
    public WeaponPartKind partKind;

    [Header("Stat (only the matching field is read)")]
    [Tooltip("Read when partKind = Blade. Added to the weapon's base damage.")]
    public int damageBonus;

    [Tooltip("Read when partKind = Guard. Added to the wielder's defense.")]
    public int defenseBonus;

    [Tooltip("Read when partKind = Handle. Added to the weapon's attack speed " +
             "(e.g. 0.1 = +10% faster).")]
    public float attackSpeedBonus;

    public override ItemCategory Category => ItemCategory.WeaponPart;
}
