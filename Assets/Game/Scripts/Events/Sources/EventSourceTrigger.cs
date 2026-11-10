using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Starts the sibling/assigned <see cref="EventRunner"/> when a player-tagged
/// collider enters (or exits) this trigger.
///
/// Tracks presence per player root so CharacterController edge jitter and
/// multiple child colliders do not spam TryStart every physics frame.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EventSourceTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        OnEnter = 0,
        OnExit = 1,
    }

    [Tooltip("Runner to start. Defaults to EventRunner on this GameObject.")]
    public EventRunner runner;

    public TriggerMode mode = TriggerMode.OnEnter;

    [Tooltip("Only react to colliders on these layers.")]
    public LayerMask playerLayers = ~0;

    [Tooltip("Also require the other collider's GameObject (or parent) to be tagged Player.")]
    public bool requirePlayerTag = true;

    [Tooltip("Tag used when requirePlayerTag is on.")]
    public string playerTag = "Player";

    readonly Dictionary<Transform, int> insideColliderCounts = new Dictionary<Transform, int>();

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

    void OnDisable()
    {
        insideColliderCounts.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidPlayer(other))
            return;

        Transform root = other.transform.root;
        insideColliderCounts.TryGetValue(root, out int count);
        insideColliderCounts[root] = count + 1;

        // First collider of this player root entering the volume.
        if (count == 0 && mode == TriggerMode.OnEnter)
            TryStartRunner();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValidPlayer(other))
            return;

        Transform root = other.transform.root;
        if (!insideColliderCounts.TryGetValue(root, out int count))
            return;

        count--;
        if (count <= 0)
        {
            insideColliderCounts.Remove(root);
            if (mode == TriggerMode.OnExit)
                TryStartRunner();
        }
        else
        {
            insideColliderCounts[root] = count;
        }
    }

    void TryStartRunner()
    {
        if (runner == null)
            return;

        runner.TryStart();
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
