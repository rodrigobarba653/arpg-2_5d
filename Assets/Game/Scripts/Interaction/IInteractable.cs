using UnityEngine;

/// <summary>
/// Minimal contract for the central <see cref="InteractionDetector"/>.
/// Interaction position is used only for distance / selection — never prompt placement.
/// </summary>
public interface IInteractable
{
    bool IsAvailable { get; }

    /// <summary>
    /// Can contribute to discovery (?) even when not interactable
    /// (e.g. locked but discoverable).
    /// </summary>
    bool IsDiscoverable { get; }

    Vector3 InteractionPosition { get; }

    float DiscoveryRadius { get; }

    float InteractionRadius { get; }

    InteractPromptMode PromptMode { get; }

    /// <summary>True if this object would show ! at the given player distance.</summary>
    bool WouldShowInteractionPrompt(float distanceToPlayer);

    /// <summary>True if this object would show ? at the given player distance.</summary>
    bool WouldShowDiscoveryPrompt(float distanceToPlayer);

    /// <summary>Perform the interaction. Returns true if handled.</summary>
    bool TryInteract();
}
