using UnityEngine;

public class KillZone : MonoBehaviour
{
    // This detects when the trash bag enters the trigger zone
    void OnTriggerEnter(Collider other)
    {
        // This deletes the trash bag instantly
        Destroy(other.gameObject);
    }
}