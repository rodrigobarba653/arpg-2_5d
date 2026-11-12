using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Silent branch. Evaluates <see cref="EventCondition"/> when reached and follows one destination.
/// No chatbox, no menu, no WorldState writes, no actions, no lock of its own.
/// </summary>
[Serializable]
public class DialogueConditionNode : DialogueNode
{
    [SerializeReference]
    [Tooltip("All must pass. An empty list is true. Null entries are skipped.")]
    public List<EventCondition> conditions = new List<EventCondition>();

    [Tooltip("Destination when every condition passes. Empty ends the conversation (Completed).")]
    public string trueNodeId = "";

    [Tooltip("Destination when a condition fails. Empty ends the conversation (Completed).")]
    public string falseNodeId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(nodeId) ? "Condition (?)" : $"Condition '{nodeId}'";
}
