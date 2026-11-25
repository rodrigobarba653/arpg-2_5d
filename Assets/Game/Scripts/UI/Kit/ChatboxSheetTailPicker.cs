using UnityEngine;

/// <summary>
/// Picks a spritesheet tail row from bubble center → speaker screen delta.
/// </summary>
public static class ChatboxSheetTailPicker
{
    const float AxisBias = 1.15f; // prefer vertical when close to diagonal
    const float SideThreshold = 48f; // px: left / center-left / center-right / right

    public static ChatboxSheetTail Pick(Vector2 bubbleCenterScreen, Vector2 speakerScreen, bool allowTail)
    {
        if (!allowTail)
            return ChatboxSheetTail.NoTail;

        Vector2 d = speakerScreen - bubbleCenterScreen;
        if (d.sqrMagnitude < 4f)
            return ChatboxSheetTail.DownCenterLeft;

        bool verticalDominant = Mathf.Abs(d.y) * AxisBias >= Mathf.Abs(d.x);

        if (verticalDominant)
        {
            if (d.y < 0f)
                return PickDown(d.x);
            return PickUp(d.x);
        }

        if (d.x < 0f)
            return d.y >= 0f ? ChatboxSheetTail.LeftUp : ChatboxSheetTail.LeftDown;
        return d.y >= 0f ? ChatboxSheetTail.RightUp : ChatboxSheetTail.RightDown;
    }

    static ChatboxSheetTail PickDown(float dx)
    {
        if (dx < -SideThreshold) return ChatboxSheetTail.DownLeft;
        if (dx < 0f) return ChatboxSheetTail.DownCenterLeft;
        if (dx < SideThreshold) return ChatboxSheetTail.DownCenterRight;
        return ChatboxSheetTail.DownRight;
    }

    static ChatboxSheetTail PickUp(float dx)
    {
        if (dx < -SideThreshold) return ChatboxSheetTail.UpLeft;
        if (dx < 0f) return ChatboxSheetTail.UpCenterLeft;
        if (dx < SideThreshold) return ChatboxSheetTail.UpCenterRight;
        return ChatboxSheetTail.UpRight;
    }
}
