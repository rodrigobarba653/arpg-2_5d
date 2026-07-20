using UnityEngine;

public enum WeaponType
{
    Sword,
    Bow,
    Staff,
    Dagger
}

[CreateAssetMenu(fileName = "Weapon", menuName = "ARPG/Items/Weapon")]
public class WeaponItem : ItemDefinition
{
    [Header("Weapon")]
    public WeaponType type;
    public int damage = 10;
    public float attackSpeed = 1f;
    public float range = 1.4f;

    [Header("Animation")]
    [Tooltip("Animator Override Controller used while this weapon is equipped. " +
             "Create one via Project view > Create > Animator Override Controller, " +
             "set its Controller field to the player's base controller, then override " +
             "the Attack clips with this weapon's swing animations.")]
    public AnimatorOverrideController animatorOverride;

    public override ItemCategory Category => ItemCategory.Weapon;
}
