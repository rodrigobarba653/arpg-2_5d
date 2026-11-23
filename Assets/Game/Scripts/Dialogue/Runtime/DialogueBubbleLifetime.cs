/// <summary>
/// How interactive chatboxes stay on screen across lines.
/// Independent of typewriter / Confirm progression.
/// </summary>
public enum DialogueBubbleLifetime
{
    /// <summary>
    /// One bubble per speaker; stays visible while others talk (classic multi-speaker stage).
    /// Same speaker updates the existing bubble.
    /// </summary>
    PerSpeakerSticky = 0,

    /// <summary>
    /// Only the active line is visible. Opening the next line closes the previous
    /// (Jyn → closes, NPC → closes, Jyn → closes).
    /// </summary>
    OneAtATime = 1,

    /// <summary>
    /// Keep the last N speakers who talked; older bubbles hide when a new speaker exceeds N.
    /// </summary>
    KeepRecent = 2,
}
