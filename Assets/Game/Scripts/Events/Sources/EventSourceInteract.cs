using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Starts the assigned <see cref="EventRunner"/> when the player is inside this
/// trigger volume and presses Interact.
///
/// NOTE (Sprint 01): Interact still uses <see cref="InteractInput"/> defaults
/// (E / Enter / Gamepad South). South also fires Jump — documented conflict;
/// binding migration is deferred. Do not remap here.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EventSourceInteract : MonoBehaviour
{
    [Tooltip("Runner to start. Defaults to EventRunner on this GameObject.")]
    public EventRunner runner;

    [Tooltip("Optional override action. Leave empty to use InteractInput defaults.")]
    public InputActionReference interactAction;

    [Tooltip("Only react to colliders on these layers.")]
    public LayerMask playerLayers = ~0;

    [Tooltip("Also require the other collider's GameObject (or parent) to be tagged Player.")]
    public bool requirePlayerTag = true;

    public string playerTag = "Player";

    int playersInside;

    void Reset()
    {
        runner = GetComponent<EventRunner>();
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Awake()
    {
        if (!runner)
            runner = GetComponent<EventRunner>();
    }

    void Update()
    {
        if (playersInside <= 0 || runner == null)
            return;

        if (!InteractInput.WasPressedThisFrame(interactAction))
            return;

        runner.TryStart();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidPlayer(other))
            return;
        playersInside++;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValidPlayer(other))
            return;
        playersInside = Mathf.Max(0, playersInside - 1);
    }

    void OnDisable()
    {
        playersInside = 0;
    }

    bool IsValidPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (((1 << other.gameObject.layer) & playerLayers) == 0)
            return false;

        if (!requirePlayerTag)
            return true;

        if (other.CompareTag(playerTag))
            return true;

        var root = other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }
}
