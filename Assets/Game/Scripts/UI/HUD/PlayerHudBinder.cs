using UnityEngine;

/// <summary>
/// Keeps the HUD's HP/MP bars in sync with whichever party member is
/// currently active — rebinds on swap, and self-heals if it ever misses the
/// swap event (same pattern as MagicCastInput's active-member tracking).
///
/// Also detects actual HP loss (as opposed to healing, or OnStatsChanged
/// firing for an unrelated reason like mana spend) and shakes this whole
/// HUD group: frame, health bar, and mana bar together.
///
/// Place ONE of these in the HUD canvas, wire the two bars.
/// </summary>
public class PlayerHudBinder : MonoBehaviour
{
    [Header("Meters")]
    public PipMeterWidget hpMeter;
    public PipMeterWidget mpMeter;

    [Header("Hit Shake")]
    [Tooltip("How far (in UI pixels) the whole frame jitters when HP drops.")]
    [Range(0f, 20f)] public float shakeMagnitude = 6f;
    [Range(0.05f, 1f)] public float shakeDuration = 0.3f;
    [Range(1f, 30f)] public float shakeFrequency = 18f;

    PlayerHealth boundHealth;
    RectTransform rt;
    float shakeTimeLeft;
    Vector2 shakeBasePosition;

    // Tracks the last known HP so Refresh() can tell "took damage" (HP went
    // down) apart from any other reason OnStatsChanged fires (healing, mana
    // spend/gain, etc). int.MinValue = "not seeded yet" — the very first
    // Refresh after a (re)bind must never be treated as damage.
    int lastHp = int.MinValue;

    void Awake()
    {
        rt = transform as RectTransform;
    }

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

        TickShake();
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
            BeginShake();

        lastHp = hp;

        hpMeter?.SetValues(hp, boundHealth.maxHealth);
        mpMeter?.SetValues(boundHealth.currentMana, boundHealth.maxMana);
    }

    void BeginShake()
    {
        if (rt == null)
            return;
        if (shakeTimeLeft <= 0f)
            shakeBasePosition = rt.anchoredPosition;
        shakeTimeLeft = shakeDuration;
    }

    void TickShake()
    {
        if (rt == null || shakeTimeLeft <= 0f)
            return;

        shakeTimeLeft -= Time.unscaledDeltaTime;
        if (shakeTimeLeft <= 0f)
        {
            rt.anchoredPosition = shakeBasePosition;
            return;
        }

        float damper = shakeTimeLeft / shakeDuration;
        float offsetX = Mathf.Sin(Time.unscaledTime * shakeFrequency) * shakeMagnitude * damper;
        rt.anchoredPosition = shakeBasePosition + new Vector2(offsetX, 0f);
    }
}
