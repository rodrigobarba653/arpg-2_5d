using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Blends to a directed focus and keeps the camera lease.
/// Without offset: follow/look at Target (gameplay-style framing).
/// With offset: origin = current camera world position; X/Y/Z truck the camera
/// in world axes without tracking the target.
/// </summary>
[Serializable]
public class CameraFocusAction : EventAction
{
    public Transform target;

    [Min(0f)]
    public float blendSeconds = 1f;

    [Tooltip("If true, truck from the current camera by World Offset, then keep " +
             "following the Target with that framing. If false, gameplay-style follow.")]
    public bool overrideFollowOffset;

    [Tooltip("World-space truck from the current camera position. " +
             "Then the camera follows the Target keeping that offset. " +
             "+X / +Y / +Z = world axes. (0,0,0) = follow from where the camera already is.")]
    public Vector3 followOffset = Vector3.zero;

    public override string DisplayName =>
        overrideFollowOffset
            ? $"Camera Focus (follow+{followOffset.x:0.#},{followOffset.y:0.#},{followOffset.z:0.#})"
            : "Camera Focus";

    public override IEnumerator Execute(EventContext ctx)
    {
        bool keepLease = false;
        var director = CameraDirector.Ensure();
        try
        {
            if (ctx.Runner == null)
            {
                ctx.RequestStop("Camera focus requires an EventRunner.");
                yield break;
            }

            if (target == null)
            {
                ctx.RequestStop("Camera focus has no target.");
                yield break;
            }

            if (director == null)
            {
                ctx.RequestStop("Camera director could not find the gameplay camera.");
                yield break;
            }

            if (!director.TryAcquire(ctx.Runner, out string reason))
            {
                ctx.RequestStop(reason);
                yield break;
            }

            float seconds = Mathf.Max(0f, blendSeconds);
            director.PushBlend(ctx.Runner, seconds);
            director.ShowEventCamera(target, overrideFollowOffset, followOffset);
            ctx.Log(
                overrideFollowOffset
                    ? $"Camera focus '{target.name}' follow with camOffset={followOffset} blend={seconds:0.00}s."
                    : $"Camera focus '{target.name}' blend={seconds:0.00}s (gameplay framing).");

            yield return director.WaitUntilSettled(ctx, director.EventCamera, seconds);
            if (ctx.IsCancelled)
                yield break;

            if (!director.LastTransitionOk)
            {
                ctx.RequestStop("Camera focus did not reach the target.");
                yield break;
            }

            keepLease = true;
            ctx.Log("Camera focused.");
        }
        finally
        {
            if (director != null)
            {
                director.ForceRestoreDefaultBlend();
                if (!keepLease)
                    director.Release(ctx.Runner);
            }
        }
    }

    public override void Cancel(EventContext ctx)
    {
        if (ctx == null)
            return;
        CameraDirector.Existing?.Abort(ctx.Runner);
    }
}
