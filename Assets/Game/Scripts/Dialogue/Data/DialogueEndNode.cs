[System.Serializable]
public class DialogueEndNode : DialogueNode
{
    public override string DisplayName =>
        string.IsNullOrEmpty(nodeId) ? "End" : $"End '{nodeId}'";
}
