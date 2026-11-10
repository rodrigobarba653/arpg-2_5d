using System;
using System.Collections.Generic;

/// <summary>
/// Picks line body text for the character id frozen on the dialogue session.
/// Does not read <see cref="Party.Active"/>.
/// </summary>
public static class DialogueTextResolver
{
    /// <summary>
    /// Returns the first variant whose character matches <paramref name="sessionCharacterId"/>
    /// and whose text is non-empty. Otherwise <paramref name="defaultText"/>.
    /// </summary>
    public static string Resolve(
        string defaultText,
        IList<DialogueTextVariant> variants,
        string sessionCharacterId)
    {
        string fallback = defaultText ?? "";
        if (string.IsNullOrEmpty(sessionCharacterId) || variants == null)
            return fallback;

        for (int i = 0; i < variants.Count; i++)
        {
            DialogueTextVariant variant = variants[i];
            if (variant == null || string.IsNullOrEmpty(variant.text))
                continue;
            if (!MatchesSession(variant.characterId, sessionCharacterId))
                continue;
            return variant.text;
        }

        return fallback;
    }

    static bool MatchesSession(string authoredCharacterId, string sessionCharacterId)
    {
        if (string.IsNullOrEmpty(authoredCharacterId))
            return false;

        // Aliases (active_player, player, party, hero) become the frozen session id.
        // Concrete ids pass through. Comparison stays case-insensitive either way.
        string resolved = DialogueActiveCharacter.ResolveSpeakerId(authoredCharacterId, sessionCharacterId);
        return string.Equals(resolved, sessionCharacterId, StringComparison.OrdinalIgnoreCase);
    }
}
