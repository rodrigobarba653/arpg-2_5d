/// <summary>
/// Per-line portrait policy for interactive dialogue. Ambient never shows portraits.
/// </summary>
public enum DialoguePortraitMode
{
    /// <summary>Player → roster Jyn/Ax. NPC → DialogueSpeakerBinding.defaultPortrait, else roster.</summary>
    UseDefault = 0,

    /// <summary>Use line portraitSprite / portraitVariants.</summary>
    Custom = 1,

    /// <summary>No portrait for this line.</summary>
    Hidden = 2,
}
