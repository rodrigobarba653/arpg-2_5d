using UnityEngine;

/// <summary>
/// Trigger on the platform edge. Pressing the climb-down action here plays
/// the climb-off animation backwards and puts the player on the ladder.
/// </summary>
public class LadderMount : MonoBehaviour
{
    public Ladder ladder;

    [Tooltip("On: the action at this trigger climbs down onto the ladder. Off: it starts a climb from the bottom.")]
    public bool mountFromTop = true;

    void Awake()
    {
        if (!ladder)
            ladder = GetComponentInParent<Ladder>();
    }

    void OnTriggerEnter(Collider other)
    {
        var climb = other.GetComponentInParent<PlayerClimbing>();
        if (climb == null || ladder == null)
            return;

        if (mountFromTop)
            climb.SetTopMount(ladder, true);
        else
            climb.SetBottomApproach(ladder, true);
    }

    void OnTriggerExit(Collider other)
    {
        var climb = other.GetComponentInParent<PlayerClimbing>();
        if (climb == null || ladder == null)
            return;

        if (mountFromTop)
            climb.SetTopMount(ladder, false);
        else
            climb.SetBottomApproach(ladder, false);
    }
}
