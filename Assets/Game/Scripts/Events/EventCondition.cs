/// <summary>
/// Base type for SerializeReference event start conditions (AND'd together).
/// Evaluated in <see cref="EventRunner.TryStart"/> before locks/scheduler.
/// </summary>
[System.Serializable]
public abstract class EventCondition
{
    public abstract string DisplayName { get; }

    public abstract bool Evaluate();
}
