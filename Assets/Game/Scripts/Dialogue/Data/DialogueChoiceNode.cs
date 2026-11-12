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

    [Tooltip("Responses. Each one can require a character and a list of conditions.")]
    public List<DialogueChoiceOption> options = new List<DialogueChoiceOption>();

    [Tooltip("Used when no authored option is visible. Empty with no authored options fails the session. Empty when options exist but all are hidden ends as Completed. A missing id fails.")]
    public string fallbackNodeId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(nodeId) ? "Choice (?)" : $"Choice '{nodeId}'";
}
