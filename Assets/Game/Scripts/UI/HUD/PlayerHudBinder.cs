using UnityEngine;

/// <summary>
/// Keeps the HUD's HP/MP pip meters in sync with whichever party member is
/// currently active — rebinds on swap, and self-heals if it ever misses the
/// swap event (same pattern as MagicCastInput's active-member tracking).
///
/// Also detects actual HP loss (as opposed to healing, or OnStatsChanged
/// firing for an unrelated reason like mana spend) and triggers a hit-shake
/// on the HP meter's front-line pip.
///
/// Place ONE of these in the HUD canvas, wire the two PipMeterWidgets.
/// </summary>
public class PlayerHudBinder : MonoBehaviour
{
    [Header("Meters")]
    public PipMeterWidget hpMeter;
    public PipMeterWidget mpMeter;

    PlayerHealth boundHealth;

    // Tracks the last known HP so Refresh() can tell "took damage" (HP went
    // down) apart from any other reason OnStatsChanged fires (healing, mana
    // spend/gain, etc). int.MinValue = "not seeded yet" — the very first
    // Refresh after a (re)bind must never be treated as damage.
    int lastHp = int.MinValue;

    void OnEnable()
    {
        Party.OnActiveChanged += HandleActiveChanged;
        RebindToActive();
    }

    void OnDisable()
    {
        Party.OnActiveChanged -= HandleActiveChanged;
        Unbind();
    }

    void Update()
    {
        // Self-heal: if this component enabled before PartyRoot spawned
        // anyone (Party.Active was null at OnEnable), or the active member
        // otherwise changed without the event reaching us, pick it up here.
        // Cheap — just a reference compare.
        if (Party.Active != boundHealth)
            RebindToActive();
    }

    void HandleActiveChanged(PlayerHealth _) => RebindToActive();

    void RebindToActive()
    {
        Unbind();

        var active = Party.Active;
        boundHealth = active;

        if (active == null)
        {
            hpMeter?.Clear();
            mpMeter?.Clear();
            lastHp = int.MinValue;
            return;
        }

        active.OnStatsChanged += Refresh;

        // Seed silently — swapping to a character with less HP than the
        // previous one must never read as "just took damage".
        lastHp = active.currentHealth;

        Refresh();
    }

    void Unbind()
    {
        if (boundHealth != null)
            boundHealth.OnStatsChanged -= Refresh;
        boundHealth = null;
    }

    void Refresh()
    {
        if (boundHealth == null) return;

        int hp = boundHealth.currentHealth;

        if (lastHp != int.MinValue && hp < lastHp)
            hpMeter?.ShakeActivePip();

        lastHp = hp;

        hpMeter?.SetValues(hp, boundHealth.maxHealth);
        mpMeter?.SetValues(boundHealth.currentMana, boundHealth.maxMana);
    }
}
