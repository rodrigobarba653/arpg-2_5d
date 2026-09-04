using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Segmented "pip" meter — a row of squares/icons instead of a fill bar.
/// Each pip represents a fixed chunk of points (pointsPerPip). Pips fully
/// BEFORE the current value are fully filled, pips fully AFTER are fully
/// empty, and the ONE pip straddling the current value drains VERTICALLY
/// (top-to-bottom) as damage/cost eats into it — classic "potion vial"
/// look, not an instant on/off swap.
///
/// Colors are flat and fixed — one filledColor (e.g. red for HP, blue for
/// MP) and one emptyColor, both fully configurable per meter instance. No
/// gradient, no per-pip variation — every pip uses the same two colors.
///
/// Reusable for HP, MP, AP — anything expressed as current/max points. Spawns
/// its own pips from a prefab (PipVisual) so the pip count adjusts
/// automatically if max changes (leveling up, equipment, buffs).
/// </summary>
public class PipMeterWidget : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite filledSprite;
    public Sprite emptySprite;

    [Header("Color (flat, no gradient)")]
    [Tooltip("Color of the filled portion of every pip. E.g. red for HP, " +
             "blue for MP.")]
    public Color filledColor = Color.white;

    [Tooltip("Color of the empty portion/background of every pip.")]
    public Color emptyColor = Color.white;

    [Header("Size Emphasis")]
    [Tooltip("If true, the pip currently taking damage grows and pulses, and " +
             "pips that are fully empty shrink down — makes remaining points " +
             "readable at a glance. If false (e.g. for MP), every pip stays " +
             "at a constant size regardless of fill state.")]
    public bool useSizeEmphasis = false;

    [Header("Spawn")]
    [Tooltip("Parent for the spawned pips. Add a HorizontalLayoutGroup (or " +
             "GridLayoutGroup) here so they auto-space themselves.")]
    public RectTransform container;

    [Tooltip("Prefab for one pip — a PipVisual with a background Image and a " +
             "Filled/Vertical foreground Image.")]
    public PipVisual pipPrefab;

    [Header("Value Mapping")]
    [Tooltip("How many points each pip represents. E.g. max=100 with " +
             "pointsPerPip=20 → 5 pips. Lower this for more granular (more, " +
             "smaller) pips; raise it for fewer, chunkier ones.")]
    [Min(1)]
    public int pointsPerPip = 20;

    readonly List<PipVisual> pips = new List<PipVisual>();
    int spawnedForPipCount = -1;
    int activePipIndex = -1;

    /// <summary>Push new current/max values — rebuilds the pip row only if the
    /// pip COUNT changed (i.e. max changed enough to add/remove a pip), and
    /// always refreshes each pip's vertical fill fraction.</summary>
    public void SetValues(int current, int max)
    {
        int pipCount = Mathf.Max(1, Mathf.CeilToInt(max / (float)pointsPerPip));

        if (pipCount != spawnedForPipCount)
            RebuildPips(pipCount);

        // The "active" / front-line pip is whichever one `current` currently
        // sits within — computed HERE from the overall value, not derived
        // per-pip from its own fraction. Using (current-1)/pointsPerPip
        // (instead of current/pointsPerPip) keeps an exact-boundary value on
        // the LOWER pip: e.g. with pointsPerPip=20, current=100 lands on pip
        // index 4 (fully filled) rather than a nonexistent pip 5. Without
        // this, damage dealt in exact multiples of pointsPerPip would make
        // the front-line pip snap straight from 1.0 to the next pip's 1.0,
        // never passing through a fractional value — so nothing would ever
        // pulse, which was the original bug.
        activePipIndex = current <= 0 ? -1 : Mathf.Clamp((current - 1) / pointsPerPip, 0, pipCount - 1);

        for (int i = 0; i < pips.Count; i++)
        {
            if (pips[i] == null) continue;

            // Pip i covers the points range [i*pointsPerPip, (i+1)*pointsPerPip].
            // How much of THIS pip's own slice is still covered by `current`:
            //   fully before current → 1 (fully filled)
            //   fully after current  → 0 (fully drained)
            //   straddling current   → partial (the vertical drain).
            float remaining = current - i * pointsPerPip;
            float fillFraction = Mathf.Clamp01(remaining / pointsPerPip);

            bool isActivePip = i == activePipIndex;

            pips[i].Apply(emptySprite, emptyColor, filledSprite, filledColor, fillFraction, useSizeEmphasis, isActivePip);
        }
    }

    /// <summary>Triggers a brief shake on the pip currently representing the
    /// front line of remaining points (the same one that pulses). Call this
    /// when actual damage just happened — NOT on every SetValues refresh
    /// (which also fires on heals/mana spend). See PlayerHudBinder for the
    /// damage-detection logic that decides when to call this.</summary>
    public void ShakeActivePip()
    {
        if (activePipIndex < 0 || activePipIndex >= pips.Count) return;
        pips[activePipIndex]?.Shake();
    }

    /// <summary>Clears the row back to empty/no pips (e.g. on death, or when
    /// no character is bound). Safe to call repeatedly.</summary>
    public void Clear()
    {
        for (int i = 0; i < pips.Count; i++)
            if (pips[i] != null) Destroy(pips[i].gameObject);
        pips.Clear();
        spawnedForPipCount = -1;
    }

    void RebuildPips(int count)
    {
        spawnedForPipCount = count;

        for (int i = 0; i < pips.Count; i++)
            if (pips[i] != null) Destroy(pips[i].gameObject);
        pips.Clear();

        if (container == null || pipPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            var pip = Instantiate(pipPrefab, container);
            pip.gameObject.SetActive(true);
            pips.Add(pip);
        }
    }
}
