using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Changes the gameplay Cinemachine Follow offset without leaving Follow mode
/// and without taking the directed CameraDirector lease.
/// </summary>
[Serializable]
public class GameplayFollowOffsetAction : EventAction
{
    public enum TuneMode
    {
        Apply = 0,
        Restore = 1,
    }

    public TuneMode mode = TuneMode.Apply;

    [Tooltip("New FollowOffset on the gameplay CinemachineFollow. Ignored when Restore.")]
    public Vector3 followOffset = new Vector3(0f, 4.5f, -9.3f);

    [Min(0f)]
    public float blendSeconds = 0.75f;

    public override string DisplayName =>
        mode == TuneMode.Restore
            ? $"Gameplay Follow Restore ({blendSeconds:0.##}s)"
            : $"Gameplay Follow Offset ({blendSeconds:0.##}s)";

    public override IEnumerator Execute(EventContext ctx)
    {
        var director = CameraDirector.Ensure();
        if (director == null)
        {
            ctx.RequestStop("Gameplay follow tune could not find CameraDirector / gameplay camera.");
            yield break;
        }

        float seconds = Mathf.Max(0f, blendSeconds);
        bool ok;
        string reason;
        if (mode == TuneMode.Restore)
        {
            ok = director.TryRestoreGameplayFollowOffset(seconds, out reason);
            ctx.Log($"Gameplay follow restore blend={seconds:0.00}s.");
        }
        else
        {
            ok = director.TryBlendGameplayFollowOffset(followOffset, seconds, out reason);
            ctx.Log($"Gameplay follow offset → {followOffset} blend={seconds:0.00}s.");
        }

        if (!ok)
        {
            ctx.RequestStop(reason);
            yield break;
        }

        // Blend runs on CameraDirector; wait wall-clock so the event step feels settled.
        float remaining = seconds;
        while (remaining > 0f)
        {
            if (ctx.IsCancelled)
                yield break;
            remaining -= Time.deltaTime;
            yield return null;
        }
    }
}
