using UnityEngine;

public enum ItemCategory
{
    Key,
    Weapon,
    Craft,
    Potion,
    Ether
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
}
