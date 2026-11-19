using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal pressure plate: opens a door while occupied, closes when empty.
/// Counts colliders per player root so CharacterController + children don't flicker.
/// Does not fire EventRunner sequences — for that, use EventRunner Start Mode Trigger On Enter/Exit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePlateDoor : MonoBehaviour
{
    public SimpleDoor door;

    [Tooltip("Optional EventRunner started on first occupant enter.")]
    public EventRunner onEnterRunner;

    [Tooltip("Optional EventRunner started when last occupant leaves.")]
    public EventRunner onExitRunner;

    public LayerMask playerLayers = ~0;
    public bool requirePlayerTag = true;
    public string playerTag = "Player";

    readonly Dictionary<Transform, int> insideCounts = new Dictionary<Transform, int>();

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnDisable()
    {
        insideCounts.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidPlayer(other))
            return;

        Transform root = other.transform.root;
        insideCounts.TryGetValue(root, out int count);
        insideCounts[root] = count + 1;

        if (count == 0 && insideCounts.Count == 1)
            OnOccupied();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValidPlayer(other))
            return;

        Transform root = other.transform.root;
        if (!insideCounts.TryGetValue(root, out int count))
            return;

        count--;
        if (count <= 0)
            insideCounts.Remove(root);
        else
            insideCounts[root] = count;

        if (insideCounts.Count == 0)
            OnVacant();
    }

    void OnOccupied()
    {
        if (door != null)
            door.OpenDoor();

        if (onEnterRunner != null)
            onEnterRunner.TryStart();
    }

    void OnVacant()
    {
        if (door != null)
            door.CloseDoor();

        if (onExitRunner != null)
            onExitRunner.TryStart();
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
