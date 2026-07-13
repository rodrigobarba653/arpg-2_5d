using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryEntry
{
    public ItemDefinition item;
    public int quantity;
}

/// <summary>
/// Runtime inventory attached to the player.
///
/// SHARED ACROSS THE PARTY — internally all entries live in a single static
/// list, so adding an item via Ax's PlayerInventory makes it visible to Jyn's
/// PlayerInventory immediately. Events also fire globally regardless of which
/// instance triggered the change (so a single UI subscription updates from
/// every character's pickups).
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    // Single shared list — every PlayerInventory instance reads/writes the same data.
    static readonly List<InventoryEntry> entries = new List<InventoryEntry>();

    // Internal static events. Instance-style events below forward subscriptions
    // here so existing code (inv.OnChanged += ...) still works.
    static event Action<ItemDefinition, int> sOnItemAdded;
    static event Action<ItemDefinition, int> sOnItemRemoved;
    static event Action sOnChanged;

    public event Action<ItemDefinition, int> OnItemAdded
    {
        add    { sOnItemAdded += value; }
        remove { sOnItemAdded -= value; }
    }
    public event Action<ItemDefinition, int> OnItemRemoved
    {
        add    { sOnItemRemoved += value; }
        remove { sOnItemRemoved -= value; }
    }
    public event Action OnChanged
    {
        add    { sOnChanged += value; }
        remove { sOnChanged -= value; }
    }

    public IReadOnlyList<InventoryEntry> Entries => entries;

    /// <summary>Wipe the shared inventory. Called by SaveManager on New Game.</summary>
    public static void ClearAll()
    {
        entries.Clear();
        sOnChanged?.Invoke();
    }

    public void Add(ItemDefinition item, int qty = 1)
    {
        if (item == null || qty <= 0) return;

        if (item.stackable)
        {
            var existing = FindEntry(item);
            if (existing != null)
            {
                int allowed = Mathf.Max(1, item.maxStack);
                int newQty = Mathf.Min(existing.quantity + qty, allowed);
                int added = newQty - existing.quantity;
                existing.quantity = newQty;

                if (added > 0)
                {
                    sOnItemAdded?.Invoke(item, added);
                    sOnChanged?.Invoke();
                }
                return;
            }
        }

        entries.Add(new InventoryEntry { item = item, quantity = qty });
        sOnItemAdded?.Invoke(item, qty);
        sOnChanged?.Invoke();
    }

    public bool Remove(ItemDefinition item, int qty = 1)
    {
        if (item == null || qty <= 0) return false;

        var existing = FindEntry(item);
        if (existing == null || existing.quantity < qty) return false;

        existing.quantity -= qty;

        if (existing.quantity <= 0)
            entries.Remove(existing);

        sOnItemRemoved?.Invoke(item, qty);
        sOnChanged?.Invoke();
        return true;
    }

    public int GetCount(ItemDefinition item)
    {
        var e = FindEntry(item);
        return e != null ? e.quantity : 0;
    }

    public bool HasItem(ItemDefinition item, int qty = 1)
    {
        return GetCount(item) >= qty;
    }

    /// <summary>
    /// Consume one of <paramref name="item"/> on the given target. Returns true
    /// only if the item was actually used (and removed from the inventory).
    /// </summary>
    public bool UseItem(ItemDefinition item, PlayerHealth target)
    {
        if (item == null) return false;
        if (!item.IsUsable) return false;
        if (!HasItem(item)) return false;
        if (!item.CanUse(target)) return false;

        if (item.Use(target))
        {
            Remove(item, 1);
            return true;
        }

        return false;
    }

    /// <summary>Returns the first KeyItem in the inventory matching the given keyId.</summary>
    public KeyItem FindKey(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return null;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.item == null) continue;

            var key = e.item as KeyItem;
            if (key != null && key.keyId == keyId)
                return key;
        }
        return null;
    }

    public bool HasKey(string keyId) => FindKey(keyId) != null;

    static InventoryEntry FindEntry(ItemDefinition item)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].item == item)
                return entries[i];
        }
        return null;
    }
}
