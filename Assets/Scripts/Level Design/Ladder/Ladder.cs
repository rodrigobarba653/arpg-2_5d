using UnityEngine;

public class Ladder : MonoBehaviour
{
    public Transform topPoint;
    public Collider ladderTrigger; // 👈 NUEVO

    void Awake()
    {
        if (!ladderTrigger)
            ladderTrigger = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerClimbing climb = other.GetComponent<PlayerClimbing>();

        if (climb != null)
        {
            climb.EnterClimb(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerClimbing climb = other.GetComponent<PlayerClimbing>();

        if (climb != null && climb.IsClimbing())
        {
            climb.ExitClimb();
        }
    }
}