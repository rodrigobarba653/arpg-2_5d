using UnityEngine;

/// <summary>
/// Base for any component that implements a spell's actual behaviour. Attached
/// to the prefab referenced by SpellItem.spellEffectPrefab.
///
/// Subclass and override <see cref="Init"/> to implement projectile motion,
/// area damage, buffs, etc. Each subclass owns its own tuning (damage, radius,
/// speed…) in the Inspector of its prefab.
/// </summary>
public abstract class SpellEffect : MonoBehaviour
{
    /// <summary>
    /// Called by SpellItem.Cast right after this prefab is instantiated. Use it
    /// to store the caster reference, pick a target, kick off a coroutine, etc.
    /// </summary>
    public abstract void Init(PlayerHealth caster, SpellItem spell);
}
