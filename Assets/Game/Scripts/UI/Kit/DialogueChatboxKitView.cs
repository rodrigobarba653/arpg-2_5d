using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional UI Kit v2 chrome on a <see cref="DialogueChatboxView"/>.
/// Tail is split into bg (fill) + ot (outline); frame/background are 9-slice Images.
/// </summary>
[DisallowMultipleComponent]
public class DialogueChatboxKitView : MonoBehaviour
{
    public UiKitSprites kit;
    public Image backgroundImage;
    public Image frameImage;
    public Image tailBgImage;
    public Image tailOtImage;
    public Image tailMaskImage;
    public DialogueChatboxTailDirection tailDirection = DialogueChatboxTailDirection.Bottom;

    /// <summary>
    /// Higher = smaller 9-slice corners / edge chrome (frame + body PNGs).
    /// Always overwritten from <see cref="DialogueBubbleKitBuilder.DefaultSlicePpuMultiplier"/> so Play Mode
    /// / serialized leftovers cannot stick on an old value.
    /// </summary>
    public float slicePixelsPerUnitMultiplier = DialogueBubbleKitBuilder.DefaultSlicePpuMultiplier;

    public void SetTailDirection(DialogueChatboxTailDirection direction)
    {
        tailDirection = direction;
        if (kit != null)
        {
            if (tailBgImage != null)
                tailBgImage.sprite = kit.TailBgFor(direction);
            if (tailOtImage != null)
                tailOtImage.sprite = kit.TailOtFor(direction);
        }
        RefreshTailPlacement();
    }

    public void RefreshTailPlacement()
    {
        var bubble = transform as RectTransform;
        if (bubble == null)
            return;
        if (tailBgImage == null && tailOtImage == null)
            return;

        DialogueBubbleKitBuilder.PlaceTail(
            bubble,
            tailBgImage != null ? tailBgImage.rectTransform : null,
            tailOtImage != null ? tailOtImage.rectTransform : null,
            tailDirection,
            DialogueBubbleKitBuilder.DefaultTailSize);
    }

    void Awake()
    {
        ApplySliceScale();
    }

    void OnEnable()
    {
        ApplySliceScale();
    }

    public void ApplySliceScale()
    {
        // Const wins — ignore stale serialized field from prior Play sessions.
        slicePixelsPerUnitMultiplier = DialogueBubbleKitBuilder.DefaultSlicePpuMultiplier;
        HardenSliced(frameImage, slicePixelsPerUnitMultiplier);
        HardenSliced(backgroundImage, slicePixelsPerUnitMultiplier);
        if (backgroundImage != null)
        {
            var c = backgroundImage.color;
            c.a = UiKitSprites.FillOpacity;
            backgroundImage.color = c;
            backgroundImage.fillCenter = true;
        }

        if (tailBgImage != null)
        {
            var c = tailBgImage.color;
            c.a = UiKitSprites.FillOpacity;
            tailBgImage.color = c;
        }
    }

    static void HardenSliced(Image img, float ppuMul)
    {
        if (img == null || img.type != Image.Type.Sliced)
            return;
        img.preserveAspect = false;
        img.fillCenter = true;
        // Fixed corner size: raise multiplier to shrink decorative borders without deforming them.
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, ppuMul);
    }
}
