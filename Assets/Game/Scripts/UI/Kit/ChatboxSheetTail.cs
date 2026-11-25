/// <summary>
/// Row index in <c>chat-notificaction-spritesheet</c> (0-based).
/// Tails point toward the speaker.
/// </summary>
public enum ChatboxSheetTail
{
    NoTail = 0,
    RightUp = 1,
    RightDown = 2,
    UpCenterLeft = 3,
    UpCenterRight = 4,
    UpRight = 5,
    DownRight = 6,
    DownCenterRight = 7,
    DownCenterLeft = 8,
    DownLeft = 9,
    UpLeft = 10,
    LeftDown = 11,
    LeftUp = 12,
}

/// <summary>Column in the chatbox spritesheet.</summary>
public enum ChatboxSheetColumn
{
    /// <summary>Large box with portrait slot (column 0).</summary>
    LargePortrait = 0,
    /// <summary>Large box, no portrait (column 1).</summary>
    LargePlain = 1,
    /// <summary>Small box, no portrait (column 2).</summary>
    Small = 2,
}

public enum DialogueChatboxSize
{
    Large = 0,
    Small = 1,
}
