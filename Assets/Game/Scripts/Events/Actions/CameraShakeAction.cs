using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Plays one Impulse shake and waits until it ends. Does not take the
/// camera lease and does not write the gameplay Perlin.
/// </summary>
[Serializable]
public class CameraShakeAction : EventAction
{
    [Min(0f)]
    public float amplitude = 2f;

    [Min(0.05f)]
    public float duration = 0.4f;

    [Min(0.01f)]
    public float frequency = 1f;

    public override string DisplayName => $"Camera Shake ({duration:0.##}s)";

    CinemachineImpulseManager.ImpulseEvent activeEvent;

    public override IEnumerator Execute(EventContext ctx)
    {
        bool finished = false;
        try
        {
            if (ctx.Runner == null)
            {
                ctx.RequestStop("Camera shake requires an EventRunner.");
                yield break;
            }

            var director = CameraDirector.Ensure();
            if (director == null)
            {
                ctx.RequestStop("Camera shake could not find the gameplay camera.");
                yield break;
            }

            float seconds = Mathf.Max(0.05f, duration);
            float gain = Mathf.Max(0f, amplitude);
            float rate = Mathf.Max(0.01f, frequency);
            activeEvent = director.TryPlayEventShake(gain, rate, seconds, out string reason);
            if (activeEvent == null)
            {
                ctx.RequestStop(reason);
                yield break;
            }

            ctx.Log($"Camera shake amplitude={gain:0.00} duration={seconds:0.00}s frequency={rate:0.00}.");
            float end = activeEvent.StartTime + seconds;
            while (CinemachineImpulseManager.Instance.CurrentTime < end)
            {
                if (ctx.IsCancelled)
                {
                    StopShake();
                    yield break;
                }

                yield return null;
            }

            if (ctx.IsCancelled)
            {
                StopShake();
                yield break;
            }

            finished = true;
            activeEvent = null;
            ctx.Log("Camera shake settled.");
        }
        finally
        {
            if (!finished)
                StopShake();
        }
    }

    public override void Cancel(EventContext ctx)
    {
        StopShake();
    }

    void StopShake()
    {
        var shake = activeEvent;
        activeEvent = null;
        if (shake == null)
            return;

        shake.Cancel(CinemachineImpulseManager.Instance.CurrentTime, true);
    }
}
