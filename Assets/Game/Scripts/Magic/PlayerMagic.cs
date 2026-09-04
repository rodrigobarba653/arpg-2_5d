using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-character magic system. The pool of AVAILABLE spells is the shared
/// PlayerInventory (any SpellItem entry). Each character owns SlotCount
/// equipped slots that reference SpellItems from that shared inventory.
///
/// Compatibility (SpellItem.compatibleCharacters) is only enforced at equip
/// time; picking up a spell always adds it to the shared inventory. If a
/// SpellItem is removed from the inventory, any slot that referenced it is
/// automatically cleared.
/// </summary>
public class PlayerMagic : MonoBehaviour
{
    public const int SlotCount = 3;

    // ============================================================
    // EQUIPPED SLOTS (per-character)
    // ============================================================
    [Header("Equipped Slots (this character only)")]
    [Tooltip("SpellItems bound to each cast-wheel slot. Length is always " +
             "SlotCount (3). Null = empty slot. All references must exist in " +
             "the shared PlayerInventory; if the item is removed from the " +
             "inventory, the slot is cleared automatically.")]
    public SpellItem[] equippedSlots = new SpellItem[SlotCount];

    [Tooltip("Index of the slot the cast wheel currently has highlighted. " +
             "Cast() with no arguments uses this slot.")]
    [Range(0, SlotCount - 1)]
    public int selectedSlot = 0;

    /// <summary>The spell that fires on a bare Cast() — the currently selected slot.</summary>
    public SpellItem equipped
    {
        get
        {
            if (selectedSlot < 0 || selectedSlot >= SlotCount) return null;
            return equippedSlots[selectedSlot];
        }
    }

    // ============================================================
    // EVENTS
    // ============================================================
    /// <summary>Fired when any slot on THIS character changes.
    /// Payload = (slotIndex, new spell). Null spell = the slot was cleared.</summary>
    public event Action<int, SpellItem> OnSlotChanged;

    /// <summary>Fired when selectedSlot changes on THIS character.</summary>
    public event Action<int> OnSelectedSlotChanged;

    /// <summary>Fired right after a successful cast on THIS character.</summary>
    public event Action<SpellItem> OnCast;

    // ============================================================
    // STATE
    // ============================================================
    readonly float[] lastCastTimePerSlot = new float[SlotCount];

    PlayerHealth health;
    PlayerInventory inventory;
    Animator animator;

    void Awake()
    {
        if (equippedSlots == null || equippedSlots.Length != SlotCount)
        {
            var fresh = new SpellItem[SlotCount];
            if (equippedSlots != null)
                for (int i = 0; i < equippedSlots.Length && i < SlotCount; i++)
                    fresh[i] = equippedSlots[i];
            equippedSlots = fresh;
        }

        for (int i = 0; i < SlotCount; i++) lastCastTimePerSlot[i] = -999f;

        health = GetComponent<PlayerHealth>();
        inventory = GetComponent<PlayerInventory>();
        animator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        // Subscribe to inventory changes so a removed SpellItem auto-unequips
        // from any slot that was holding it. PlayerInventory's events are
        // shared-list-wide, so subscribing on any instance is sufficient.
        if (inventory != null)
        {
            inventory.OnItemRemoved += HandleItemRemoved;
        }
    }

    void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnItemRemoved -= HandleItemRemoved;
        }
    }

    void HandleItemRemoved(ItemDefinition item, int qty)
    {
        var spell = item as SpellItem;
        if (spell == null) return;

        // If the party no longer has this SpellItem, clear it from any slot
        // on this character. GetCount checks the shared pool.
        if (inventory != null && inventory.GetCount(spell) > 0) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (equippedSlots[i] == spell)
            {
                equippedSlots[i] = null;
                OnSlotChanged?.Invoke(i, null);
            }
        }
    }

    // ============================================================
    // AVAILABILITY / COMPATIBILITY
    // ============================================================
    /// <summary>True if the party has this SpellItem in the shared inventory.</summary>
    public bool HasSpellInInventory(SpellItem spell)
    {
        if (spell == null) return false;
        return inventory != null && inventory.HasItem(spell);
    }

    /// <summary>True if THIS character is compatible with the given spell
    /// (based on SpellItem.compatibleCharacters).</summary>
    public bool IsCompatible(SpellItem spell)
    {
        if (spell == null) return false;
        var character = health != null ? health.character : null;
        return spell.CanBeEquippedBy(character);
    }

    // ============================================================
    // EQUIP
    // ============================================================
    /// <summary>Bind (or clear) a spell to a specific slot on this character.
    /// Requires: the SpellItem is in the shared inventory + this character is
    /// compatible. Passing null unequips the slot.</summary>
    public bool Equip(SpellItem spell, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return false;

        if (spell != null)
        {
            if (!HasSpellInInventory(spell)) return false;
            if (!IsCompatible(spell)) return false;

            // No duplicates across slots on the same character.
            for (int i = 0; i < SlotCount; i++)
            {
                if (i == slotIndex) continue;
                if (equippedSlots[i] == spell)
                {
                    equippedSlots[i] = null;
                    OnSlotChanged?.Invoke(i, null);
                }
            }
        }

        if (equippedSlots[slotIndex] == spell) return true;

        equippedSlots[slotIndex] = spell;
        OnSlotChanged?.Invoke(slotIndex, spell);
        return true;
    }

    public SpellItem GetEquipped(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        return equippedSlots[slotIndex];
    }

    public int EquippedCount()
    {
        int n = 0;
        for (int i = 0; i < SlotCount; i++)
            if (equippedSlots[i] != null) n++;
        return n;
    }

    public int FirstEquippedSlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (equippedSlots[i] != null) return i;
        return -1;
    }

    public int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (equippedSlots[i] == null) return i;
        return -1;
    }

    public void SetSelectedSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        if (selectedSlot == slotIndex) return;
        selectedSlot = slotIndex;
        OnSelectedSlotChanged?.Invoke(selectedSlot);
    }

    /// <summary>Called by MagicPickup right after adding a SpellItem to the
    /// inventory — auto-equips it in the first empty slot if this character
    /// is compatible. No-op if it's already equipped or no slot is free.</summary>
    public void TryAutoEquip(SpellItem spell)
    {
        if (spell == null) return;
        if (!IsCompatible(spell)) return;

        for (int i = 0; i < SlotCount; i++)
            if (equippedSlots[i] == spell) return;

        int empty = FirstEmptySlot();
        if (empty < 0) return;

        Equip(spell, empty);
    }

    // ============================================================
    // CAST
    // ============================================================
    public string GetCastBlockReason(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return "bad slot";
        var spell = equippedSlots[slotIndex];
        if (spell == null) return "empty slot";
        if (!HasSpellInInventory(spell)) return "spell removed from inventory";
        if (health != null && health.currentMana < spell.manaCost) return "not enough mana";
        float since = Time.time - lastCastTimePerSlot[slotIndex];
        if (since < spell.cooldown)
            return $"on cooldown ({spell.cooldown - since:0.00}s)";
        return null;
    }

    public bool Cast() => Cast(selectedSlot);

    public bool Cast(int slotIndex)
    {
        string reason = GetCastBlockReason(slotIndex);
        if (reason != null)
        {
            Debug.Log($"[PlayerMagic] '{name}' can't cast slot {slotIndex}: {reason}.", this);
            return false;
        }

        var spell = equippedSlots[slotIndex];

        if (!spell.Cast(health)) return false;

        if (health != null && spell.manaCost > 0)
        {
            health.currentMana = Mathf.Max(0, health.currentMana - spell.manaCost);
            health.NotifyStatsChanged();
        }

        if (animator != null && !string.IsNullOrEmpty(spell.animTrigger))
            animator.SetTrigger(spell.animTrigger);

        if (spell.castSound != null)
            AudioManager.Play2DOrFallback(spell.castSound);

        if (spell.castVfxPrefab != null)
            Instantiate(spell.castVfxPrefab, transform.position, transform.rotation);

        lastCastTimePerSlot[slotIndex] = Time.time;
        OnCast?.Invoke(spell);
        return true;
    }

    public float CooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return 0f;
        var spell = equippedSlots[slotIndex];
        if (spell == null) return 0f;
        float rem = spell.cooldown - (Time.time - lastCastTimePerSlot[slotIndex]);
        return rem > 0f ? rem : 0f;
    }
}
