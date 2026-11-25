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

    [Tooltip("Use Default / Custom / Hidden. Ambient never shows portraits.")]
    public DialoguePortraitMode portraitMode = DialoguePortraitMode.UseDefault;

    [Tooltip("Roster expression when Use Default falls back to CharacterPortraitRoster.")]
    public string portraitExpression = "Neutral";

    [Tooltip("Custom portrait for NPC / fixed speaker lines when Portrait Mode is Custom.")]
    public Sprite portraitSprite;

    [Tooltip("Custom portraits for Active Player (jyn / ax) when Portrait Mode is Custom.")]
    public List<DialoguePortraitVariant> portraitVariants = new List<DialoguePortraitVariant>();

    [Tooltip("Next node id. Empty = end conversation.")]
    public string nextNodeId = "";

    [Tooltip("Optional typewriter chars/sec override. 0 = use asset/service default.")]
    public float typewriterCharsPerSecond = 0f;

    [Tooltip("Ambient: seconds to keep the finished line visible. 0 = use host/service default.")]
    public float ambientHoldSeconds = 0f;

    [Tooltip("Optional voice / VO clip. Plays when this line's chatbox starts (with typewriter). Stops on Continue / cancel / next line.")]
    public AudioClip voiceClip;

    [Range(0f, 1f)]
    [Tooltip("Per-line volume scale for voiceClip (multiplied by AudioManager voice + master).")]
    public float voiceVolume = 1f;

    public override string DisplayName =>
        string.IsNullOrEmpty(nodeId) ? "Line (?)" : $"Line '{nodeId}'";
}
