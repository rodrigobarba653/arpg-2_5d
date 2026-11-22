using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Returns the camera to normal gameplay: ends a directed lease (shot/focus) if
/// this runner holds one, and restores any Gameplay Follow Offset tune.
/// </summary>
[Serializable]
public class CameraRestoreAction : EventAction
{
    [Tooltip("0 = cut back to gameplay. >0 = blend over that many seconds.")]
    [Min(0f)]
    public float blendSeconds = 1f;

    public override string DisplayName =>
        blendSeconds <= 0.0001f
            ? "Camera Restore (Cut)"
            : $"Camera Restore (Blend {blendSeconds:0.##}s)";

    public override IEnumerator Execute(EventContext ctx)
    {
        bool finished = false;
        var director = CameraDirector.Ensure();
        try
        {
            if (ctx.Runner == null)
            {
                ctx.RequestStop("Camera restore requires an EventRunner.");
                yield break;
            }

            if (director == null)
            {
                ctx.RequestStop("Camera restore could not find CameraDirector.");
                yield break;
            }

            float seconds = Mathf.Max(0f, blendSeconds);
            bool hasLease = director.HasLease(ctx.Runner);
            bool restoredFollow = director.TryRestoreGameplayFollowOffset(seconds, out string followReason);

            if (!hasLease && !restoredFollow)
            {
                // Nothing to undo (no shot lease, no follow-offset tune).
                ctx.Log(
                    string.IsNullOrEmpty(followReason)
                        ? "Camera restore skipped. Nothing to restore."
                        : $"Camera restore skipped. {followReason}");
                finished = true;
                yield break;
            }

            if (restoredFollow)
                ctx.Log($"Gameplay follow offset restore blend={seconds:0.00}s.");

            if (hasLease)
            {
                var gameplay = director.GameplayCamera;
                if (gameplay == null)
                {
                    ctx.RequestStop("Camera restore could not find the gameplay camera.");
                    yield break;
                }

                director.PushBlend(ctx.Runner, seconds);
                director.HideEventCamera();
                ctx.Log($"Camera lease restore blend={seconds:0.00}s.");

                yield return director.WaitUntilSettled(ctx, gameplay, seconds);
                if (ctx.IsCancelled)
                    yield break;

                if (!director.LastTransitionOk)
                {
                    ctx.RequestStop("Camera restore did not reach the gameplay camera.");
                    yield break;
                }

                director.Release(ctx.Runner);
            }
            else
            {
                // Follow-offset-only path: wait the blend on wall clock.
                float remaining = seconds;
                while (remaining > 0f)
                {
                    if (ctx.IsCancelled)
                        yield break;
                    remaining -= Time.deltaTime;
                    yield return null;
                }
            }

            finished = true;
            ctx.Log("Camera restored.");
        }
        finally
        {
            if (director != null)
            {
                director.ForceRestoreDefaultBlend();
                if (!finished && ctx?.Runner != null)
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
