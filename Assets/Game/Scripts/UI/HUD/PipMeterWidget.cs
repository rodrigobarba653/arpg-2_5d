using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Continuous HP/MP bar. One track, one horizontal fill: fill amount is
/// current / max. The class name stays PipMeterWidget so the HUD prefabs
/// keep their existing script reference.
/// </summary>
public class PipMeterWidget : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Optional. Leave empty for a solid bar tinted by the colors below.")]
    public Sprite filledSprite;
    public Sprite emptySprite;

    [Header("Color")]
    public Color filledColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    public Color emptyColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    [Header("Unit Dividers")]
    [Tooltip("Draw a vertical mark every unitsPerDivider points. The mark that would sit on the right end is skipped.")]
    public bool drawUnitDividers;

    [Min(1)]
    public int unitsPerDivider = 10;

    [Tooltip("Width of each divider, in UI pixels.")]
    public float dividerWidth = 2f;

    public Color dividerColor = new Color(0.152941f, 0.098039f, 0.160784f, 1f);

    [Header("Damage Trail")]
    [Tooltip("A white fill sits behind the main bar. On damage it waits, then shrinks down to the new value.")]
    public bool useDamageTrail;
    public float damageTrailDelay = 1f;
    [Tooltip("How long the white bar takes to catch the main bar after the delay.")]
    public float damageTrailCatchup = 0.35f;
    public Color damageTrailColor = Color.white;

    Image track;
    Image fill;
    Image trail;
    RectTransform rt;
    readonly List<Image> dividers = new List<Image>();

    float trailAmount;
    float trailHoldUntil = -1f;
    bool trailInitialized;
    bool ready;

    void Awake()
    {
        EnsureBar();
    }

    /// <summary>Push new current/max. The filled portion stays on the left and
    /// shortens from the right as the value drops.</summary>
    public void SetValues(int current, int max)
    {
        EnsureBar();
        if (fill == null)
            return;

        float amount = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
        fill.fillAmount = amount;
        ApplyColors();
        UpdateDividers(max);
        NoteTrailTarget(amount);
    }

    /// <summary>Empties the bar when no character is bound.</summary>
    public void Clear()
    {
        EnsureBar();
        if (fill != null)
            fill.fillAmount = 0f;
        trailAmount = 0f;
        trailHoldUntil = -1f;
        trailInitialized = false;
        if (trail != null)
            trail.fillAmount = 0f;
        UpdateDividers(0);
    }

    void EnsureBar()
    {
        if (ready)
            return;

        rt = transform as RectTransform;

        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;

        // A stretched rect is positioned by its parent (the frame windows).
        // Only invent a size when this meter is still a loose top-left point.
        if (rt != null && rt.anchorMin == rt.anchorMax && rt.sizeDelta.x < 8f)
            rt.sizeDelta = new Vector2(220f, 16f);

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "Background")
                child.gameObject.SetActive(false);
        }

        track = CreateImage("Track", transform);
        trail = CreateImage("Trail", track.transform);
        fill = CreateImage("Fill", track.transform);
        Stretch(track.rectTransform, 0f);
        Stretch(trail.rectTransform, 0f);
        Stretch(fill.rectTransform, 0f);
        trail.transform.SetSiblingIndex(0);
        fill.transform.SetAsLastSibling();

        track.type = Image.Type.Simple;
        trail.type = Image.Type.Filled;
        trail.fillMethod = Image.FillMethod.Horizontal;
        trail.fillOrigin = (int)Image.OriginHorizontal.Left;
        trail.color = damageTrailColor;
        trail.gameObject.SetActive(useDamageTrail);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        // Left origin keeps the remaining points on the left. Lowering the
        // amount eats the bar from the right edge toward the left.
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;

        ApplyColors();
        ready = true;
    }

    void ApplyColors()
    {
        // A Filled image with no sprite draws a full rectangle and ignores
        // fillAmount, so the bar would never follow the current points.
        Sprite solid = SolidSprite();

        if (track != null)
        {
            track.sprite = emptySprite != null ? emptySprite : solid;
            track.color = emptyColor;
        }

        if (trail != null)
        {
            trail.sprite = filledSprite != null ? filledSprite : solid;
            trail.color = damageTrailColor;
            trail.type = Image.Type.Filled;
            trail.fillMethod = Image.FillMethod.Horizontal;
            trail.fillOrigin = (int)Image.OriginHorizontal.Left;
            trail.gameObject.SetActive(useDamageTrail);
        }

        if (fill != null)
        {
            fill.sprite = filledSprite != null ? filledSprite : solid;
            fill.color = filledColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    void NoteTrailTarget(float amount)
    {
        if (!useDamageTrail || trail == null)
            return;

        if (!trailInitialized)
        {
            trailAmount = amount;
            trailInitialized = true;
            trailHoldUntil = -1f;
        }
        else if (amount < trailAmount)
        {
            trailHoldUntil = Time.unscaledTime + Mathf.Max(0f, damageTrailDelay);
        }
        else if (amount > trailAmount)
        {
            trailAmount = amount;
            trailHoldUntil = -1f;
        }

        trail.fillAmount = trailAmount;
    }

    void TickTrail()
    {
        if (!useDamageTrail || trail == null || fill == null)
            return;

        float target = fill.fillAmount;
        if (trailHoldUntil > 0f && Time.unscaledTime < trailHoldUntil)
        {
            trail.fillAmount = trailAmount;
            return;
        }

        float catchup = Mathf.Max(0.05f, damageTrailCatchup);
        trailAmount = Mathf.MoveTowards(trailAmount, target, Time.unscaledDeltaTime / catchup);
        trail.fillAmount = trailAmount;
    }

    void UpdateDividers(int max)
    {
        int count = 0;
        if (drawUnitDividers && unitsPerDivider > 0 && max > unitsPerDivider)
            count = (max - 1) / unitsPerDivider;

        Sprite solid = SolidSprite();
        float width = Mathf.Max(1f, dividerWidth);

        while (dividers.Count < count)
        {
            Image mark = CreateImage("Divider" + dividers.Count, transform);
            mark.raycastTarget = false;
            dividers.Add(mark);
        }

        for (int i = 0; i < dividers.Count; i++)
        {
            Image mark = dividers[i];
            if (mark == null)
                continue;

            bool show = i < count;
            mark.gameObject.SetActive(show);
            if (!show)
                continue;

            mark.sprite = solid;
            mark.color = dividerColor;
            mark.type = Image.Type.Simple;
            mark.transform.SetAsLastSibling();

            // Mark i sits on point (i+1)*units, never on the right end.
            float fraction = (i + 1) * unitsPerDivider / (float)max;
            RectTransform markRect = mark.rectTransform;
            markRect.anchorMin = new Vector2(fraction, 0f);
            markRect.anchorMax = new Vector2(fraction, 1f);
            markRect.pivot = new Vector2(0.5f, 0.5f);
            markRect.sizeDelta = new Vector2(width, 0f);
            markRect.anchoredPosition = Vector2.zero;
        }
    }

    static Sprite solidSprite;

    static Sprite SolidSprite()
    {
        if (solidSprite != null)
            return solidSprite;

        Texture2D tex = Texture2D.whiteTexture;
        solidSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return solidSprite;
    }

    static Image CreateImage(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();
            if (found != null)
                return found;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    void Update()
    {
        TickTrail();
    }
}
