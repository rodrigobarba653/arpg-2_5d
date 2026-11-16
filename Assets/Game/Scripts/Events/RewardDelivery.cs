using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One authored item line. Shared by <see cref="GrantRewardsAction"/> and
/// <see cref="ClaimChestAction"/>. This does not record a chest claim.
/// </summary>
[Serializable]
public class RewardItemEntry
{
    public ItemDefinition item;

    [Min(1)]
    public int quantity = 1;
}

/// <summary>
/// Gives items and Varium through the existing inventory and wallet.
/// Does not set WorldState, does not touch the camera, and does not take a lock.
///
/// Technical debt, left as-is: <see cref="PlayerInventory.Add"/> matches the
/// asset reference, while saves store <c>item.id</c>. A full stack drops the
/// overflow and returns nothing. A second row of a non-stackable item is
/// invisible to <see cref="PlayerInventory.GetCount"/>. Notices use that count
/// delta, so a dropped overflow produces no notice.
/// </summary>
public static class RewardDelivery
{
    public static bool HasPayableReward(List<RewardItemEntry> items, int varium)
    {
        if (varium > 0)
            return true;

        if (items == null)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            if (entry != null && entry.item != null && entry.quantity > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Grants every payable line. Fails before changing anything when a
    /// required inventory or wallet is missing, or when nothing is payable.
    /// </summary>
    public static bool TryGrant(List<RewardItemEntry> items, int varium, out string failure)
    {
        failure = null;
        bool wantsItems = false;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                if (entry != null && entry.item != null && entry.quantity > 0)
                {
                    wantsItems = true;
                    break;
                }
            }
        }

        bool wantsVarium = varium > 0;
        if (!wantsItems && !wantsVarium)
        {
            failure = "no payable rewards";
            return false;
        }

        PlayerInventory inventory = null;
        if (wantsItems)
        {
            inventory = FindInventory();
            if (inventory == null)
            {
                failure = "no PlayerInventory";
                return false;
            }
        }

        PlayerWallet wallet = null;
        if (wantsVarium)
        {
            wallet = PlayerWallet.Instance;
            if (wallet == null)
            {
                failure = "no PlayerWallet";
                return false;
            }
        }

        if (wantsItems)
            GrantItems(inventory, items);

        if (wantsVarium)
            GrantVarium(wallet, varium);

        return true;
    }

    static void GrantItems(PlayerInventory inventory, List<RewardItemEntry> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            if (entry == null || entry.item == null || entry.quantity <= 0)
                continue;

            int before = inventory.GetCount(entry.item);
            inventory.Add(entry.item, entry.quantity);
            int gained = inventory.GetCount(entry.item) - before;
            if (gained <= 0)
                continue;

            ItemPickupNotifier.ShowEventReward(ItemLabel(entry.item, gained), entry.item.icon);
        }
    }

    static void GrantVarium(PlayerWallet wallet, int amount)
    {
        int before = PlayerWallet.Total;
        wallet.AddVarium(amount);
        int gained = PlayerWallet.Total - before;
        if (gained <= 0)
            return;

        ItemPickupNotifier.ShowEventReward("Varium x" + gained, null);
    }

    static string ItemLabel(ItemDefinition item, int gained)
    {
        string name = item.displayName;
        if (string.IsNullOrEmpty(name))
            name = item.name;
        if (string.IsNullOrEmpty(name))
            name = item.id;
        if (string.IsNullOrEmpty(name))
            name = "Item";

        if (gained == 1)
            return name;
        return name + " x" + gained;
    }

    static PlayerInventory FindInventory()
    {
        var players = PlayerHealth.All;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null)
                continue;
            var inventory = players[i].GetComponent<PlayerInventory>();
            if (inventory != null)
                return inventory;
        }

        var tagged = GameObject.FindWithTag("Player");
        return tagged != null ? tagged.GetComponent<PlayerInventory>() : null;
    }
}
