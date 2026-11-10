using UnityEngine;

/// <summary>
/// Opt-in migration: registers an existing <see cref="EventSourceInteract"/> with the
/// central detector and disables its local Interact poll + trigger gate.
/// Distance uses this transform; radii / PromptMode are configured here.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EventSourceInteract))]
public class EventSourceInteractAdapter : MonoBehaviour, IInteractable
{
    public InteractPromptMode promptMode = InteractPromptMode.InteractionOnly;

    [Min(0.1f)] public float discoveryRadius = 3f;
    [Min(0.05f)] public float interactionRadius = 2f;

    public Transform interactionPoint;

    EventSourceInteract source;
    EventRunner runner;

    public InteractPromptMode PromptMode => promptMode;
    public float DiscoveryRadius => discoveryRadius;
    public float InteractionRadius => interactionRadius;

    public Vector3 InteractionPosition =>
        interactionPoint != null ? interactionPoint.position : transform.position;

    public bool IsAvailable
    {
        get
        {
            if (!isActiveAndEnabled)
                return false;

            if (runner == null)
                runner = source != null ? source.runner : GetComponent<EventRunner>();

            if (runner == null)
                return false;
            if (runner.IsRunning)
                return false;
            if (runner.executionMode == EventExecutionMode.Once && runner.HasCompletedOnce)
                return false;
            return true;
        }
    }

    public bool IsDiscoverable => IsAvailable;

    void OnValidate()
    {
        if (discoveryRadius < interactionRadius)
            discoveryRadius = interactionRadius;
    }

    void Awake()
    {
        source = GetComponent<EventSourceInteract>();
        runner = source != null ? source.runner : GetComponent<EventRunner>();
        if (runner == null)
            runner = GetComponent<EventRunner>();

        // Disable legacy polling so detector is the only input path for this object.
        if (source != null)
            source.enabled = false;
    }

    void OnEnable() => InteractableRegistry.Register(this);

    void OnDisable() => InteractableRegistry.Unregister(this);

    public bool WouldShowInteractionPrompt(float distanceToPlayer)
    {
        if (promptMode == InteractPromptMode.Hidden)
            return false;
        if (!IsAvailable)
            return false;
        return distanceToPlayer <= interactionRadius;
    }

    public bool WouldShowDiscoveryPrompt(float distanceToPlayer)
    {
        if (promptMode != InteractPromptMode.DiscoveryAndInteraction)
            return false;
        if (!IsDiscoverable)
            return false;
        if (distanceToPlayer > discoveryRadius)
            return false;
        if (IsAvailable && distanceToPlayer <= interactionRadius)
            return false;
        return true;
    }

    public bool TryInteract()
    {
        if (!IsAvailable || runner == null)
            return false;
        return runner.TryStart();
    }
}
