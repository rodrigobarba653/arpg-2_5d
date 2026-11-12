using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Side actually used for one choice-menu open. LowerCenter is the bottom of the safe rect.
/// Below and Above sit against the player chatbox.
/// </summary>
public enum DialogueChoiceMenuSide
{
    LowerCenter = 0,
    Below = 1,
    Above = 2,
}

/// <summary>
/// Screen pivot for the choice panel. The panel is not a speaker and this does not write bubbles.
/// </summary>
public struct DialogueChoiceLayoutResult
{
    /// <summary>Bottom-center pivot, in screen pixels.</summary>
    public Vector2 pivotScreen;
    public DialogueChoiceMenuSide used;
    public bool fellBackToLowerCenter;
}

/// <summary>
/// Returns a screen position for the choice panel. Does not read or write speaker bubbles
/// except through the rects the caller already measured.
/// </summary>
public static class DialogueChoiceLayout
{
    /// <summary>
    /// Bottom-center pivot, in screen pixels, clamped inside <paramref name="safeScreen"/>.
    /// </summary>
    public static Vector2 ResolveBottomCenter(
        DialogueChoicePresentation presentation,
        Rect safeScreen,
        Vector2 sizeScreen,
        float canvasScale)
    {
        float scale = canvasScale > 0.01f ? canvasScale : 1f;
        float margin = Mathf.Max(0f, presentation.marginRef) * scale;
        Vector2 nudge = presentation.offsetRef * scale;
        Vector2 raw = new Vector2(
            safeScreen.center.x + nudge.x,
            safeScreen.yMin + margin + nudge.y);
        return ClampBottomCenter(raw, sizeScreen, safeScreen);
    }

    /// <summary>
    /// Places the panel once. Each side is clamped into the safe rect first.
    /// NearPlayerChatbox then keeps below if that rect misses every bubble, otherwise above.
    /// If both still cover a bubble, the side with less overlap wins.
    /// A missing player bubble uses LowerCenter for this open.
    /// </summary>
    public static DialogueChoiceLayoutResult Resolve(
        DialogueChoicePresentation presentation,
        Rect safeScreen,
        Vector2 sizeScreen,
        float canvasScale,
        bool hasPlayerBubble,
        Rect playerBubble,
        IList<Rect> otherBubbles)
    {
        if (presentation.placement != DialogueChoicePlacement.NearPlayerChatbox ||
            !hasPlayerBubble ||
            playerBubble.width < 1f ||
            playerBubble.height < 1f)
        {
            return new DialogueChoiceLayoutResult
            {
                pivotScreen = ResolveBottomCenter(presentation, safeScreen, sizeScreen, canvasScale),
                used = DialogueChoiceMenuSide.LowerCenter,
                fellBackToLowerCenter = presentation.placement == DialogueChoicePlacement.NearPlayerChatbox,
            };
        }

        float scale = canvasScale > 0.01f ? canvasScale : 1f;
        float gap = Mathf.Max(0f, presentation.gapFromBubbleRef) * scale;
        Vector2 nudge = presentation.offsetRef * scale;
        float centerX = playerBubble.center.x + nudge.x;
        float height = Mathf.Max(0f, sizeScreen.y);

        Vector2 below = ClampBottomCenter(
            new Vector2(centerX, playerBubble.yMin - gap - height + nudge.y),
            sizeScreen,
            safeScreen);
        Vector2 above = ClampBottomCenter(
            new Vector2(centerX, playerBubble.yMax + gap + nudge.y),
            sizeScreen,
            safeScreen);
        Rect belowRect = BottomCenterRect(below, sizeScreen);
        Rect aboveRect = BottomCenterRect(above, sizeScreen);

        // Judge the rect that will actually be shown. Clamping first stops a
        // below-the-fold menu from winning and then being dragged onto the bubble.
        bool belowFits = Fits(belowRect, safeScreen, playerBubble, otherBubbles);
        bool aboveFits = Fits(aboveRect, safeScreen, playerBubble, otherBubbles);

        Vector2 chosen;
        DialogueChoiceMenuSide side;
        if (belowFits)
        {
            chosen = below;
            side = DialogueChoiceMenuSide.Below;
        }
        else if (aboveFits)
        {
            chosen = above;
            side = DialogueChoiceMenuSide.Above;
        }
        else
        {
            float belowScore = OverlapScore(belowRect, safeScreen, playerBubble, otherBubbles);
            float aboveScore = OverlapScore(aboveRect, safeScreen, playerBubble, otherBubbles);
            if (aboveScore < belowScore)
            {
                chosen = above;
                side = DialogueChoiceMenuSide.Above;
            }
            else
            {
                chosen = below;
                side = DialogueChoiceMenuSide.Below;
            }
        }

        return new DialogueChoiceLayoutResult
        {
            pivotScreen = chosen,
            used = side,
            fellBackToLowerCenter = false,
        };
    }

    static bool Fits(Rect menu, Rect safe, Rect player, IList<Rect> others)
    {
        return OverflowArea(menu, safe) <= 1f &&
               OverlapArea(menu, player) <= 1f &&
               OthersOverlap(menu, others) <= 1f;
    }

    static float OverlapScore(Rect menu, Rect safe, Rect player, IList<Rect> others)
    {
        return OverflowArea(menu, safe) + OverlapArea(menu, player) + OthersOverlap(menu, others);
    }

    static float OthersOverlap(Rect menu, IList<Rect> others)
    {
        if (others == null)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < others.Count; i++)
            sum += OverlapArea(menu, others[i]);
        return sum;
    }

    static float OverflowArea(Rect menu, Rect safe)
    {
        float inside = OverlapArea(menu, safe);
        return Mathf.Max(0f, menu.width * menu.height - inside);
    }

    static float OverlapArea(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        if (w <= 0f || h <= 0f)
            return 0f;
        return w * h;
    }

    static Rect BottomCenterRect(Vector2 pivot, Vector2 size)
    {
        float width = Mathf.Max(0f, size.x);
        float height = Mathf.Max(0f, size.y);
        return new Rect(pivot.x - width * 0.5f, pivot.y, width, height);
    }

    static Vector2 ClampBottomCenter(Vector2 pivot, Vector2 size, Rect safe)
    {
        float x = pivot.x;
        float y = pivot.y;
        float halfW = Mathf.Max(0f, size.x) * 0.5f;
        float minX = safe.xMin + halfW;
        float maxX = safe.xMax - halfW;
        if (minX <= maxX)
            x = Mathf.Clamp(x, minX, maxX);
        else
            x = safe.center.x;

        float minY = safe.yMin;
        float maxY = safe.yMax - Mathf.Max(0f, size.y);
        if (minY <= maxY)
            y = Mathf.Clamp(y, minY, maxY);
        else
            y = safe.yMin;

        return new Vector2(x, y);
    }
}
