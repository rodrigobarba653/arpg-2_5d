using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dialogue", menuName = "ARPG/Dialogue/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    [Tooltip("Stable id for WorldState helpers (seen flags) and debugging.")]
    public string dialogueId = "";

    [Tooltip("First node id to play.")]
    public string entryNodeId = "";

    [Tooltip("Default typewriter speed for lines that leave override at 0.")]
    public float defaultTypewriterCharsPerSecond = 40f;

    [Tooltip("When true (interactive), acquire GameplayLock.PlayerControl.")]
    public bool lockGameplay = true;

    [Tooltip("Who this conversation is tied to. Player = active party member. " +
             "Npc = originSpeakerId (e.g. guard1). Scene Transform is assigned on Start Dialogue / NpcDialogueHost.")]
    public DialogueOriginKind originKind = DialogueOriginKind.ActivePlayer;

    [Tooltip("Speaker id for the NPC origin when Origin is Npc. Must match line speaker ids and Start Dialogue Npc Speaker Id.")]
    public string originSpeakerId = "";

    [Tooltip("When set, this asset's choice panel placement replaces DialogueLayoutSettings.")]
    public bool overrideChoicePresentation;

    [Tooltip("Used when Override Choice Presentation is on. Otherwise the service uses DialogueLayoutSettings.")]
    public DialogueChoicePresentation choicePresentation = DialogueChoicePresentation.LowerCenterDefault;

    [SerializeReference]
    public List<DialogueNode> nodes = new List<DialogueNode>();

    public DialogueNode FindNode(string id)
    {
        if (string.IsNullOrEmpty(id) || nodes == null)
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n != null && n.nodeId == id)
                return n;
        }

        return null;
    }

    public DialogueNode GetEntry()
    {
        if (!string.IsNullOrEmpty(entryNodeId))
            return FindNode(entryNodeId);

        return nodes != null && nodes.Count > 0 ? nodes[0] : null;
    }

    /// <summary>
    /// Speaker id Start Dialogue / hosts should use for the NPC bubble anchor.
    /// Empty when origin is the active player.
    /// </summary>
    public string GetOriginNpcSpeakerId()
    {
        if (originKind != DialogueOriginKind.Npc)
            return "";

        if (!string.IsNullOrEmpty(originSpeakerId))
            return originSpeakerId.Trim();

        return FindFirstNonPlayerSpeakerId();
    }

    public string FindFirstNonPlayerSpeakerId()
    {
        if (nodes == null)
            return "";

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not DialogueLineNode line)
                continue;
            if (string.IsNullOrEmpty(line.speakerId))
                continue;
            if (DialogueActiveCharacter.IsActivePlayerAlias(line.speakerId))
                continue;
            return line.speakerId.Trim();
        }

        return "";
    }
}
