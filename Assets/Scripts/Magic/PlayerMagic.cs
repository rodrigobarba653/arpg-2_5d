using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-character magic system. Each PlayerHealth has its own PlayerMagic with
/// its OWN independent list of learned spells and equipped slot.
///
/// Currently one equipped slot. Add spell books to the inventory and use them
/// (SpellBookItem.Use) to teach a magic to a character.
///
/// Cast the equipped spell with <see cref="Cast"/> (wired from MagicCastInput).
/// </summary>
public class PlayerMagic : MonoBehaviour
{
    [Header("Learned Magics (this character only)")]
    [Tooltip("Spells this character knows. Populated at runtime via Learn(). " +
             "Pre-populate here to give a character starter spells.")]
    public List<MagicDefinition> learned = new List<MagicDefinition>();

    [Header("Equipped Slot")]
    [Tooltip("The spell that gets cast when the player presses the cast button. " +
             "Must be in the learned list.")]
    public MagicDefinition equipped;

    /// <summary>Fired when the learned list changes.</summary>
    public event Action OnLearnedChanged;

    /// <summary>Fired when the equipped magic changes (payload = new equipped, may be null).</summary>
    public event Action<MagicDefinition> OnEquippedChanged;

    /// <summary>Fired right after a successful cast (payload = cast magic).</summary>
    public event Action<MagicDefinition> OnCast;

    float lastCastTime = -999f;

    PlayerHealth health;
    Animator animator;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        animator = GetComponentInChildren<Animator>();
    }

    public IReadOnlyList<MagicDefinition> LearnedList => learned;
    public bool HasLearned(MagicDefinition def) => def != null && learned.Contains(def);

    /// <summary>True if this character is compatible with the given magic
    /// (based on MagicDefinition.compatibleCharacters).</summary>
    public bool IsCompatible(MagicDefinition def)
    {
        if (def == null) return false;
        var character = health != null ? health.character : null;
        return def.CanBeEquippedBy(character);
    }

    public bool Learn(MagicDefinition def)
    {
        if (def == null) return false;
        if (HasLearned(def)) return false;

        if (!IsCompatible(def))
        {
            Debug.Log($"[PlayerMagic] '{name}' can't learn '{def.displayName}' — " +
                      "not in the spell's compatibleCharacters list.", this);
            return false;
        }

        learned.Add(def);
        OnLearnedChanged?.Invoke();

        // Auto-equip if the slot is empty.
        if (equipped == null) Equip(def);
        return true;
    }

    public bool Forget(MagicDefinition def)
    {
        if (def == null) return false;
        if (!learned.Remove(def)) return false;

        if (equipped == def) Equip(null);
        OnLearnedChanged?.Invoke();
        return true;
    }

    public bool Equip(MagicDefinition def)
    {
        // Null unequips. Otherwise the def must have been learned first AND
        // be compatible with this character.
        if (def != null)
        {
            if (!HasLearned(def)) return false;
            if (!IsCompatible(def)) return false;
        }

        if (equipped == def) return true;

        equipped = def;
        OnEquippedChanged?.Invoke(equipped);
        return true;
    }

    /// <summary>Try to cast the equipped magic. Returns null on success or a
    /// string reason on failure ("no magic", "not enough mana", "on cooldown").</summary>
    public string GetCastBlockReason()
    {
        if (equipped == null) return "no magic equipped";
        if (health != null && health.currentMana < equipped.manaCost) return "not enough mana";
        if (Time.time - lastCastTime < equipped.cooldown)
            return $"on cooldown ({equipped.cooldown - (Time.time - lastCastTime):0.00}s)";
        return null;
    }

    public bool Cast()
    {
        string reason = GetCastBlockReason();
        if (reason != null)
        {
            Debug.Log($"[PlayerMagic] '{name}' can't cast: {reason}.", this);
            return false;
        }

        var def = equipped;

        // Fire the effect first. If the subclass returns false, don't charge
        // mana or start the cooldown.
        if (!def.Cast(health)) return false;

        // Consume mana.
        if (health != null && def.manaCost > 0)
        {
            health.currentMana = Mathf.Max(0, health.currentMana - def.manaCost);
            health.NotifyStatsChanged();
        }

        // Animation.
        if (animator != null && !string.IsNullOrEmpty(def.animTrigger))
            animator.SetTrigger(def.animTrigger);

        // Sound.
        if (def.castSound != null)
            AudioManager.Play2DOrFallback(def.castSound);

        // VFX at caster position.
        if (def.castVfxPrefab != null)
            Instantiate(def.castVfxPrefab, transform.position, transform.rotation);

        lastCastTime = Time.time;
        OnCast?.Invoke(def);
        return true;
    }
}
