[System.Serializable]
public class FlagIsTrueCondition : EventCondition
{
    public string flagId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(flagId) ? "Flag == true (?)" : $"Flag '{flagId}' == true";

    public override bool Evaluate() => WorldState.GetFlag(flagId);
}
