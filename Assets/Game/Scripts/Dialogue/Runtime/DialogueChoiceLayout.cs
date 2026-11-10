using UnityEngine;

/// <summary>
/// Returns a screen position for the choice panel. Does not read or write speaker bubbles.
/// Step 3 implements LowerCenter. NearPlayerChatbox uses that same placement until Step 7.
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

        float x = safeScreen.center.x + nudge.x;
        float y = safeScreen.yMin + margin + nudge.y;

        float halfW = Mathf.Max(0f, sizeScreen.x) * 0.5f;
        float minX = safeScreen.xMin + halfW;
        float maxX = safeScreen.xMax - halfW;
        if (minX <= maxX)
            x = Mathf.Clamp(x, minX, maxX);
        else
            x = safeScreen.center.x;

        float minY = safeScreen.yMin;
        float maxY = safeScreen.yMax - Mathf.Max(0f, sizeScreen.y);
        if (minY <= maxY)
            y = Mathf.Clamp(y, minY, maxY);
        else
            y = safeScreen.yMin;

        return new Vector2(x, y);
    }
}
