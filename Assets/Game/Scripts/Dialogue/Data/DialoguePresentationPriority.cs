/// <summary>
/// Shared presentation priority for event-driven screen-space UI.
/// Dialogue owns Interactive/Ambient bands; Sprint 07 notifications can
/// insert between without coupling systems.
/// </summary>
public enum DialoguePresentationPriority
{
    Ambient = 10,
    Notification = 50,   // reserved for Sprint 07 — do not use in Sprint 05
    Interactive = 100,
}
