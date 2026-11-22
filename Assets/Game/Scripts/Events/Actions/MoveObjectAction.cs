using System.Collections;
using UnityEngine;

/// <summary>
/// Animates a <see cref="MovableObject"/> (ladder, rock, bridge, …) toward Rest or Active.
/// Pair with Persist State + flagId on the component (or a separate Set Flag) so save/load snaps.
/// </summary>
[System.Serializable]
public class MoveObjectAction : EventAction
{
    public enum TargetState
    {
        ToActive = 0,
        ToRest = 1,
        Toggle = 2,
    }

    public MovableObject target;

    public TargetState state = TargetState.ToActive;

    [Tooltip("If true, wait until the object finishes moving before the next action.")]
    public bool waitForCompletion = true;

    public override string DisplayName
    {
        get
        {
            string name = target != null ? target.name : "?";
            string verb = state switch
            {
                TargetState.ToRest => "Rest",
                TargetState.Toggle => "Toggle",
                _ => "Active",
            };
            return $"Move Object ({name} → {verb})";
        }
    }

    public override IEnumerator Execute(EventContext ctx)
    {
        if (target == null)
        {
            Debug.LogWarning("[MoveObjectAction] Target MovableObject is missing.", ctx != null ? ctx.Runner : null);
            yield break;
        }

        switch (state)
        {
            case TargetState.ToRest:
                target.MoveToRest();
                break;
            case TargetState.Toggle:
                target.Toggle();
                break;
            default:
                target.MoveToActive();
                break;
        }

        if (ctx != null && ctx.Runner != null && ctx.Runner.debugLog)
            ctx.Log($"Move Object '{target.name}' → {state}");

        if (!waitForCompletion)
            yield break;

        while (target != null && target.IsMoving)
        {
            if (ctx != null && ctx.IsCancelled)
                yield break;
            yield return null;
        }
    }
}
