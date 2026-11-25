using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a UI Kit v2 chatbox hierarchy (9-slice frame + separate portrait + tail/mask).
/// Does not change dialogue progression or layout solver contracts.
/// </summary>
public static class DialogueBubbleKitBuilder
{
    public const float DefaultPortraitSize = 220f;
    public const float DefaultTailSize = 48f;
    /// <summary>Shrinks 9-slice corner/edge chrome (frame + body). Higher = smaller.</summary>
    // With sprite PPU 100 + canvas ref 100: borderPx / multiplier ≈ on-screen corner size.
    // 264/2.1 ≈ 126px corners / 100/2.1 ≈ 48px edge stroke.
    public const float DefaultSlicePpuMultiplier = 2.1f;

    public static DialogueChatboxView Build(
        RectTransform parent,
        string name,
        UiKitSprites kit,
        Vector2 size,
        DialogueChatboxTailDirection tailDirection,
        bool withPortraitSlot,
        string speaker,
        string body,
        Sprite portraitSprite = null,
        TMP_FontAsset font = null,
        bool withTail = true)
    {
        if (kit == null)
            throw new System.ArgumentNullException(nameof(kit));

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        var cg = go.GetComponent<CanvasGroup>();

        // Background then frame (9-slice only — no corner/edge mashup). Opaque fill.
        var bg = CreateSliced(
            "Background",
            go.transform,
            kit.dialogueBackground,
            Color.white,
            DefaultSlicePpuMultiplier);
        bg.material = null;
        StretchFull(bg.rectTransform);

        var frame = CreateSliced("Frame", go.transform, kit.dialogueFrame, Color.white, DefaultSlicePpuMultiplier);
        StretchFull(frame.rectTransform);

        Image maskGo = null;
        Image tailBgGo = null;
        Image tailOtGo = null;
        bool hasTail = withTail && (kit.TailBgFor(tailDirection) != null || kit.TailOtFor(tailDirection) != null);
        if (hasTail)
        {
            // Opaque fills: connection mask covers the seam under the stroke.
            if (kit.tailConnectionMask != null)
                maskGo = CreateSimple("TailMask", go.transform, kit.tailConnectionMask, UiKitSprites.Background, preserveAspect: false);

            if (kit.TailBgFor(tailDirection) != null)
            {
                tailBgGo = CreateSimple(
                    "TailBg",
                    go.transform,
                    kit.TailBgFor(tailDirection),
                    Color.white,
                    preserveAspect: false);
            }

            if (kit.TailOtFor(tailDirection) != null)
            {
                tailOtGo = CreateSimple(
                    "TailOt",
                    go.transform,
                    kit.TailOtFor(tailDirection),
                    Color.white,
                    preserveAspect: false);
            }

            PlaceTail(
                rt,
                maskGo != null ? maskGo.rectTransform : null,
                tailBgGo != null ? tailBgGo.rectTransform : null,
                tailOtGo != null ? tailOtGo.rectTransform : null,
                tailDirection,
                DefaultTailSize);
        }

        GameObject portraitRoot = null;
        Image portraitImage = null;
        if (withPortraitSlot)
        {
            portraitRoot = new GameObject("PortraitRoot", typeof(RectTransform));
            portraitRoot.transform.SetParent(go.transform, false);
            var prt = portraitRoot.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            // Keep 18px inset from the left edge of the bubble.
            prt.anchoredPosition = new Vector2(18f + DefaultPortraitSize * 0.5f, 0f);
            prt.sizeDelta = new Vector2(DefaultPortraitSize, DefaultPortraitSize);

            // No PortraitFrame — only the image (and optional soft bg) in the slot.
            if (kit.portraitBackground != null)
            {
                var pBg = CreateSimple("PortraitBackground", portraitRoot.transform, kit.portraitBackground, Color.white);
                StretchFull(pBg.rectTransform);
            }

            portraitImage = CreateSimple("PortraitImage", portraitRoot.transform, portraitSprite, Color.white);
            var pImgRt = portraitImage.rectTransform;
            pImgRt.anchorMin = new Vector2(0.5f, 0.5f);
            pImgRt.anchorMax = new Vector2(0.5f, 0.5f);
            pImgRt.pivot = new Vector2(0.5f, 0.5f);
            pImgRt.sizeDelta = new Vector2(DefaultPortraitSize * 0.9f, DefaultPortraitSize * 0.9f);
            portraitImage.preserveAspect = true;
            portraitImage.enabled = portraitSprite != null;

            // Start hidden — DialogueChatboxView.SetPortrait enables when a sprite exists.
            if (portraitSprite == null)
                portraitRoot.SetActive(false);
        }

        // Extra padding so text sits inside the coral frame, not on the stroke.
        float padL = 52f;
        float padR = 52f;
        float padB = 44f;
        float padT = 34f; // keep speaker clear of the top stroke
        float textLeftNoPortrait = padL;
        // Portrait column + gap so text clears the portrait slot.
        float textLeftWithPortrait = 18f + DefaultPortraitSize + 28f;
        float textLeft = (withPortraitSlot && portraitSprite != null) ? textLeftWithPortrait : textLeftNoPortrait;
        float headerHeight = 40f;
        float speakerBodyGap = 2f; // tighter name → body
        float speakerSize = size.y < 200f ? 24f : 28f;
        float bodySize = size.y < 200f ? 28f : 34f;

        var speakerTmp = CreateTmp("Speaker", go.transform, font, speakerSize, UiKitSprites.Cyan, FontStyles.Bold);
        var speakerRt = speakerTmp.rectTransform;
        speakerRt.anchorMin = new Vector2(0f, 1f);
        speakerRt.anchorMax = new Vector2(1f, 1f);
        speakerRt.pivot = new Vector2(0f, 1f);
        speakerRt.offsetMin = new Vector2(textLeft, -headerHeight - padT);
        speakerRt.offsetMax = new Vector2(-padR, -padT);
        speakerTmp.text = speaker ?? "";
        speakerTmp.enableWordWrapping = false;
        speakerTmp.overflowMode = TextOverflowModes.Ellipsis;

        var bodyTmp = CreateTmp("Body", go.transform, font, bodySize, Color.white, FontStyles.Normal);
        var bodyRt = bodyTmp.rectTransform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(textLeft, padB);
        bodyRt.offsetMax = new Vector2(-padR, -(headerHeight + padT + speakerBodyGap));
        bodyTmp.text = body ?? "";
        bodyTmp.enableWordWrapping = true;

        // Classic "more dialogue" caret — bottom-right, hidden until typewriter finishes.
        Image continueImg = null;
        if (kit.continueIndicator != null)
        {
            continueImg = CreateSimple(
                "ContinueIndicator",
                go.transform,
                kit.continueIndicator,
                UiKitSprites.Cyan,
                preserveAspect: true);
            var cRt = continueImg.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 0f);
            cRt.pivot = new Vector2(1f, 0f);
            cRt.sizeDelta = new Vector2(28f, 22f);
            cRt.anchoredPosition = new Vector2(-36f, 28f);
            continueImg.gameObject.SetActive(false);
        }

        var view = go.AddComponent<DialogueChatboxView>();
        view.rectTransform = rt;
        view.canvasGroup = cg;
        view.speakerLabel = speakerTmp;
        view.bodyLabel = bodyTmp;
        view.portraitImage = portraitImage;
        view.portraitRoot = portraitRoot;
        view.bubbleBackground = bg;
        view.continueIndicator = continueImg;
        view.textLeftWithPortrait = textLeftWithPortrait;
        view.textLeftWithoutPortrait = textLeftNoPortrait;

        var kitView = go.AddComponent<DialogueChatboxKitView>();
        kitView.frameImage = frame;
        kitView.backgroundImage = bg;
        kitView.tailBgImage = tailBgGo;
        kitView.tailOtImage = tailOtGo;
        kitView.tailMaskImage = maskGo;
        kitView.tailDirection = tailDirection;
        kitView.slicePixelsPerUnitMultiplier = DefaultSlicePpuMultiplier;
        kitView.kit = kit;
        kitView.ApplySliceScale();
        kitView.RefreshTailPlacement();

        return view;
    }

    public static void PlaceTail(
        RectTransform bubble,
        RectTransform tailBg,
        RectTransform tailOt,
        DialogueChatboxTailDirection direction,
        float size)
    {
        PlaceTail(bubble, null, tailBg, tailOt, direction, size, alongEdge: 0f);
    }

    public static void PlaceTail(
        RectTransform bubble,
        RectTransform mask,
        RectTransform tailBg,
        RectTransform tailOt,
        DialogueChatboxTailDirection direction,
        float size)
    {
        PlaceTail(bubble, mask, tailBg, tailOt, direction, size, alongEdge: 0f);
    }

    /// <param name="alongEdge">
    /// Offset along the attached edge from center (local units). Bottom/Top = X, Left/Right = Y.
    /// Used to slide the tip toward the speaker.
    /// </param>
    public static void PlaceTail(
        RectTransform bubble,
        RectTransform mask,
        RectTransform tailBg,
        RectTransform tailOt,
        DialogueChatboxTailDirection direction,
        float size,
        float alongEdge)
    {
        RectTransform primary = tailOt != null ? tailOt : tailBg;
        if (bubble == null || primary == null)
            return;

        // Prefer sizeDelta — rect.width can be 0 before canvas layout.
        float boxW = Mathf.Max(bubble.rect.width, bubble.sizeDelta.x);
        float boxH = Mathf.Max(bubble.rect.height, bubble.sizeDelta.y);
        float edgePad = 72f;
        float portraitClear = 190f;

        // Pivot on the attached edge. Same tuck for bg + ot so they stay aligned,
        // pulled INTO the bubble so the triangle joins the coral stroke.
        const float tuck = 18f;

        Vector2 sizeDelta;
        Vector2 anchor;
        Vector2 pivot;
        Vector2 pos;
        Vector2 maskSize;

        switch (direction)
        {
            case DialogueChatboxTailDirection.Top:
            {
                anchor = new Vector2(0.5f, 1f);
                pivot = new Vector2(0.5f, 0f);
                sizeDelta = new Vector2(size, size * (124f / 132f));
                float x = ClampAlong(alongEdge, boxW, edgePad, portraitClear: 0f);
                pos = new Vector2(x, -tuck);
                maskSize = new Vector2(size * 0.95f, 20f);
                break;
            }

            case DialogueChatboxTailDirection.Left:
            {
                anchor = new Vector2(0f, 0.5f);
                pivot = new Vector2(1f, 0.5f);
                sizeDelta = new Vector2(size * (124f / 132f), size);
                float y = ClampAlong(alongEdge, boxH, edgePad, portraitClear: 0f);
                pos = new Vector2(tuck, y);
                maskSize = new Vector2(20f, size * 0.95f);
                break;
            }

            case DialogueChatboxTailDirection.Right:
            {
                anchor = new Vector2(1f, 0.5f);
                pivot = new Vector2(0f, 0.5f);
                sizeDelta = new Vector2(size * (124f / 132f), size);
                float y = ClampAlong(alongEdge, boxH, edgePad, portraitClear: 0f);
                pos = new Vector2(-tuck, y);
                maskSize = new Vector2(20f, size * 0.95f);
                break;
            }

            default: // Bottom — slide along X toward speaker; stay clear of corners / portrait.
            {
                anchor = new Vector2(0.5f, 0f);
                pivot = new Vector2(0.5f, 1f);
                sizeDelta = new Vector2(size, size * (124f / 132f));
                float x = ClampAlong(alongEdge, boxW, edgePad, portraitClear);
                pos = new Vector2(x, tuck);
                maskSize = new Vector2(size * 0.95f, 20f);
                break;
            }
        }

        ApplyTailRect(tailBg, anchor, pivot, sizeDelta, pos);
        ApplyTailRect(tailOt, anchor, pivot, sizeDelta, pos);
        if (tailBg != null && tailOt != null)
            tailOt.SetSiblingIndex(tailBg.GetSiblingIndex() + 1);

        PlaceMask(mask, tailBg != null ? tailBg : primary, anchor, maskSize, Vector2.zero);
    }

    static float ClampAlong(float along, float boxSpan, float edgePad, float portraitClear)
    {
        float min = -boxSpan * 0.5f + Mathf.Max(edgePad, portraitClear * 0.35f);
        float max = boxSpan * 0.5f - edgePad;
        if (min > max)
            return 0f;
        return Mathf.Clamp(along, min, max);
    }

    static void ApplyTailRect(RectTransform tail, Vector2 anchor, Vector2 pivot, Vector2 sizeDelta, Vector2 pos)
    {
        if (tail == null)
            return;
        SetTailAnchors(tail, anchor, pivot);
        tail.sizeDelta = sizeDelta;
        tail.anchoredPosition = pos;
    }

    static void PlaceMask(RectTransform mask, RectTransform tail, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        if (mask == null || tail == null)
            return;
        mask.gameObject.SetActive(true);
        mask.SetSiblingIndex(Mathf.Max(0, tail.GetSiblingIndex() - 1));
        mask.anchorMin = anchor;
        mask.anchorMax = anchor;
        mask.pivot = anchor;
        mask.sizeDelta = size;
        mask.anchoredPosition = tail.anchoredPosition + offset;
    }

    static void SetTailAnchors(RectTransform rt, Vector2 anchor, Vector2 pivot)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
    }

    static Image CreateSliced(string name, Transform parent, Sprite sprite, Color color, float ppuMul)
    {
        var img = CreateSimple(name, parent, sprite, color, preserveAspect: false);
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, ppuMul);
        return img;
    }

    static Image CreateSimple(string name, Transform parent, Sprite sprite, Color color, bool preserveAspect = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        img.preserveAspect = preserveAspect;
        return img;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static TMP_Text CreateTmp(string name, Transform parent, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
