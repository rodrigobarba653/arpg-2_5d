using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Moves the live directed camera toward a marker. Does not switch slots
/// and does not start a Brain blend.
/// </summary>
[Serializable]
public class CameraMoveAction : EventAction
{
    public enum MoveEase
    {
        SmoothStep = 0,
        Linear = 1,
    }

    public CameraShotMarker destination;

    [Min(0f)]
    public float duration = 2f;

    public MoveEase ease = MoveEase.SmoothStep;

    public override string DisplayName => $"Camera Move ({duration:0.##}s)";

    public override IEnumerator Execute(EventContext ctx)
    {
        bool finished = false;
        var director = CameraDirector.Existing;
        try
        {
            if (ctx.Runner == null)
            {
                ctx.RequestStop("Camera move requires an EventRunner.");
                yield break;
            }

            if (director == null || !director.HasLease(ctx.Runner))
            {
                ctx.RequestStop("Camera move requires the camera lease.");
                yield break;
            }

            if (!director.TryBeginMove(ctx.Runner, destination, out string reason))
            {
                ctx.RequestStop(reason);
                yield break;
            }

            float seconds = Mathf.Max(0f, duration);
            string name = destination != null ? destination.name : "(none)";
            ctx.Log($"Camera move '{name}' duration={seconds:0.00}s.");
            yield return director.RunMove(ctx, seconds, ease != MoveEase.Linear);
            if (ctx.IsCancelled)
                yield break;

            if (!director.LastMoveOk)
            {
                ctx.RequestStop("Camera move did not reach the marker.");
                yield break;
            }

            finished = true;
            ctx.Log("Camera move settled.");
        }
        finally
        {
            if (director != null && !finished)
            {
                director.InvalidateMove();
                director.Release(ctx.Runner);
            }
        }
    }

    public override void Cancel(EventContext ctx)
    {
        if (ctx == null)
            return;
        var director = CameraDirector.Existing;
        if (director == null)
            return;
        director.InvalidateMove();
        director.Abort(ctx.Runner);
    }
}
