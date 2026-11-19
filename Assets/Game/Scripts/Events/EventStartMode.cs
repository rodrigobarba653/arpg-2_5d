/// <summary>
/// How an <see cref="EventRunner"/> auto-starts. Trigger modes use the collider
/// on the same GameObject — no separate EventSourceTrigger component.
/// </summary>
public enum EventStartMode
{
    ManualOnly = 0,
    TriggerOnEnter = 1,
    TriggerOnExit = 2,
    Interact = 3,
    ToggleCross = 4,
}
