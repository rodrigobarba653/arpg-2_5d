using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Blends to a target and keeps the camera lease for later actions.
/// </summary>
[Serializable]
public class CameraFocusAction : EventAction
{
    public Transform target;

    [Min(0f)]
    public float blendSeconds = 1f;

    public override string DisplayName => "Camera Focus";

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
            director.ShowEventCamera(target);
            ctx.Log($"Camera focus '{target.name}' blend={seconds:0.00}s.");

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
