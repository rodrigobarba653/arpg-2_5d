using System.Collections;
using UnityEngine;

/// <summary>
/// Marks a door unlocked without consuming a key or opening it.
/// Useful for event sequences that unlock after a puzzle, before OpenDoorAction.
/// </summary>
[System.Serializable]
public class UnlockDoorAction : EventAction
{
    public SimpleDoor door;

    public override string DisplayName =>
        door != null ? $"UnlockDoor ({door.name})" : "UnlockDoor (?)";

    public override IEnumerator Execute(EventContext ctx)
    {
        if (door == null)
        {
            Debug.LogWarning("[UnlockDoorAction] Door reference is missing.", ctx.Runner);
            yield break;
        }

        // Keep current open/closed pose; only unlock for future key-free opens.
        door.SetStateImmediate(door.IsOpen, unlocked: true, writePersist: true);
        yield break;
    }
}
