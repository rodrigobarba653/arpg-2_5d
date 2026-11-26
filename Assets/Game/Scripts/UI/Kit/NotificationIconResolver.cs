using UnityEngine;

/// <summary>
/// Picks the UI Kit category icon for notification teal boxes:
/// Varium / Ether / gear (equipment) / potion (any other item).
/// </summary>
public static class NotificationIconResolver
{
    public static Sprite ForVarium()
    {
        var kit = UiKitRuntime.Get();
        return kit != null ? kit.notificationIconVarium : null;
    }

    public static Sprite ForItem(ItemDefinition item)
    {
        var kit = UiKitRuntime.Get();
        if (kit == null)
            return item != null ? item.icon : null;

        if (item == null)
            return kit.notificationIconPotion;

        switch (item.Category)
        {
            case ItemCategory.Ether:
                return kit.notificationIconEther != null ? kit.notificationIconEther : item.icon;
            case ItemCategory.Weapon:
            case ItemCategory.WeaponPart:
                return kit.notificationIconGear != null ? kit.notificationIconGear : item.icon;
            default:
                return kit.notificationIconPotion != null ? kit.notificationIconPotion : item.icon;
        }
    }

    /// <summary>
    /// Prefer category kit icon; fall back to an explicit sprite, then potion.
    /// </summary>
    public static Sprite Resolve(ItemDefinition item, Sprite fallback = null)
    {
        var resolved = ForItem(item);
        if (resolved != null)
            return resolved;
        if (fallback != null)
            return fallback;
        var kit = UiKitRuntime.Get();
        return kit != null ? kit.notificationIconPotion : null;
    }
}
