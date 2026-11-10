using System;
using UnityEngine;

/// <summary>
/// Optional body text for one <see cref="CharacterDefinition.id"/>.
/// Empty text is ignored. No match falls back to the line's default text.
/// </summary>
[Serializable]
public class DialogueTextVariant
{
    [Tooltip("CharacterDefinition.id (for example ax, jyn). Matching is case-insensitive. " +
             "active_player / player / party / hero follow the frozen session character.")]
    public string characterId = "";

    [TextArea(2, 6)]
    public string text = "";
}
