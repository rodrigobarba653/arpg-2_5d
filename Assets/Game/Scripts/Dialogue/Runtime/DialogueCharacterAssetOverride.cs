using System;
using UnityEngine;

/// <summary>
/// One character-specific <see cref="DialogueAsset"/> entry.
/// characterId must match <see cref="CharacterDefinition.id"/> (not display name).
/// </summary>
[Serializable]
public class DialogueCharacterAssetOverride
{
    [Tooltip("Stable CharacterDefinition.id (e.g. jyn, ax).")]
    public string characterId = "";

    public DialogueAsset asset;
}
