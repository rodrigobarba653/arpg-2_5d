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
/// Runtime inventory attached to the player. No UI; expose events so a future
/// UI panel can subscribe and refresh.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] List<InventoryEntry> entries = new List<InventoryEntry>();

    public event Action<ItemDefinition, int> OnItemAdded;
    public event Action<ItemDefinition, int> OnItemRemoved;
    public event Action OnChanged;

    public IReadOnlyList<InventoryEntry> Entries => entries;

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
                    OnItemAdded?.Invoke(item, added);
                    OnChanged?.Invoke();
                }
                return;
            }
        }

        entries.Add(new InventoryEntry { item = item, quantity = qty });
        OnItemAdded?.Invoke(item, qty);
        OnChanged?.Invoke();
    }

    public bool Remove(ItemDefinition item, int qty = 1)
    {
        if (item == null || qty <= 0) return false;

        var existing = FindEntry(item);
        if (existing == null || existing.quantity < qty) return false;

        existing.quantity -= qty;

        if (existing.quantity <= 0)
            entries.Remove(existing);

        OnItemRemoved?.Invoke(item, qty);
        OnChanged?.Invoke();
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

    InventoryEntry FindEntry(ItemDefinition item)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].item == item)
                return entries[i];
        }
        return null;
    }
}
