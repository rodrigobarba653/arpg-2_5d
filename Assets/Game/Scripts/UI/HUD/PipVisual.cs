using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual for ONE pip in a PipMeterWidget row — two stacked Images:
///   - background: the empty-state look, always fully visible underneath.
///   - fill: the filled-state look, Image.Type = Filled / Method = Vertical,
///     so it visually drains top-to-bottom as its point range empties out
///     (fillAmount 1 = full pip, 0 = fully drained).
///
/// Colors are flat and fixed (no gradient) — PipMeterWidget passes the same
/// filledColor/emptyColor to every pip each refresh.
///
/// Optional SIZE EMPHASIS (enabled per-meter, e.g. HP yes / MP no): the pip
/// currently representing the "front line" of remaining points — passed in
/// explicitly as isActivePip by PipMeterWidget, NOT derived from this pip's
/// own fraction — grows and pulses continuously, whether it happens to be
/// exactly full or partially drained. Deriving it from the pip's own fraction
/// alone was the bug: if damage always lands in exact multiples of
/// pointsPerPip, the front-line pip snaps straight from 1.0 to the next pip's
/// 1.0 and NEVER passes through a fractional value, so nothing ever pulsed.
/// Fully-empty pips (behind the front line) shrink down instead.
///
/// Also supports a one-shot Shake() — a brief positional jitter, independent
/// of the pulse system, meant to be triggered by the owner on an actual hit.
/// </summary>
public class PipVisual : MonoBehaviour
{
    [Header("Layers")]
    [Tooltip("Bottom layer — the pip's empty-state background, always fully shown.")]
    public Image background;

    [Tooltip("Top layer — the pip's filled-state overlay. Must have Image Type " +
             "set to 'Filled' with Fill Method 'Vertical' in the Inspector.")]
    public Image fill;

    [Header("Size Emphasis Tuning (only used when the parent meter enables it)")]
    [Tooltip("Scale once this pip is fully empty. 0.25 = 75% smaller.")]
    [Range(0.1f, 1f)] public float emptyScale = 0.25f;

    [Tooltip("Scale while this pip is the front-line/active pip.")]
    [Range(1f, 2f)] public float damagedScale = 1.4f;

    [Tooltip("Extra size added on top of damagedScale by the pulse, at the " +
             "peak of the wave.")]
    [Range(0f, 0.6f)] public float pulseAmplitude = 0.2f;

    [Tooltip("Pulse speed (radians/sec) while active.")]
    [Range(0.5f, 10f)] public float pulseSpeed = 5f;

    [Tooltip("How quickly the pip's base scale settles toward its target " +
             "(empty/active/normal). Higher = snappier.")]
    [Range(1f, 20f)] public float scaleLerpSpeed = 10f;

    [Header("Hit Shake")]
    [Tooltip("How far (in UI pixels) the pip jitters horizontally when " +
             "Shake() is called.")]
    [Range(0f, 20f)] public float shakeMagnitude = 6f;

    [Tooltip("How long the shake lasts.")]
    [Range(0.05f, 1f)] public float shakeDuration = 0.3f;

    [Tooltip("Wobbles per second while shaking.")]
    [Range(1f, 30f)] public float shakeFrequency = 18f;

    RectTransform rt;

    bool emphasisEnabled;
    float targetBaseScale = 1f;
    float currentBaseScale = 1f;
    bool pulsing;
    float pulseTime;

    float shakeTimeLeft;
    Vector2 shakeBasePosition;

    void Awake()
    {
        rt = transform as RectTransform;
    }

    /// <summary>Apply this pip's current visual state.
    /// fillFraction: 0 (fully drained) .. 1 (fully filled).
    /// useSizeEmphasis: set by the parent PipMeterWidget — false keeps this
    /// pip at a constant normal scale regardless of fill state (e.g. MP).
    /// isActivePip: true for the ONE pip representing the current front line
    /// of remaining points, as decided by PipMeterWidget (not derived from
    /// fillFraction alone — see class doc for why).</summary>
    public void Apply(Sprite emptySprite, Color emptyColor, Sprite filledSprite, Color filledColor,
                       float fillFraction, bool useSizeEmphasis, bool isActivePip)
    {
        if (background != null)
        {
            background.sprite = emptySprite;
            background.color = emptyColor;
        }

        if (fill != null)
        {
            fill.sprite = filledSprite;
            fill.color = filledColor;
            fill.fillAmount = fillFraction;
            fill.gameObject.SetActive(fillFraction > 0f);
        }

        emphasisEnabled = useSizeEmphasis;

        if (!emphasisEnabled)
        {
            // Emphasis off (e.g. MP) — stay at a constant normal scale.
            targetBaseScale = 1f;
            currentBaseScale = 1f;
            pulsing = false;
            if (rt != null) rt.localScale = Vector3.one;
            return;
        }

        bool isEmpty = fillFraction <= 0f;

        targetBaseScale = isActivePip ? damagedScale : (isEmpty ? emptyScale : 1f);

        pulsing = isActivePip;
        if (!pulsing) pulseTime = 0f;
    }

    /// <summary>Triggers a brief horizontal jitter, independent of the pulse
    /// system — call this when a real hit lands. Safe to call repeatedly;
    /// re-triggering mid-shake just resets the timer instead of drifting the
    /// pip off its true layout position.</summary>
    public void Shake()
    {
        if (rt == null) return;
        if (shakeTimeLeft <= 0f) shakeBasePosition = rt.anchoredPosition;
        shakeTimeLeft = shakeDuration;
    }

    void Update()
    {
        if (rt == null) return;

        if (emphasisEnabled)
        {
            currentBaseScale = Mathf.Lerp(currentBaseScale, targetBaseScale, Time.unscaledDeltaTime * scaleLerpSpeed);

            float s = currentBaseScale;
            if (pulsing)
            {
                pulseTime += Time.unscaledDeltaTime * pulseSpeed;
                float wave = (Mathf.Sin(pulseTime) + 1f) * 0.5f; // 0..1
                s += wave * pulseAmplitude;
            }

            rt.localScale = new Vector3(s, s, 1f);
        }

        if (shakeTimeLeft > 0f)
        {
            shakeTimeLeft -= Time.unscaledDeltaTime;

            if (shakeTimeLeft <= 0f)
            {
                rt.anchoredPosition = shakeBasePosition;
            }
            else
            {
                float damper = shakeTimeLeft / shakeDuration; // fades out linearly
                float offsetX = Mathf.Sin(Time.unscaledTime * shakeFrequency) * shakeMagnitude * damper;
                rt.anchoredPosition = shakeBasePosition + new Vector2(offsetX, 0f);
            }
        }
    }
}
