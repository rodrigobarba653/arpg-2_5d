/// <summary>
/// Per-interactable prompt presentation. Independent of whether interaction is allowed.
/// </summary>
public enum InteractPromptMode
{
    /// <summary>Fully interactable in range; never shows ? or ! above the player.</summary>
    Hidden = 0,

    /// <summary>No discovery. Show ! only inside interaction radius.</summary>
    InteractionOnly = 1,

    /// <summary>Show ? in discovery range (outside interaction); ! inside interaction range.</summary>
    DiscoveryAndInteraction = 2,
}
