using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A magic spell as an inventory item. Owning this item = having access to
/// the spell. Compatible characters can then equip it in one of their 3 magic
/// slots (see PlayerMagic).
///
/// Non-stackable and unique per inventory — only one copy can exist at a time.
/// Removing the item auto-unequips it from any slot that was using it.
///
/// Absorbs everything the old MagicDefinition used to hold (compatibility,
/// cost, cooldown, VFX, spell-effect prefab).
///
/// Create via: Assets → Create → ARPG → Items → Spell.
/// </summary>
/// <summary>One character's damage multiplier override for a SpellItem — the
/// elemental-affinity system (a fire-type character hits harder with fire
/// spells than an off-element character using the same spell).</summary>
[System.Serializable]
public struct CharacterDamageMultiplier
{
    public CharacterDefinition character;
    [Min(0f)] public float multiplier;
}

[CreateAssetMenu(fileName = "Spell", menuName = "ARPG/Items/Spell")]
public class SpellItem : ItemDefinition
{
    [Header("Character Restrictions")]
    [Tooltip("Which characters can EQUIP this spell. Leave empty to allow any " +
             "character. Anyone can pick up and hold the item regardless of this.")]
    public List<CharacterDefinition> compatibleCharacters = new List<CharacterDefinition>();

    [Header("Cost & Cooldown")]
    [Min(0)] public int manaCost = 10;
    [Min(0f)] public float cooldown = 1f;

    [Header("Damage Scaling")]
    [Tooltip("Default multiplier applied to the caster's CURRENT weapon damage " +
             "(PlayerEquipment.TotalDamage — base weapon + Blade/damage parts) " +
             "for any compatible character NOT listed in Per-Character " +
             "Multipliers below. Balance spells against each other here: a " +
             "fast, short-cooldown spell (e.g. Fire) might use 0.5; a slow, " +
             "multi-target spell (e.g. Thunder) might use 1.1+.")]
    [Min(0f)] public float defaultDamageMultiplier = 1f;

    [Tooltip("Per-character overrides — an elemental affinity system. E.g. Ax " +
             "(a fire-type character) casting Fireball hits harder than Ax " +
             "casting an off-element spell like Blizzard, even though both are " +
             "'compatible' and equippable. Any character not listed here falls " +
             "back to Default Damage Multiplier above.")]
    public List<CharacterDamageMultiplier> perCharacterMultipliers = new List<CharacterDamageMultiplier>();

    [Header("UI Display")]
    [Tooltip("Damage number shown in the equip menu / cast wheel. Purely for " +
             "display — actual damage is computed live via ComputeDamage(), " +
             "not read from this field. Set it to whatever example number " +
             "makes sense for your current weapon balance.")]
    [Min(0)] public int displayDamage = 0;

    [Header("Animation & FX")]
    [Tooltip("Animator trigger fired on the caster when this spell casts.")]
    public string animTrigger = "Cast";

    public AudioClip castSound;

    [Tooltip("Optional VFX spawned at the caster's position on cast (hand burst " +
             "etc). Purely cosmetic — the spell body is spellEffectPrefab.")]
    public GameObject castVfxPrefab;

    [Header("Spell Effect")]
    [Tooltip("Prefab with a SpellEffect component (ProjectileSpellEffect, " +
             "AreaSpellEffect, etc.) that runs the spell's actual behaviour.")]
    public GameObject spellEffectPrefab;

    public override ItemCategory Category => ItemCategory.Craft;

    /// <summary>Spells are equipped, not "used" from the menu.</summary>
    public override bool IsUsable => false;

    void OnEnable()
    {
        // Force these defaults so a mistakenly-stackable asset doesn't break
        // the "one copy per inventory" contract.
        stackable = false;
        maxStack = 1;
    }

    /// <summary>True if the given character is allowed to equip this spell.
    /// Empty list = anyone can equip.</summary>
    public bool CanBeEquippedBy(CharacterDefinition character)
    {
        if (compatibleCharacters == null || compatibleCharacters.Count == 0) return true;
        for (int i = 0; i < compatibleCharacters.Count; i++)
            if (compatibleCharacters[i] != null && compatibleCharacters[i] == character) return true;
        return false;
    }

    /// <summary>The damage multiplier for a specific character — their entry
    /// in perCharacterMultipliers if one exists, otherwise defaultDamageMultiplier.</summary>
    public float GetDamageMultiplierFor(CharacterDefinition character)
    {
        if (perCharacterMultipliers != null && character != null)
        {
            for (int i = 0; i < perCharacterMultipliers.Count; i++)
            {
                if (perCharacterMultipliers[i].character == character)
                    return perCharacterMultipliers[i].multiplier;
            }
        }
        return defaultDamageMultiplier;
    }

    /// <summary>
    /// This spell's actual damage for the given caster: their CURRENT weapon
    /// damage (PlayerEquipment.TotalDamage) times that CHARACTER's own damage
    /// multiplier (elemental affinity — see perCharacterMultipliers). Falls
    /// back to displayDamage if the caster has no PlayerEquipment (shouldn't
    /// normally happen for a real party member).
    /// </summary>
    public int ComputeDamage(PlayerHealth caster)
    {
        var eq = caster != null ? caster.GetComponent<PlayerEquipment>() : null;
        int weaponDamage = eq != null ? eq.TotalDamage : displayDamage;

        var character = caster != null ? caster.character : null;
        float multiplier = GetDamageMultiplierFor(character);

        return Mathf.Max(0, Mathf.RoundToInt(weaponDamage * multiplier));
    }

    /// <summary>
    /// Fire the spell effect. Instantiates spellEffectPrefab and calls
    /// SpellEffect.Init on it. Returns true if the cast should count against
    /// mana/cooldown (false = abort, refund).
    /// </summary>
    public virtual bool Cast(PlayerHealth caster)
    {
        if (spellEffectPrefab == null || caster == null) return true;

        var go = Instantiate(spellEffectPrefab, caster.transform.position, caster.transform.rotation);
        var effect = go.GetComponent<SpellEffect>();
        if (effect != null)
        {
            effect.Init(caster, this);
        }
        else
        {
            Debug.LogWarning($"[SpellItem] '{displayName}' spellEffectPrefab has no " +
                             "SpellEffect component. Instantiated as-is.", this);
        }
        return true;
    }
}
