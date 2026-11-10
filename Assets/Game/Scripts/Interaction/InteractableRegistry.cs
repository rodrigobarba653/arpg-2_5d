using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of active <see cref="IInteractable"/>s.
/// Avoids per-frame FindObjects / per-object Interact polling for migrated targets.
/// </summary>
public static class InteractableRegistry
{
    static readonly List<IInteractable> items = new List<IInteractable>(64);

    public static IReadOnlyList<IInteractable> Items => items;

    public static void Register(IInteractable interactable)
    {
        if (interactable == null)
            return;
        if (items.Contains(interactable))
            return;
        items.Add(interactable);
    }

    public static void Unregister(IInteractable interactable)
    {
        if (interactable == null)
            return;
        items.Remove(interactable);
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        items.Clear();
    }
#endif
}
