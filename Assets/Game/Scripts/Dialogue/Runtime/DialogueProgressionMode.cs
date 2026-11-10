/// <summary>
/// How interactive-style lines advance. Independent of presentation and lock policy.
/// </summary>
public enum DialogueProgressionMode
{
    /// <summary>Confirm reveals/advances; Cancel ends (if allowed).</summary>
    Manual = 0,

    /// <summary>Typewriter runs, then auto-advances after a hold. No Confirm required.</summary>
    Automatic = 1,
}
