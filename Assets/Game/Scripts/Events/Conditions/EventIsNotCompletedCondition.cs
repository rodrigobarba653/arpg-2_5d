[System.Serializable]
public class EventIsNotCompletedCondition : EventCondition
{
    public string eventId = "";

    public override string DisplayName =>
        string.IsNullOrEmpty(eventId)
            ? "Event not completed (?)"
            : $"Event '{eventId}' not completed";

    public override bool Evaluate() => !WorldState.IsEventCompleted(eventId);
}
