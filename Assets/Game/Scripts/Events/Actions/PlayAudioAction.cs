using System.Collections;
using UnityEngine;

/// <summary>
/// Plays an AudioClip via <see cref="AudioManager"/> (SFX or stoppable Voice).
/// Optional wait-until-finished for cutscene timing.
/// </summary>
[System.Serializable]
public class PlayAudioAction : EventAction
{
    public enum Channel
    {
        Sfx2D = 0,
        Sfx3DAtRunner = 1,
        Voice = 2,
    }

    public AudioClip clip;

    public Channel channel = Channel.Sfx2D;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Hold the sequence until the clip finishes (Voice / managed SFX).")]
    public bool waitUntilFinished;

    [Tooltip("Stop this clip if the EventRunner cancels (Voice / managed wait clips).")]
    public bool stopOnCancel = true;

    AudioSource playing;

    public override string DisplayName
    {
        get
        {
            string name = clip != null ? clip.name : "(no clip)";
            string mode = channel == Channel.Voice ? "Voice" : channel == Channel.Sfx3DAtRunner ? "SFX 3D" : "SFX";
            return waitUntilFinished ? $"Play Audio [{mode}] {name} (wait)" : $"Play Audio [{mode}] {name}";
        }
    }

    public override IEnumerator Execute(EventContext ctx)
    {
        playing = null;
        if (clip == null)
        {
            ctx.Log("Play Audio skipped — no clip.");
            yield break;
        }

        float vol = Mathf.Clamp01(volume);

        if (channel == Channel.Voice)
        {
            if (AudioManager.Instance != null)
                playing = AudioManager.Instance.PlayVoice(clip, vol);
            else
                AudioManager.Play2DOrFallback(clip, vol);
        }
        else if (channel == Channel.Sfx3DAtRunner)
        {
            Vector3 pos = ctx.Runner != null ? ctx.Runner.transform.position : Vector3.zero;
            if (AudioManager.Instance != null)
                playing = AudioManager.Instance.PlaySFXAt(clip, pos, vol);
            else
                AudioManager.PlaySfxOrFallback(clip, pos, vol);
        }
        else
        {
            // SFX 2D — managed source when waiting/stopping; else one-shot.
            if (waitUntilFinished || stopOnCancel)
            {
                if (AudioManager.Instance != null)
                    playing = AudioManager.Instance.PlaySFXManaged(clip, vol);
                else
                    AudioManager.Play2DOrFallback(clip, vol);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clip, vol);
            }
            else
            {
                AudioManager.Play2DOrFallback(clip, vol);
            }
        }

        if (!waitUntilFinished)
            yield break;

        float timeout = clip.length + 0.25f;
        float t = 0f;
        while (t < timeout && !ctx.IsCancelled)
        {
            bool stillPlaying = channel == Channel.Voice
                ? AudioManager.Instance != null && AudioManager.Instance.IsVoicePlaying
                : playing != null && playing.isPlaying;

            if (!stillPlaying)
                break;

            t += Time.deltaTime;
            yield return null;
        }
    }

    public override void Cancel(EventContext ctx)
    {
        if (!stopOnCancel)
            return;

        if (channel == Channel.Voice)
        {
            AudioManager.Instance?.StopVoice();
            return;
        }

        if (playing != null)
        {
            playing.Stop();
            if (playing.gameObject != null && playing.gameObject.name.StartsWith("SFX"))
                Object.Destroy(playing.gameObject);
            playing = null;
        }
    }
}
