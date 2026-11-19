using UnityEngine;

/// <summary>
/// Obsolete. Trigger start modes now live on <see cref="EventRunner"/>.
/// On Awake, migrates settings into the sibling EventRunner and disables itself
/// so TestLab / old scenes keep working without a second component.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class EventSourceTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        OnEnter = 0,
        OnExit = 1,
        CrossToggle = 2,
    }

    [Tooltip("Runner to configure. Defaults to EventRunner on this GameObject.")]
    public EventRunner runner;

    public TriggerMode mode = TriggerMode.OnEnter;

    public LayerMask playerLayers = ~0;
    public bool requirePlayerTag = true;
    public string playerTag = "Player";

    [Min(0f)]
    public float crossCooldownSeconds = 0.45f;

    void Reset()
    {
        runner = GetComponent<EventRunner>();
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Awake()
    {
        if (runner == null)
            runner = GetComponent<EventRunner>();
        if (runner == null)
            return;

        MigrateIntoRunner(runner);
        enabled = false;
    }

    /// <summary>Editor / runtime: copy fields onto EventRunner.startMode.</summary>
    public void MigrateIntoRunner(EventRunner target)
    {
        if (target == null)
            return;

        switch (mode)
        {
            case TriggerMode.OnExit:
                target.startMode = EventStartMode.TriggerOnExit;
                break;
            case TriggerMode.CrossToggle:
                target.startMode = EventStartMode.ToggleCross;
                break;
            default:
                target.startMode = EventStartMode.TriggerOnEnter;
                break;
        }

        target.triggerPlayerLayers = playerLayers;
        target.triggerRequirePlayerTag = requirePlayerTag;
        target.triggerPlayerTag = playerTag;
        target.toggleCrossCooldownSeconds = crossCooldownSeconds;
    }
}
