/// <summary>
/// Where the interactive chatbox sits. Independent of who is speaking (Player / NPC).
/// </summary>
public enum DialogueChatboxAim
{
    /// <summary>Point at the speaker in the world (tail toward them).</summary>
    FollowSpeaker = 0,

    /// <summary>
    /// Top-of-screen banner, large, no tail — speaker can still be Player/NPC
    /// (e.g. Active Player off-camera).
    /// </summary>
    Screen = 1,
}
