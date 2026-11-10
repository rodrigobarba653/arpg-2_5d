using UnityEngine;

/// <summary>
/// Resolves the active playable character from <see cref="Party.Active"/>.
/// No second active-player state — Party remains the source of truth.
/// </summary>
public static class DialogueActiveCharacter
{
    /// <summary>Preferred authoring token for "whoever is currently controlled".</summary>
    public const string ActivePlayerAlias = "active_player";

    static readonly string[] ActivePlayerAliases =
    {
        ActivePlayerAlias,
        "player",
        "party",
        "hero",
    };

    public static bool TryGetActiveCharacterId(out string characterId)
    {
        characterId = null;
        var active = Party.Active;
        if (active == null || active.character == null)
            return false;
        if (string.IsNullOrEmpty(active.character.id))
            return false;
        characterId = active.character.id;
        return true;
    }

    public static bool TryGetActiveTransform(out Transform transform)
    {
        transform = null;
        var active = Party.Active;
        if (active == null)
            return false;
        transform = active.transform;
        return transform != null;
    }

    public static bool IsActivePlayerAlias(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId))
            return false;
        string key = speakerId.Trim().ToLowerInvariant();
        for (int i = 0; i < ActivePlayerAliases.Length; i++)
        {
            if (key == ActivePlayerAliases[i])
                return true;
        }
        return false;
    }

    /// <summary>
    /// Maps authored speaker id → resolved character id for the session.
    /// ActivePlayer aliases become <paramref name="sessionActiveCharacterId"/>.
    /// Fixed ids (e.g. jyn, villager_a) pass through unchanged.
    /// </summary>
    public static string ResolveSpeakerId(string authoredSpeakerId, string sessionActiveCharacterId)
    {
        if (IsActivePlayerAlias(authoredSpeakerId))
        {
            if (!string.IsNullOrEmpty(sessionActiveCharacterId))
                return sessionActiveCharacterId;
            return ActivePlayerAlias;
        }

        return string.IsNullOrEmpty(authoredSpeakerId) ? "_default" : authoredSpeakerId;
    }

    public static string FallbackDisplayName(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || IsActivePlayerAlias(characterId))
            return "Player";
        return characterId;
    }
}
