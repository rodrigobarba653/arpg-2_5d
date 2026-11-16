using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Blends back to the gameplay camera and ends the lease.
/// No lease: finishes immediately.
/// </summary>
[Serializable]
public class CameraRestoreAction : EventAction
{
    [Min(0f)]
    public float blendSeconds = 1f;

    public override string DisplayName => "Camera Restore";

    public override IEnumerator Execute(EventContext ctx)
    {
        bool finished = false;
        var director = CameraDirector.Existing;
        try
        {
            if (ctx.Runner == null)
            {
                ctx.RequestStop("Camera restore requires an EventRunner.");
                yield break;
            }

            if (director == null || !director.HasLease(ctx.Runner))
            {
                ctx.Log("Camera restore skipped. No lease.");
                finished = true;
                yield break;
            }

            var gameplay = director.GameplayCamera;
            if (gameplay == null)
            {
                ctx.RequestStop("Camera restore could not find the gameplay camera.");
                yield break;
            }

            float seconds = Mathf.Max(0f, blendSeconds);
            director.PushBlend(ctx.Runner, seconds);
            director.HideEventCamera();
            ctx.Log($"Camera restore blend={seconds:0.00}s.");

            yield return director.WaitUntilSettled(ctx, gameplay, seconds);
            if (ctx.IsCancelled)
                yield break;

            if (!director.LastTransitionOk)
            {
                ctx.RequestStop("Camera restore did not reach the gameplay camera.");
                yield break;
            }

            director.Release(ctx.Runner);
            finished = true;
            ctx.Log("Camera restored.");
        }
        finally
        {
            if (director != null)
            {
                director.ForceRestoreDefaultBlend();
                if (!finished)
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
