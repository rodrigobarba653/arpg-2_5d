using System.Collections;
using UnityEngine;

[System.Serializable]
public class OpenDoorAction : EventAction
{
    public SimpleDoor door;

    [Tooltip("If true, wait until the door finishes moving before the next action.")]
    public bool waitForCompletion = true;

    public override string DisplayName =>
        door != null ? $"OpenDoor ({door.name})" : "OpenDoor (?)";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (door == null)
        {
            Debug.LogWarning("[OpenDoorAction] Door reference is missing.", ctx.Runner);
            yield break;
        }

        if (!door.IsOpen)
            door.OpenDoor();

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
