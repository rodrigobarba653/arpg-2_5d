[System.Serializable]
public class EventIsCompletedCondition : EventCondition
{
    public string eventId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(eventId)
            ? "Event completed (?)"
            : $"Event '{eventId}' completed";

    public override bool Evaluate() => WorldState.IsEventCompleted(eventId);
}
