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

    Vector2 lastAimScreen;
    bool hasAimTarget;
    float alongEdge;

    const float AxisBias = 1.15f;

    public void ClearTailAim()
    {
        hasAimTarget = false;
        alongEdge = 0f;
    }

    /// <summary>
    /// Pick Bottom/Top/Left/Right and slide the tip along that edge toward the speaker.
    /// </summary>
    public void AimTailAtScreen(Vector2 speakerScreen)
    {
        lastAimScreen = speakerScreen;
        hasAimTarget = true;
        ApplyAimFromStored();
    }

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
        if (hasAimTarget)
        {
            ApplyAimFromStored();
            return;
        }

        PlaceCurrent(alongEdge);
    }

    void ApplyAimFromStored()
    {
        var bubble = transform as RectTransform;
        if (bubble == null)
            return;

        Rect screenRect = GetBubbleScreenRect(bubble);
        if (screenRect.width < 1f || screenRect.height < 1f)
        {
            PlaceCurrent(alongEdge);
            return;
        }

        Vector2 center = screenRect.center;
        Vector2 d = lastAimScreen - center;

        float boxW = Mathf.Max(bubble.rect.width, bubble.sizeDelta.x);
        float boxH = Mathf.Max(bubble.rect.height, bubble.sizeDelta.y);
        float sx = boxW / screenRect.width;
        float sy = boxH / screenRect.height;

        DialogueChatboxTailDirection next;
        float along;
        bool verticalDominant = Mathf.Abs(d.y) * AxisBias >= Mathf.Abs(d.x);

        if (verticalDominant)
        {
            next = d.y < 0f
                ? DialogueChatboxTailDirection.Bottom
                : DialogueChatboxTailDirection.Top;
            along = d.x * sx;
        }
        else
        {
            next = d.x < 0f
                ? DialogueChatboxTailDirection.Left
                : DialogueChatboxTailDirection.Right;
            along = d.y * sy;
        }

        alongEdge = along;
        if (next != tailDirection)
        {
            tailDirection = next;
            if (kit != null)
            {
                if (tailBgImage != null)
                    tailBgImage.sprite = kit.TailBgFor(next);
                if (tailOtImage != null)
                    tailOtImage.sprite = kit.TailOtFor(next);
            }
        }

        PlaceCurrent(alongEdge);
    }

    void PlaceCurrent(float along)
    {
        var bubble = transform as RectTransform;
        if (bubble == null)
            return;
        if (tailBgImage == null && tailOtImage == null)
            return;

        DialogueBubbleKitBuilder.PlaceTail(
            bubble,
            tailMaskImage != null ? tailMaskImage.rectTransform : null,
            tailBgImage != null ? tailBgImage.rectTransform : null,
            tailOtImage != null ? tailOtImage.rectTransform : null,
            tailDirection,
            DialogueBubbleKitBuilder.DefaultTailSize,
            along);
    }

    static Rect GetBubbleScreenRect(RectTransform bubble)
    {
        Canvas.ForceUpdateCanvases();
        Vector3[] corners = new Vector3[4];
        bubble.GetWorldCorners(corners);

        float xmin = corners[0].x;
        float xmax = corners[0].x;
        float ymin = corners[0].y;
        float ymax = corners[0].y;
        for (int i = 1; i < 4; i++)
        {
            xmin = Mathf.Min(xmin, corners[i].x);
            xmax = Mathf.Max(xmax, corners[i].x);
            ymin = Mathf.Min(ymin, corners[i].y);
            ymax = Mathf.Max(ymax, corners[i].y);
        }

        return Rect.MinMaxRect(xmin, ymin, xmax, ymax);
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
            c.a = 1f;
            backgroundImage.color = c;
            backgroundImage.fillCenter = true;
        }

        if (tailBgImage != null)
        {
            var c = tailBgImage.color;
            c.a = 1f;
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
