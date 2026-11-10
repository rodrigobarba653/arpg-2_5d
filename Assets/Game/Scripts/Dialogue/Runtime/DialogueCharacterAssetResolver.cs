using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared selection of DialogueAsset by active character id.
/// Used by NpcDialogueHost now; StartDialogueAction / scripted events can reuse in Phase C.
/// </summary>
public static class DialogueCharacterAssetResolver
{
    /// <summary>
    /// Picks a character-specific asset when present; otherwise <paramref name="fallback"/>.
    /// Does not hardcode Ax/Jyn — compares authored characterId strings only.
    /// </summary>
    public static DialogueAsset Resolve(
        DialogueAsset fallback,
        IList<DialogueCharacterAssetOverride> byCharacter,
        string characterId)
    {
        if (!string.IsNullOrEmpty(characterId) && byCharacter != null)
        {
            for (int i = 0; i < byCharacter.Count; i++)
            {
                var entry = byCharacter[i];
                if (entry == null || entry.asset == null)
                    continue;
                if (string.IsNullOrEmpty(entry.characterId))
                    continue;
                if (string.Equals(entry.characterId, characterId, System.StringComparison.OrdinalIgnoreCase))
                    return entry.asset;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Convenience: resolve using current <see cref="Party.Active"/> id.
    /// </summary>
    public static DialogueAsset ResolveForActivePartyMember(
        DialogueAsset fallback,
        IList<DialogueCharacterAssetOverride> byCharacter)
    {
        DialogueActiveCharacter.TryGetActiveCharacterId(out string id);
        return Resolve(fallback, byCharacter, id);
    }
}
