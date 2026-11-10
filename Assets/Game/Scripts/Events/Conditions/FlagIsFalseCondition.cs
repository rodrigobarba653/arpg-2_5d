[System.Serializable]
public class FlagIsFalseCondition : EventCondition
{
    public string flagId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(flagId) ? "Flag == false (?)" : $"Flag '{flagId}' == false";

    public override bool Evaluate() => !WorldState.GetFlag(flagId);
}
