using UnityEngine;

public class PlayerWaterDetector : MonoBehaviour
{
    PlayerSwimming swimming;

    int waterContacts = 0;

    void Awake()
    {
        swimming = GetComponentInParent<PlayerSwimming>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Water"))
            return;

        waterContacts++;

        Debug.Log($"[PlayerWaterDetector] Entered water '{other.name}' (contacts={waterContacts}).", this);

        if (waterContacts == 1)
        {
            if (swimming == null)
                Debug.LogWarning("[PlayerWaterDetector] No PlayerSwimming on parent — nothing to call.", this);
            else
                swimming.EnterWater();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Water"))
            return;

        waterContacts--;

        if (waterContacts <= 0)
        {
            waterContacts = 0;
            swimming?.ExitWater();
        }
    }
}