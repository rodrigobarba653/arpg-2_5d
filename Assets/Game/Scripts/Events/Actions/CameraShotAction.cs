using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Cut or blend to a Fixed or Follow shot. The brain blends two camera
/// states. The visible camera is not moved onto the next marker.
/// </summary>
[Serializable]
public class CameraShotAction : EventAction
{
    public enum ShotTransition
    {
        Cut = 0,
        Blend = 1,
    }

    public CameraShotMarker marker;
    public ShotTransition transition = ShotTransition.Cut;

    [Min(0f)]
    public float blendSeconds = 1f;

    public override string DisplayName =>
        transition == ShotTransition.Blend ? "Camera Shot (Blend)" : "Camera Shot (Cut)";

    public override IEnumerator Execute(EventContext ctx)
    {
        bool keepLease = false;
        var director = CameraDirector.Ensure();
        try
        {
            if (ctx.Runner == null)
            {
                ctx.RequestStop("Camera shot requires an EventRunner.");
                yield break;
            }

            if (marker == null)
            {
                ctx.RequestStop("Camera shot has no marker.");
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

            bool cut = transition != ShotTransition.Blend;
            float seconds = cut ? 0f : Mathf.Max(0f, blendSeconds);
            director.PushBlend(ctx.Runner, seconds);
            bool holdFrame = director.StageShot(marker);
            if (cut)
                ctx.Log($"Camera cut '{marker.name}'.");
            else
                ctx.Log($"Camera blend '{marker.name}' blend={seconds:0.00}s.");
            if (holdFrame)
                yield return null;
            if (ctx.IsCancelled)
                yield break;

            var incoming = director.PromoteStagedCut();
            if (incoming == null)
            {
                ctx.RequestStop("Camera shot had no incoming camera.");
                yield break;
            }

            yield return director.WaitUntilSettled(ctx, incoming, seconds);
            if (ctx.IsCancelled)
                yield break;

            if (!director.LastTransitionOk)
            {
                ctx.RequestStop(cut
                    ? "Camera cut did not reach the marker."
                    : "Camera blend did not reach the marker.");
                yield break;
            }

            keepLease = true;
            ctx.Log(cut ? "Camera cut settled." : "Camera blend settled.");
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
