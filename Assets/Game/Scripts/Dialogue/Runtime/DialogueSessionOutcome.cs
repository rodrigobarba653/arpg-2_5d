/// <summary>
/// How an interactive dialogue session ended.
/// <see cref="None"/> while it is still running.
/// </summary>
public enum DialogueSessionOutcome
{
    None = 0,
    Completed = 1,
    Cancelled = 2,
    Failed = 3,
}
