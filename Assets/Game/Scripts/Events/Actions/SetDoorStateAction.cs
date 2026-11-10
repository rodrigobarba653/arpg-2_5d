using System.Collections;
using UnityEngine;

[System.Serializable]
public class SetDoorStateAction : EventAction
{
    public SimpleDoor door;
    public bool open = true;

    [Tooltip("If true, play the normal open/close motion and optionally wait. " +
             "If false, snap immediately (no sound / UnityEvents).")]
    public bool animate = true;

    public bool waitForCompletion = true;

    public override string DisplayName =>
        door != null
            ? $"SetDoor ({door.name}) → {(open ? "Open" : "Closed")}"
            : "SetDoorState (?)";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (door == null)
        {
            Debug.LogWarning("[SetDoorStateAction] Door reference is missing.", ctx.Runner);
            yield break;
        }

        if (!animate)
        {
            door.SetStateImmediate(open, unlocked: open || door.IsUnlocked);
            yield break;
        }

        if (open)
        {
            if (!door.IsOpen)
                door.OpenDoor();
        }
        else
        {
            if (door.IsOpen)
                door.CloseDoor();
        }

        if (waitForCompletion)
        {
            while (door != null && door.IsMoving)
            {
                if (ctx.IsCancelled)
                    yield break;
                yield return null;
            }
        }
    }
}
