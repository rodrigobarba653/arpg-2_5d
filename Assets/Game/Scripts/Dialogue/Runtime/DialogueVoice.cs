using UnityEngine;

/// <summary>
/// Plays / stops per-line dialogue voice through <see cref="AudioManager"/>.
/// One voice channel — a new line replaces the previous clip.
/// </summary>
public static class DialogueVoice
{
    public static void PlayLine(DialogueLineNode line)
    {
        if (line == null || line.voiceClip == null)
        {
            Stop();
            return;
        }

        float vol = line.voiceVolume > 0f ? line.voiceVolume : 1f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayVoice(line.voiceClip, vol);
        else
            AudioManager.Play2DOrFallback(line.voiceClip, vol);
    }

    public static void Stop()
    {
        AudioManager.Instance?.StopVoice();
    }
}
