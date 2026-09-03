using UnityEngine;

public class HazardRespawn : MonoBehaviour
{
    public Transform respawnPoint;

    // After a teleport (SpawnPoint, SaveManager spawn, etc.) the player needs a
    // moment to fall to the floor without being yanked back by a hazard trigger.
    // Anyone who teleports the player should call SuspendForSeconds(~1.5f).
    static float suspendUntilTime = -1f;

    public static void SuspendForSeconds(float seconds)
    {
        float candidate = Time.time + seconds;
        if (candidate > suspendUntilTime) suspendUntilTime = candidate;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Skip if the player was just teleported — let them fall to the ground
        // before any hazard can grab them.
        if (Time.time < suspendUntilTime)
            return;

        Rigidbody rb = other.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        other.transform.position = respawnPoint.position;

        Physics.SyncTransforms();
    }
}