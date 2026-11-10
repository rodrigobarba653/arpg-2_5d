using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-facing interactable for the central InteractionDetector.
/// Starts an EventRunner (or fires a UnityEvent) without per-object prompt GOs.
/// </summary>
[DisallowMultipleComponent]
public class WorldInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    public InteractPromptMode promptMode = InteractPromptMode.DiscoveryAndInteraction;

    [Min(0.1f)]
    public float discoveryRadius = 3.5f;

    [Min(0.05f)]
    public float interactionRadius = 1.1f;

    [Tooltip("Optional point used only for distance checks. Defaults to this transform.")]
    public Transform interactionPoint;

    [Header("Availability")]
    [Tooltip("Master toggle. Also gated by conditions / linked EventRunner Once state.")]
    public bool available = true;

    [Tooltip("If true, can show ? while not interactable (e.g. locked).")]
    public bool discoverableWhenUnavailable = false;

    [SerializeReference]
    public List<EventCondition> conditions = new List<EventCondition>();

    [Header("Action")]
    [Tooltip("Started on interact. Defaults to EventRunner on this GameObject.")]
    public EventRunner eventRunner;

    [Tooltip("If true, this component owns Interact input via InteractionDetector " +
             "and disables legacy EventSourceInteract polling on this object.")]
    public bool useCentralDetector = true;

    EventSourceInteract legacySource;

    public InteractPromptMode PromptMode => promptMode;
    public float DiscoveryRadius => discoveryRadius;
    public float InteractionRadius => interactionRadius;

    public Vector3 InteractionPosition =>
        interactionPoint != null ? interactionPoint.position : transform.position;

    public bool IsAvailable
    {
        get
        {
            if (!isActiveAndEnabled || !available)
                return false;

            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    var c = conditions[i];
                    if (c != null && !c.Evaluate())
                        return false;
                }
            }

            if (eventRunner != null)
            {
                if (eventRunner.IsRunning)
                    return false;
                if (eventRunner.executionMode == EventExecutionMode.Once &&
                    eventRunner.HasCompletedOnce)
                    return false;
            }

            return true;
        }
    }

    public bool IsDiscoverable
    {
        get
        {
            if (!isActiveAndEnabled)
                return false;
            if (IsAvailable)
                return true;
            return discoverableWhenUnavailable && available;
        }
    }

    void Reset()
    {
        eventRunner = GetComponent<EventRunner>();
    }

    void OnValidate()
    {
        if (discoveryRadius < interactionRadius)
            discoveryRadius = interactionRadius;
    }

    void Awake()
    {
        if (eventRunner == null)
            eventRunner = GetComponent<EventRunner>();

        legacySource = GetComponent<EventSourceInteract>();
        if (useCentralDetector && legacySource != null)
            legacySource.enabled = false;
    }

    void OnEnable()
    {
        if (useCentralDetector)
            InteractableRegistry.Register(this);
    }

    void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

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

        // Available + inside interaction → ! wins (not ?).
        if (IsAvailable && distanceToPlayer <= interactionRadius)
            return false;

        // Outside interaction, or locked-but-discoverable inside interaction → ?.
        return true;
    }

    public bool TryInteract()
    {
        if (!IsAvailable)
            return false;

        if (eventRunner != null)
            return eventRunner.TryStart();

        Debug.LogWarning($"[WorldInteractable] '{name}' has no EventRunner.", this);
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 p = InteractionPosition;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(p, discoveryRadius);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.45f);
        Gizmos.DrawWireSphere(p, interactionRadius);
    }
#endif
}
