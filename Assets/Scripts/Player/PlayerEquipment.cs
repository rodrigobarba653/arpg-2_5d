using System;
using UnityEngine;

/// <summary>
/// Player equipment slots. Currently only one weapon slot.
///
/// Hooks PlayerInventory.OnItemAdded so the first weapon picked up is auto-
/// equipped if nothing is held (configurable).
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    [Header("Equipped State")]
    [Tooltip("Master toggle. When ON, the player is considered equipped with " +
             "currentWeapon (can attack, weapon override applied). " +
             "When OFF, the player is unequipped (can't attack, base animator restored). " +
             "Toggle this in the inspector during Play to test.")]
    public bool equipped = true;

    [Header("Current Weapon")]
    [Tooltip("The weapon ScriptableObject currently equipped. Can be set in the " +
             "Inspector for testing, or assigned via Equip().")]
    [SerializeField] private WeaponItem currentWeapon;

    [Header("Equipped Weapon Parts (slot 1/2/3 — the kinds depend on the weapon)")]
    [Tooltip("Slot 1 part. For a Sword this is Handle; for a Glove it's Knuckle.")]
    [SerializeField] private WeaponPart equippedSlot1;

    [Tooltip("Slot 2 part.")]
    [SerializeField] private WeaponPart equippedSlot2;

    [Tooltip("Slot 3 part.")]
    [SerializeField] private WeaponPart equippedSlot3;

    [Header("Behavior")]
    [Tooltip("Auto-equip a weapon as soon as it's picked up, if nothing is currently equipped.")]
    public bool autoEquipFirstWeapon = true;

    [Tooltip("Require the equipped weapon to actually be present in the inventory. " +
             "If the inventory loses it (consumed, removed), the player is unequipped. " +
             "Turn off for boss / story setups where the weapon shouldn't be in inventory.")]
    public bool requireInInventory = false;

    [Header("Animation")]
    [Tooltip("Animator that gets its runtimeAnimatorController swapped when the " +
             "equipped weapon changes. Usually the player's sprite body Animator. " +
             "If left empty, no controller swap happens.")]
    public Animator targetAnimator;

    [Tooltip("Base controller restored when no weapon (or a weapon without override) " +
             "is equipped. Auto-captured from targetAnimator on Awake if empty. " +
             "Should be the AnimatorController, NOT an AnimatorOverrideController.")]
    public RuntimeAnimatorController baseController;

    [Tooltip("Enable to print info about every animator swap.")]
    public bool debugLog = false;

    /// <summary>Fired whenever the equipped weapon changes (including unequip → null).</summary>
    public event Action<WeaponItem> OnWeaponChanged;

    /// <summary>Fired whenever any weapon part changes. Subscribers should re-read
    /// the combined stats getters.</summary>
    public event Action OnPartsChanged;

    public WeaponItem CurrentWeapon => currentWeapon;
    public bool HasWeapon => equipped && currentWeapon != null;

    public WeaponPart GetSlotPart(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return equippedSlot1;
            case 1: return equippedSlot2;
            case 2: return equippedSlot3;
            default: return null;
        }
    }

    public void SetSlotPart(int slotIndex, WeaponPart part)
    {
        switch (slotIndex)
        {
            case 0: equippedSlot1 = part; break;
            case 1: equippedSlot2 = part; break;
            case 2: equippedSlot3 = part; break;
        }
    }

    /// <summary>Get the equipped part of a specific kind (across all 3 slots).</summary>
    public WeaponPart GetPart(WeaponPartKind kind)
    {
        if (equippedSlot1 != null && equippedSlot1.partKind == kind) return equippedSlot1;
        if (equippedSlot2 != null && equippedSlot2.partKind == kind) return equippedSlot2;
        if (equippedSlot3 != null && equippedSlot3.partKind == kind) return equippedSlot3;
        return null;
    }

    /// <summary>Total damage = weapon base + sum of all parts whose role is Damage.</summary>
    public int TotalDamage
    {
        get
        {
            if (!HasWeapon) return 0;
            int dmg = currentWeapon.damage;
            dmg += SumStatByRole(WeaponPartRole.Damage);
            return dmg;
        }
    }

    public int TotalDefense => SumStatByRole(WeaponPartRole.Defense);

    public float TotalAttackSpeed
    {
        get
        {
            float spd = HasWeapon ? currentWeapon.attackSpeed : 0f;
            // Speed bonus uses the float attackSpeedBonus; sum across speed parts.
            for (int i = 0; i < 3; i++)
            {
                var p = GetSlotPart(i);
                if (p == null) continue;
                if (p.partKind.GetRole() == WeaponPartRole.Speed)
                    spd += p.attackSpeedBonus;
            }
            return spd;
        }
    }

    int SumStatByRole(WeaponPartRole role)
    {
        int total = 0;
        for (int i = 0; i < 3; i++)
        {
            var p = GetSlotPart(i);
            if (p == null) continue;
            if (p.partKind.GetRole() != role) continue;
            switch (role)
            {
                case WeaponPartRole.Damage:  total += p.damageBonus; break;
                case WeaponPartRole.Defense: total += p.defenseBonus; break;
            }
        }
        return total;
    }

    /// <summary>
    /// Equip a part. Routed to the slot whose kind matches part.partKind, based
    /// on the currently equipped weapon's slot definitions. Returns true on success.
    /// </summary>
    public bool EquipPart(WeaponPart part)
    {
        if (part == null) return false;
        if (currentWeapon == null) return false;

        int slot = currentWeapon.FindSlotIndexForKind(part.partKind);
        if (slot < 0)
        {
            Debug.LogWarning($"[PlayerEquipment] Part '{part.displayName}' (kind={part.partKind}) " +
                             $"doesn't fit any slot of weapon '{currentWeapon.displayName}'.", this);
            return false;
        }

        SetSlotPart(slot, part);
        OnPartsChanged?.Invoke();
        return true;
    }

    public void UnequipSlot(int slotIndex)
    {
        SetSlotPart(slotIndex, null);
        OnPartsChanged?.Invoke();
    }

    PlayerInventory inventory;
    bool awakeDone;
    bool lastEquipped;
    WeaponItem lastWeapon;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();

        // Auto-resolve animator: try children first (sprite body Animator).
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();

        // Capture the base controller before any override is applied. If the
        // animator was set up with an AnimatorOverrideController in the editor,
        // walk to the actual underlying base so we have something pure to fall
        // back to when unequipping.
        if (baseController == null && targetAnimator != null)
        {
            var current = targetAnimator.runtimeAnimatorController;

            while (current is AnimatorOverrideController aoc && aoc.runtimeAnimatorController != null)
                current = aoc.runtimeAnimatorController;

            baseController = current;

            if (debugLog)
                Debug.Log($"[PlayerEquipment] Captured base controller: " +
                          $"{(baseController != null ? baseController.name : "NULL")}", this);
        }

        if (targetAnimator == null)
            Debug.LogWarning("[PlayerEquipment] No targetAnimator assigned and none found in children. " +
                             "Weapon animator overrides will not be applied.", this);

        awakeDone = true;
        lastEquipped = equipped;
        lastWeapon = currentWeapon;
    }

    void Start()
    {
        // Apply the initial state.
        ApplyWeaponAnimator(equipped ? currentWeapon : null);
    }

    void OnValidate()
    {
        // Respond to inspector tweaks during Play (toggle equipped, swap weapon).
        if (!Application.isPlaying) return;
        if (!awakeDone) return;

        bool changed = equipped != lastEquipped || currentWeapon != lastWeapon;
        if (!changed) return;

        lastEquipped = equipped;
        lastWeapon = currentWeapon;

        ApplyWeaponAnimator(equipped ? currentWeapon : null);
        OnWeaponChanged?.Invoke(equipped ? currentWeapon : null);
    }

    void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnItemAdded   += HandleItemAdded;
            inventory.OnItemRemoved += HandleItemRemoved;
        }
    }

    void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnItemAdded   -= HandleItemAdded;
            inventory.OnItemRemoved -= HandleItemRemoved;
        }
    }

    void HandleItemAdded(ItemDefinition item, int qty)
    {
        if (!autoEquipFirstWeapon) return;
        if (currentWeapon != null) return;

        if (item is WeaponItem weapon)
            Equip(weapon);
    }

    void HandleItemRemoved(ItemDefinition item, int qty)
    {
        if (!requireInInventory) return;
        if (currentWeapon == null) return;
        if (item != currentWeapon) return;

        if (inventory == null || !inventory.HasItem(currentWeapon))
            Unequip();
    }

    /// <summary>Equip a weapon. Sets equipped=true and applies the override.</summary>
    public bool Equip(WeaponItem weapon)
    {
        if (weapon == null) return false;

        if (requireInInventory && inventory != null && !inventory.HasItem(weapon))
            return false;

        currentWeapon = weapon;
        equipped = true;

        lastWeapon = currentWeapon;
        lastEquipped = equipped;

        ApplyWeaponAnimator(currentWeapon);
        OnWeaponChanged?.Invoke(currentWeapon);
        return true;
    }

    /// <summary>Unequip: sets equipped=false and restores the base animator.</summary>
    public void Unequip()
    {
        equipped = false;
        lastEquipped = false;

        ApplyWeaponAnimator(null);
        OnWeaponChanged?.Invoke(null);
    }

    void ApplyWeaponAnimator(WeaponItem weapon)
    {
        if (targetAnimator == null)
        {
            if (debugLog) Debug.Log("[PlayerEquipment] No targetAnimator; skipping swap.", this);
            return;
        }

        RuntimeAnimatorController next =
            (weapon != null && weapon.animatorOverride != null)
                ? (RuntimeAnimatorController)weapon.animatorOverride
                : baseController;

        if (next == null)
        {
            if (debugLog)
                Debug.LogWarning("[PlayerEquipment] Resolved controller is null. " +
                                 "Did the weapon have no override AND was baseController never set?", this);
            return;
        }

        if (targetAnimator.runtimeAnimatorController == next)
        {
            if (debugLog) Debug.Log($"[PlayerEquipment] Animator already on '{next.name}'; no swap needed.", this);
            return;
        }

        targetAnimator.runtimeAnimatorController = next;

        if (debugLog)
            Debug.Log($"[PlayerEquipment] Swapped animator controller → '{next.name}' " +
                      $"(weapon: {(weapon != null ? weapon.displayName : "none")})", this);
    }

    /// <summary>
    /// Re-applies the override based on the currently assigned weapon. Useful
    /// when you change currentWeapon in the inspector during Play mode (which
    /// doesn't call Equip), or after changing the weapon's override asset.
    /// </summary>
    [ContextMenu("Apply Current Weapon Animator")]
    public void ForceReapplyAnimator()
    {
        ApplyWeaponAnimator(currentWeapon);
    }
}
