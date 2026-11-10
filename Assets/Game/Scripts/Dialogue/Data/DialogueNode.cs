using UnityEngine;

[System.Serializable]
public abstract class DialogueNode
{
    [Tooltip("Stable id within this DialogueAsset. Referenced by nextNodeId fields.")]
    public string nodeId = "";

    public abstract string DisplayName { get; }
}
