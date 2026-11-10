using System.Collections;
using UnityEngine;

[System.Serializable]
public class CloseDoorAction : EventAction
{
    public SimpleDoor door;

    [Tooltip("If true, wait until the door finishes moving before the next action.")]
    public bool waitForCompletion = true;

    public override string DisplayName =>
        door != null ? $"CloseDoor ({door.name})" : "CloseDoor (?)";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (door == null)
        {
            Debug.LogWarning("[CloseDoorAction] Door reference is missing.", ctx.Runner);
            yield break;
        }

        if (door.IsOpen)
            door.CloseDoor();

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
