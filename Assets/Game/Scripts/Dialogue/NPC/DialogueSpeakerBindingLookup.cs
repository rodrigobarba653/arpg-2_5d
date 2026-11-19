using UnityEngine;

/// <summary>
/// Finds <see cref="DialogueSpeakerBinding"/> instances in loaded scenes by speaker id.
/// </summary>
public static class DialogueSpeakerBindingLookup
{
    public static DialogueSpeakerBinding Find(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId))
            return null;

        var all = Object.FindObjectsByType<DialogueSpeakerBinding>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            DialogueSpeakerBinding binding = all[i];
            if (binding == null || !binding.isActiveAndEnabled)
                continue;
            if (string.Equals(binding.speakerId, speakerId, System.StringComparison.OrdinalIgnoreCase))
                return binding;
        }

        return null;
    }

    public static Transform FindAnchor(string speakerId)
    {
        DialogueSpeakerBinding binding = Find(speakerId);
        return binding != null ? binding.Anchor : null;
    }

    public static Sprite FindDefaultPortrait(string speakerId)
    {
        DialogueSpeakerBinding binding = Find(speakerId);
        return binding != null ? binding.defaultPortrait : null;
    }
}
