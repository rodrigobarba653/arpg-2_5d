using UnityEngine;

/// <summary>
/// Helper to drive BGM and/or Ambient from a GameObject without writing code.
///
/// Two modes:
///   - On Start  → fires immediately when the scene loads (good for the level's
///                 main music).
///   - On Trigger → fires when the player enters/exits a trigger volume (good
///                  for sub-areas like caves, towns, boss rooms).
/// </summary>
public class AreaAudio : MonoBehaviour
{
    public enum Mode
    {
        OnStart,
        OnTrigger
    }

    [Header("Mode")]
    public Mode mode = Mode.OnStart;

    [Header("Clips")]
    public AudioClip bgmClip;
    public AudioClip ambientClip;

    [Header("Fade")]
    [Tooltip("Crossfade time. -1 = use AudioManager.defaultFadeTime.")]
    public float fadeTime = -1f;

    [Header("On Trigger Options")]
    [Tooltip("If true, when the player exits the trigger, stop the BGM (fade out).")]
    public bool stopBgmOnExit = false;

    [Tooltip("If true, when the player exits the trigger, stop the Ambient (fade out).")]
    public bool stopAmbientOnExit = true;

    [Tooltip("If true, only fires the first time the player enters.")]
    public bool oneShot = false;

    bool spent;

    void Start()
    {
        if (mode == Mode.OnStart)
            ApplyClips();
    }

    void OnTriggerEnter(Collider other)
    {
        if (mode != Mode.OnTrigger) return;
        if (spent) return;

        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null && !other.CompareTag("Player")) return;

        ApplyClips();

        if (oneShot) spent = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (mode != Mode.OnTrigger) return;

        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null && !other.CompareTag("Player")) return;

        if (AudioManager.Instance == null) return;

        if (stopBgmOnExit && bgmClip != null)
            AudioManager.Instance.StopBGM(fadeTime);

        if (stopAmbientOnExit && ambientClip != null)
            AudioManager.Instance.StopAmbient(fadeTime);
    }

    void ApplyClips()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning($"AreaAudio '{name}' could not find an AudioManager in the scene.", this);
            return;
        }

        if (bgmClip != null)
            AudioManager.Instance.PlayBGM(bgmClip, fadeTime);

        if (ambientClip != null)
            AudioManager.Instance.PlayAmbient(ambientClip, fadeTime);
    }
}
