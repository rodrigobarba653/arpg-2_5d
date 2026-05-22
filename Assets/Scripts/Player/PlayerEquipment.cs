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

    public WeaponItem CurrentWeapon => currentWeapon;
    public bool HasWeapon => equipped && currentWeapon != null;

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
