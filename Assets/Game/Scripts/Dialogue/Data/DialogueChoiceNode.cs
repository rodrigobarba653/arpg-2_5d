using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player response menu. Not a speaker and not a chatbox.
/// </summary>
[Serializable]
public class DialogueChoiceNode : DialogueNode
{
    [TextArea(1, 3)]
    [Tooltip("Optional header above the responses. The previous line is the question.")]
    public string promptText = "";

    public List<DialogueChoiceOption> options = new List<DialogueChoiceOption>();

    [Tooltip("Used only when no option is authored. Empty plus no options = Failed.")]
    public string fallbackNodeId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(nodeId) ? "Choice (?)" : $"Choice '{nodeId}'";
}
