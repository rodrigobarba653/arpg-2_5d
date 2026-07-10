using UnityEngine;

/// <summary>
/// Base ScriptableObject for a magic spell. Subclass and override
/// <see cref="Cast"/> to implement a spell's effect (damage, heal, buff,
/// spawn projectile, etc.). Or leave it as a default no-op for testing.
///
/// Create via: Assets → Create → ARPG → Magic → Magic Definition.
/// </summary>
[CreateAssetMenu(fileName = "Magic", menuName = "ARPG/Magic/Magic Definition")]
public class MagicDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id used by SaveData and SpellBook items.")]
    public string id;
    public string displayName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Character Restrictions")]
    [Tooltip("Which characters can learn and equip this spell. Leave EMPTY " +
             "to allow any character.")]
    public System.Collections.Generic.List<CharacterDefinition> compatibleCharacters
        = new System.Collections.Generic.List<CharacterDefinition>();

    /// <summary>
    /// True if this spell can be equipped by the given character. Empty list
    /// means it's unrestricted (any character can use it).
    /// </summary>
    public bool CanBeEquippedBy(CharacterDefinition character)
    {
        if (compatibleCharacters == null || compatibleCharacters.Count == 0)
            return true;

        for (int i = 0; i < compatibleCharacters.Count; i++)
        {
            if (compatibleCharacters[i] != null && compatibleCharacters[i] == character)
                return true;
        }
        return false;
    }

    [Header("Cost & Cooldown")]
    [Min(0)] public int manaCost = 10;
    [Min(0f)] public float cooldown = 1f;

    [Header("Animation & FX (optional)")]
    [Tooltip("Animator trigger fired on the caster when this magic is cast.")]
    public string animTrigger = "Cast";

    public AudioClip castSound;

    [Tooltip("Optional VFX spawned at the caster's position when the spell " +
             "is cast (e.g. a burst on the caster's hand). Purely cosmetic — " +
             "the actual spell effect is spawned via spellEffectPrefab.")]
    public GameObject castVfxPrefab;

    [Header("Spell Effect")]
    [Tooltip("Prefab with a SpellEffect component (ProjectileSpellEffect, " +
             "AreaSpellEffect, etc.) that implements the spell's actual " +
             "behaviour — projectile, area strike, buff, etc. Leave null for " +
             "a placeholder spell that consumes mana but does nothing.")]
    public GameObject spellEffectPrefab;

    /// <summary>
    /// Implement the actual spell effect. Return true if the cast should be
    /// considered successful (mana was consumed, cooldown started). Return
    /// false to abort — mana is refunded and the cast doesn't count.
    ///
    /// Default implementation instantiates <see cref="spellEffectPrefab"/> and
    /// calls Init on its SpellEffect component. Subclasses can override for
    /// fully custom logic (buffs, teleport, etc.).
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
            Debug.LogWarning($"[MagicDefinition] '{displayName}' spellEffectPrefab " +
                             "has no SpellEffect component. Instantiated as-is.", this);
        }
        return true;
    }
}
