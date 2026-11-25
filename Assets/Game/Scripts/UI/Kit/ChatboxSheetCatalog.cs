using UnityEngine;

/// <summary>
/// Named sprites from <c>chat-notificaction-spritesheet</c>.
/// Columns: LargePortrait / LargePlain / Small. Rows: <see cref="ChatboxSheetTail"/> + item notifications.
/// </summary>
[CreateAssetMenu(fileName = "ChatboxSheetCatalog", menuName = "ARPG/UI/Chatbox Sheet Catalog")]
public class ChatboxSheetCatalog : ScriptableObject
{
    public const string AssetPath = "Assets/Game/UI/UI_Kit/ChatboxSheetCatalog.asset";
    public const string ResourcesPath = "Assets/Game/Resources/ChatboxSheetCatalog.asset";
    public const string SheetPath = "Assets/Game/UI/UI_Kit/chat-notificaction-spritesheet.png";

    public const int Columns = 3;
    public const int TailRows = 13; // 0..12 chat tails (item is row 13)
    public const int CellWidth = 1600;
    public const int CellHeight = 1114;

    [Header("Chatboxes — [column, tailRow]")]
    public Sprite[] largePortrait = new Sprite[TailRows];
    public Sprite[] largePlain = new Sprite[TailRows];
    public Sprite[] small = new Sprite[TailRows];

    [Header("Item notifications (no tail)")]
    public Sprite itemNotificationLarge;
    public Sprite itemNotificationSmall;

    public static string SpriteName(ChatboxSheetColumn column, ChatboxSheetTail tail)
    {
        return $"chat_{ColumnToken(column)}_{TailToken(tail)}";
    }

    public static string ItemSpriteName(bool large) => large ? "notif_large" : "notif_small";

    public Sprite Get(ChatboxSheetColumn column, ChatboxSheetTail tail)
    {
        int i = (int)tail;
        if (i < 0 || i >= TailRows)
            i = 0;

        switch (column)
        {
            case ChatboxSheetColumn.LargePlain:
                return Slot(largePlain, i);
            case ChatboxSheetColumn.Small:
                return Slot(small, i);
            default:
                return Slot(largePortrait, i);
        }
    }

    public Sprite GetItem(bool large) => large ? itemNotificationLarge : itemNotificationSmall;

    public ChatboxSheetColumn ResolveColumn(DialogueChatboxSize size, bool hasPortrait)
    {
        if (size == DialogueChatboxSize.Small)
            return ChatboxSheetColumn.Small;
        return hasPortrait ? ChatboxSheetColumn.LargePortrait : ChatboxSheetColumn.LargePlain;
    }

    static Sprite Slot(Sprite[] arr, int i)
    {
        if (arr == null || i < 0 || i >= arr.Length)
            return null;
        return arr[i];
    }

    static string ColumnToken(ChatboxSheetColumn c)
    {
        switch (c)
        {
            case ChatboxSheetColumn.LargePlain: return "largePlain";
            case ChatboxSheetColumn.Small: return "small";
            default: return "largePortrait";
        }
    }

    static string TailToken(ChatboxSheetTail t)
    {
        switch (t)
        {
            case ChatboxSheetTail.RightUp: return "rightUp";
            case ChatboxSheetTail.RightDown: return "rightDown";
            case ChatboxSheetTail.UpCenterLeft: return "upCenterLeft";
            case ChatboxSheetTail.UpCenterRight: return "upCenterRight";
            case ChatboxSheetTail.UpRight: return "upRight";
            case ChatboxSheetTail.DownRight: return "downRight";
            case ChatboxSheetTail.DownCenterRight: return "downCenterRight";
            case ChatboxSheetTail.DownCenterLeft: return "downCenterLeft";
            case ChatboxSheetTail.DownLeft: return "downLeft";
            case ChatboxSheetTail.UpLeft: return "upLeft";
            case ChatboxSheetTail.LeftDown: return "leftDown";
            case ChatboxSheetTail.LeftUp: return "leftUp";
            default: return "noTail";
        }
    }
}
