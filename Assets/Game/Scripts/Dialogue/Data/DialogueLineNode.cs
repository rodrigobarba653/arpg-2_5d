using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueLineNode : DialogueNode
{
    [Tooltip("Roster character id (e.g. jyn, villager_a).")]
    public string speakerId = "";

    [Tooltip("Optional display-name override. Empty = roster / speakerId.")]
    public string speakerDisplayName = "";

    [TextArea(2, 6)]
    [Tooltip("Default body. Used when no variant matches the frozen session character.")]
    public string text = "";

    [Tooltip("Optional body overrides keyed by CharacterDefinition.id. " +
             "First non-empty match wins. Empty text is skipped.")]
    public List<DialogueTextVariant> textVariants = new List<DialogueTextVariant>();

    [Tooltip("Interactive only. Ambient never shows portraits.")]
    public string portraitExpression = "Neutral";

    [Tooltip("Next node id. Empty = end conversation.")]
    public string nextNodeId = "";

    [Tooltip("Optional typewriter chars/sec override. 0 = use asset/service default.")]
    public float typewriterCharsPerSecond = 0f;

    [Tooltip("Ambient: seconds to keep the finished line visible. 0 = use host/service default.")]
    public float ambientHoldSeconds = 0f;

    public override string DisplayName =>
        string.IsNullOrEmpty(nodeId) ? "Line (?)" : $"Line '{nodeId}'";
}
