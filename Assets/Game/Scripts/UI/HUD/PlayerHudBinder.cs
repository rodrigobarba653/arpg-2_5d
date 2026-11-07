using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the HUD's HP/MP bars in sync with whichever party member is
/// currently active — rebinds on swap, and self-heals if it ever misses the
/// swap event (same pattern as MagicCastInput's active-member tracking).
///
/// Also detects actual HP loss (as opposed to healing, or OnStatsChanged
/// firing for an unrelated reason like mana spend) and shakes this whole
/// HUD group: frame, health bar, and mana bar together. The plate behind
/// the bars flashes for the same hold as the white damage trail.
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

    [Header("Hit Flash")]
    [Tooltip("Plate behind the bars. Painted while the white damage trail is holding.")]
    public Image hitFlashImage;
    [Tooltip("Draws the plate's shape in Hit Flash Color. A normal tint leaves the dark plate looking unchanged.")]
    public Material hitFlashMaterial;
    public Color hitFlashColor = new Color(0.890196f, 0.180392f, 0.164706f, 1f);

    PlayerHealth boundHealth;
    RectTransform rt;
    float shakeTimeLeft;
    Vector2 shakeBasePosition;
    Color hitFlashRest = Color.white;
    float hitFlashTimeLeft;
    bool hitFlashRestSaved;

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
        EndHitFlash();
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
        TickHitFlash();
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
        {
            BeginShake();
            BeginHitFlash();
        }

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

    void BeginHitFlash()
    {
        if (hitFlashImage == null)
        {
            Transform plate = transform.Find("HPBarBg");
            if (plate != null)
                hitFlashImage = plate.GetComponent<Image>();
        }

        if (hitFlashImage == null)
            return;

        if (!hitFlashRestSaved)
        {
            hitFlashRest = hitFlashImage.color;
            hitFlashRestSaved = true;
        }

        EnsureFlashMaterial();
        if (hitFlashMaterial != null)
            hitFlashImage.material = hitFlashMaterial;

        hitFlashImage.color = hitFlashColor;
        float delay = hpMeter != null ? hpMeter.damageTrailDelay : 0.4f;
        hitFlashTimeLeft = Mathf.Max(0f, delay);
    }

    void EnsureFlashMaterial()
    {
        if (hitFlashMaterial != null && hitFlashMaterial.shader != null
            && hitFlashMaterial.shader.name != "Hidden/InternalErrorShader")
            return;

        Shader shader = Shader.Find("Game/UI/SpritePaint");
        if (shader == null)
            return;

        hitFlashMaterial = new Material(shader);
    }

    void TickHitFlash()
    {
        if (hitFlashImage == null || hitFlashTimeLeft <= 0f)
            return;

        hitFlashTimeLeft -= Time.unscaledDeltaTime;
        if (hitFlashTimeLeft <= 0f)
            EndHitFlash();
    }

    void EndHitFlash()
    {
        hitFlashTimeLeft = 0f;
        if (hitFlashImage == null || !hitFlashRestSaved)
            return;

        hitFlashImage.color = hitFlashRest;
        hitFlashImage.material = null;
        hitFlashRestSaved = false;
    }
}
