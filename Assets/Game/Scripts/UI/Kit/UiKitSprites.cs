using UnityEngine;

/// <summary>
/// Sprite refs for TOM UI Kit v2. Prefer the assembled 9-slice frames
/// (do not also render the separate corner/edge set at the same time).
/// Tails are split into background (bg) + outline (ot) layers.
/// </summary>
[CreateAssetMenu(fileName = "UiKitSprites", menuName = "ARPG/UI/UI Kit Sprites")]
public class UiKitSprites : ScriptableObject
{
    public const string AssetPath = "Assets/Game/UI/UI_Kit/UiKitSprites.asset";
    public const string ResourcesAssetPath = "Assets/Game/Resources/UiKitSprites.asset";
    public const string PngFolder = "Assets/Game/UI/UI_Kit/PNG";

    [Header("Dialogue 9-slice (PNG 1280x360 — L/R 264, T/B 100)")]
    public Sprite dialogueFrame;
    public Sprite dialogueBackground;

    [Header("Notification 9-slice (PNG 1120x200 — L 230 R 100 T/B 60)")]
    public Sprite notificationFrame;
    public Sprite notificationBackground;
    public Sprite notificationIconFrame;

    [Header("Portrait (do not slice)")]
    public Sprite portraitFrame;
    public Sprite portraitBackground;

    [Header("Tail background (bg) — unsliced")]
    public Sprite tailBottomBg;
    public Sprite tailTopBg;
    public Sprite tailLeftBg;
    public Sprite tailRightBg;

    [Header("Tail outline (ot) — unsliced")]
    public Sprite tailBottomOt;
    public Sprite tailTopOt;
    public Sprite tailLeftOt;
    public Sprite tailRightOt;

    public Sprite tailConnectionMask;

    [Header("Optional")]
    public Sprite continueIndicator;

    public static readonly Color Coral = new Color(1f, 0.40784314f, 0.46666667f, 1f);
    public static readonly Color Cyan = new Color(0.38039216f, 0.8509804f, 0.8235294f, 1f);
    public static readonly Color Background = new Color(0.12941177f, 0.09803922f, 0.14509805f, 1f);

    /// <summary>Chatbox / tail fill opacity (1 = fully opaque).</summary>
    public const float FillOpacity = 1f;

    public Sprite TailBgFor(DialogueChatboxTailDirection direction)
    {
        switch (direction)
        {
            case DialogueChatboxTailDirection.Top: return tailTopBg;
            // Art tip points inward on the named side PNGs — swap so the tip faces the speaker.
            case DialogueChatboxTailDirection.Left: return tailRightBg;
            case DialogueChatboxTailDirection.Right: return tailLeftBg;
            default: return tailBottomBg;
        }
    }

    public Sprite TailOtFor(DialogueChatboxTailDirection direction)
    {
        switch (direction)
        {
            case DialogueChatboxTailDirection.Top: return tailTopOt;
            case DialogueChatboxTailDirection.Left: return tailRightOt;
            case DialogueChatboxTailDirection.Right: return tailLeftOt;
            default: return tailBottomOt;
        }
    }
}

public enum DialogueChatboxTailDirection
{
    Bottom = 0,
    Top = 1,
    Left = 2,
    Right = 3,
}
