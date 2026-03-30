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

        if (waterContacts == 1)
        {
            swimming?.EnterWater();
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