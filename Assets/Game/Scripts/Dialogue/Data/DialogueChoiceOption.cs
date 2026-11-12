using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor marker for a choice option's <see cref="EventAction"/> list.
/// The drawer omits <see cref="StartDialogueAction"/>.
/// </summary>
public sealed class DialogueConsequenceListAttribute : PropertyAttribute
{
}

/// <summary>
/// One selectable response on a <see cref="DialogueChoiceNode"/>.
/// Visibility uses <see cref="requiredCharacterId"/> and <see cref="availability"/>.
/// The label uses <see cref="text"/> unless <see cref="textVariants"/> matches the frozen session character.
/// Consequences run only after this option is confirmed.
/// </summary>
[Serializable]
public class DialogueChoiceOption
{
    [Tooltip("Id inside this choice. If text and variants are empty, the menu shows this id. Duplicates in the same choice are an authoring error.")]
    public string optionId = "";

    [TextArea(1, 4)]
    [Tooltip("Default label. A matching textVariant with non-empty text replaces it.")]
    public string text = "";

    [Tooltip("Label overrides for a character id. Empty variant text is ignored.")]
    public List<DialogueTextVariant> textVariants = new List<DialogueTextVariant>();

    [Tooltip("Next node id. Empty = end the conversation (Completed).")]
    public string nextNodeId = "";

    [Tooltip("Empty = any character. Otherwise the frozen session id must match, ignoring case.")]
    public string requiredCharacterId = "";

    [SerializeReference]
    [Tooltip("All conditions must pass. An empty list is always available. Unavailable options are omitted.")]
    public List<EventCondition> availability = new List<EventCondition>();

    [SerializeReference]
    [DialogueConsequenceList]
    [Tooltip("Runs in order only after this option is confirmed. Empty does nothing. Start Dialogue is rejected.")]
    public List<EventAction> consequences = new List<EventAction>();
}
