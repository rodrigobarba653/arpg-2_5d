using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds UI Kit v2 item notification banners (icon left, text right). No pointer.
/// Uses the same 9-slice PPU scale as chatboxes, with a shorter banner height.
/// Width hugs label content (+ padding), capped by <see cref="DefaultWidth"/>.
/// </summary>
public static class NotificationKitBuilder
{
    /// <summary>Same corner scale as <see cref="DialogueBubbleKitBuilder.DefaultSlicePpuMultiplier"/>.</summary>
    public const float SlicePpuMultiplier = DialogueBubbleKitBuilder.DefaultSlicePpuMultiplier;

    /// <summary>Shorter than chatbox (160–220). Fits notification art T/B borders at chatbox PPU.</summary>
    public const float DefaultHeight = 100f;

    /// <summary>Max hug width.</summary>
    public const float DefaultWidth = 720f;

    public const float MinWidth = 320f;

    /// <summary>Extra space after the measured label before the right chrome.</summary>
    public const float ContentPadX = 18f;

    // Notification PNG borders (L B R T) — keep in sync with UiKitImportAndTestBuilder.
    const float BorderLeft = 230f;
    const float BorderRight = 100f;

    public static RectTransform Build(
        RectTransform parent,
        string name,
        UiKitSprites kit,
        Vector2 size,
        string text,
        Sprite icon,
        TMP_FontAsset font = null)
    {
        if (kit == null)
            throw new System.ArgumentNullException(nameof(kit));

        float height = size.y > 1f ? size.y : DefaultHeight;
        float maxWidth = size.x > 1f ? size.x : DefaultWidth;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(maxWidth, height);

        var bg = CreateSliced("Background", go.transform, kit.notificationBackground);
        StretchFull(bg.rectTransform);
        // Sprite may have been authored with soft alpha — keep Image fully opaque.
        var bgColor = bg.color;
        bgColor.a = 1f;
        bg.color = bgColor;

        var frame = CreateSliced("Frame", go.transform, kit.notificationFrame);
        StretchFull(frame.rectTransform);

        float leftChrome = BorderLeft / SlicePpuMultiplier;
        float rightChrome = BorderRight / SlicePpuMultiplier;

        // Icon cluster sits in the left bulb of the notification frame (2px below center).
        var iconRoot = new GameObject("IconRoot", typeof(RectTransform));
        iconRoot.transform.SetParent(go.transform, false);
        var iconRt = iconRoot.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        float iconSize = Mathf.Min(height * 0.72f, leftChrome * 0.78f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        iconRt.anchoredPosition = new Vector2(leftChrome * 0.5f, -2f);

        if (kit.notificationIconFrame != null)
        {
            var iconFrame = CreateSimple("IconFrame", iconRoot.transform, kit.notificationIconFrame);
            StretchFull(iconFrame.rectTransform);
        }

        var iconImg = CreateSimple("Icon", iconRoot.transform, icon);
        var iRt = iconImg.rectTransform;
        iRt.anchorMin = iRt.anchorMax = new Vector2(0.5f, 0.5f);
        iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.sizeDelta = new Vector2(iconSize * 0.62f, iconSize * 0.62f);
        iconImg.preserveAspect = true;
        iconImg.enabled = icon != null;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.fontSize = Mathf.Clamp(height * 0.28f, 18f, 28f);
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = text ?? "";

        var lRt = label.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        float leftPad = leftChrome + 10f;
        float rightPad = Mathf.Max(24f, rightChrome * 0.35f);
        float vPad = Mathf.Max(6f, height * 0.08f);
        lRt.offsetMin = new Vector2(leftPad, vPad);
        lRt.offsetMax = new Vector2(-rightPad, -vPad);

        ApplyHugWidth(rt, maxWidth);
        return rt;
    }

    /// <summary>
    /// Resize banner width to hug the Label text (+ padding), clamped to [MinWidth, maxWidth].
    /// </summary>
    public static void ApplyHugWidth(RectTransform banner, float maxWidth = DefaultWidth)
    {
        if (banner == null)
            return;

        var label = banner.Find("Label")?.GetComponent<TMP_Text>();
        if (label == null)
            return;

        float leftChrome = BorderLeft / SlicePpuMultiplier;
        float rightChrome = BorderRight / SlicePpuMultiplier;
        float leftPad = leftChrome + 10f;
        float rightPad = Mathf.Max(24f, rightChrome * 0.35f) + ContentPadX;

        label.ForceMeshUpdate();
        Vector2 pref = label.GetPreferredValues(
            string.IsNullOrEmpty(label.text) ? " " : label.text,
            4096f,
            banner.sizeDelta.y);

        float hug = leftPad + pref.x + rightPad;
        float width = Mathf.Clamp(hug, MinWidth, Mathf.Max(MinWidth, maxWidth));
        banner.sizeDelta = new Vector2(width, banner.sizeDelta.y);

        // Keep hint (if any) matched to the new width.
        var hint = banner.Find("Hint") as RectTransform;
        if (hint != null)
            hint.sizeDelta = new Vector2(width, hint.sizeDelta.y);
    }

    static Image CreateSliced(string name, Transform parent, Sprite sprite)
    {
        var img = CreateSimple(name, parent, sprite);
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, SlicePpuMultiplier);
        return img;
    }

    static Image CreateSimple(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
