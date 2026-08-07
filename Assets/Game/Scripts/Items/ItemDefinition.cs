using UnityEngine;

public enum ItemCategory
{
    Key,
    Weapon,
    WeaponPart,
    Craft,
    Potion,
    Ether,
    Revive
}

/// <summary>
/// Base class for every item in the game. Items are data-only ScriptableObjects:
/// the same ItemDefinition asset is referenced by world pickups, by the player's
/// inventory, by crafting recipes, by UI, etc.
///
/// Create concrete items via the Create menu (Assets > Create > ARPG > Items).
/// </summary>
public abstract class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for save data, recipes, etc.")]
    public string id;

    [Tooltip("Display name in UI.")]
    public string displayName;

    [TextArea]
    public string description;

    public Sprite icon;

    [Header("Stacking")]
    public bool stackable = true;
    public int maxStack = 99;

    public abstract ItemCategory Category { get; }

    // ============================================================
    // USAGE — overridden by subclasses (Potion heals, Ether restores MP, etc).
    // Default = not usable (Key, Craft, Weapon items don't "use" from inventory).
    // ============================================================

    /// <summary>True if this item type can be used from the inventory UI at all.</summary>
    public virtual bool IsUsable => false;

    /// <summary>True if it can be used right now on this specific target.</summary>
    public virtual bool CanUse(PlayerHealth target) => false;

    /// <summary>Apply the effect to the target. Returns true if the item was consumed.</summary>
    public virtual bool Use(PlayerHealth target) => false;
}

