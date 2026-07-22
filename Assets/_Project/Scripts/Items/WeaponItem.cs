using UnityEngine;

public enum WeaponType
{
    Sword,
    Bow,
    Staff,
    Dagger,
    Glove
}

[CreateAssetMenu(fileName = "Weapon", menuName = "ARPG/Items/Weapon")]
public class WeaponItem : ItemDefinition
{
    [Header("Weapon")]
    public WeaponType type;
    public int damage = 10;
    public float attackSpeed = 1f;
    public float range = 1.4f;

    [Header("Part Slots (3, in display order)")]
    [Tooltip("Slot 1's part kind. For Sword this is Handle, for Glove it's Knuckle, etc.")]
    public WeaponPartKind slot1Kind = WeaponPartKind.Handle;

    [Tooltip("Slot 2's part kind.")]
    public WeaponPartKind slot2Kind = WeaponPartKind.Guard;

    [Tooltip("Slot 3's part kind.")]
    public WeaponPartKind slot3Kind = WeaponPartKind.Blade;

    [Header("Animation")]
    [Tooltip("Animator Override Controller used while this weapon is equipped. " +
             "Create one via Project view > Create > Animator Override Controller, " +
             "set its Controller field to the player's base controller, then override " +
             "the Attack clips with this weapon's swing animations.")]
    public AnimatorOverrideController animatorOverride;

    public override ItemCategory Category => ItemCategory.Weapon;

    /// <summary>Get the kind of a slot by index (0, 1, 2).</summary>
    public WeaponPartKind GetSlotKind(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return slot1Kind;
            case 1: return slot2Kind;
            case 2: return slot3Kind;
            default: return slot1Kind;
        }
    }

    /// <summary>Find which slot index (0,1,2) accepts the given part kind. Returns -1 if none.</summary>
    public int FindSlotIndexForKind(WeaponPartKind kind)
    {
        if (slot1Kind == kind) return 0;
        if (slot2Kind == kind) return 1;
        if (slot3Kind == kind) return 2;
        return -1;
    }
}
