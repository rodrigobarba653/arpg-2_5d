using System.Collections;
using UnityEngine;

/// <summary>
/// Global audio manager. Three channels: BGM (music), Ambient (loop ambience),
/// SFX (one-shots). All volumes are stored as 0-1 scalars and combined with a
/// master volume. UI hooks can call SetXxxVolume() later.
///
/// Place ONE GameObject called "AudioManager" in your first scene with this
/// component. It auto-creates child AudioSources and persists across scenes.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume (0-1)")]
    [Range(0f, 1f)] public float masterVolume  = 1f;
    [Range(0f, 1f)] public float bgmVolume     = 0.7f;
    [Range(0f, 1f)] public float ambientVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume     = 1f;

    [Header("Sources (auto-created if empty)")]
    public AudioSource bgmA;
    public AudioSource bgmB;
    public AudioSource ambientSource;
    public AudioSource sfxSource;

    [Header("Behavior")]
    [Tooltip("Default fade time for BGM crossfade and Ambient transitions.")]
    public float defaultFadeTime = 1.5f;

    [Tooltip("Keep this AudioManager alive across scene loads.")]
    public bool persistAcrossScenes = true;

    [Tooltip("Default mixer group for created SFX sources (optional).")]
    public UnityEngine.Audio.AudioMixerGroup defaultMixerGroup;

    bool usingBgmA = true;
    AudioSource CurrentBgm => usingBgmA ? bgmA : bgmB;
    AudioSource OtherBgm   => usingBgmA ? bgmB : bgmA;

    Coroutine bgmFadeRoutine;
    Coroutine ambientFadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        EnsureSources();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        // Live-update volume when no fade is in progress (so inspector / UI
        // sliders take effect immediately for clips already playing).
        if (CurrentBgm != null && CurrentBgm.isPlaying && bgmFadeRoutine == null)
            CurrentBgm.volume = bgmVolume * masterVolume;

        if (ambientSource != null && ambientSource.isPlaying && ambientFadeRoutine == null)
            ambientSource.volume = ambientVolume * masterVolume;
    }

    void EnsureSources()
    {
        if (!bgmA)          bgmA          = CreateSource("BGM_A",   loop: true);
        if (!bgmB)          bgmB          = CreateSource("BGM_B",   loop: true);
        if (!ambientSource) ambientSource = CreateSource("Ambient", loop: true);
        if (!sfxSource)     sfxSource     = CreateSource("SFX",     loop: false);
    }

    AudioSource CreateSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D by default
        src.outputAudioMixerGroup = defaultMixerGroup;

        return src;
    }

    // ============================================================
    // BGM
    // ============================================================

    /// <summary>Crossfade to a new BGM clip. If the same clip is already
    /// playing, does nothing.</summary>
    public void PlayBGM(AudioClip clip, float fadeTime = -1f)
    {
        if (clip == null) return;
        if (CurrentBgm.clip == clip && CurrentBgm.isPlaying) return;

        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(CrossfadeBGM(clip, fadeTime));
    }

    public void StopBGM(float fadeTime = -1f)
    {
        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(FadeOutBGM(fadeTime));
    }

    IEnumerator CrossfadeBGM(AudioClip newClip, float fadeTime)
    {
        var fadeOut = CurrentBgm;
        var fadeIn  = OtherBgm;

        fadeIn.clip = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        usingBgmA = !usingBgmA;

        float t = 0f;
        float startOutVol = fadeOut.volume;
        float targetVol = bgmVolume * masterVolume;

        if (fadeTime <= 0f) fadeTime = 0.0001f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeTime);

            fadeIn.volume  = Mathf.Lerp(0f,          targetVol, k);
            fadeOut.volume = Mathf.Lerp(startOutVol, 0f,        k);

            yield return null;
        }

        fadeIn.volume = targetVol;
        fadeOut.Stop();
        fadeOut.clip = null;

        bgmFadeRoutine = null;
    }

    IEnumerator FadeOutBGM(float fadeTime)
    {
        var src = CurrentBgm;
        if (!src.isPlaying) { bgmFadeRoutine = null; yield break; }

        float t = 0f;
        float startVol = src.volume;

        if (fadeTime <= 0f) fadeTime = 0.0001f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }

        src.Stop();
        src.clip = null;
        bgmFadeRoutine = null;
    }

    // ============================================================
    // AMBIENT
    // ============================================================

    public void PlayAmbient(AudioClip clip, float fadeTime = -1f)
    {
        if (clip == null) return;
        if (ambientSource.clip == clip && ambientSource.isPlaying) return;

        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        if (ambientFadeRoutine != null) StopCoroutine(ambientFadeRoutine);
        ambientFadeRoutine = StartCoroutine(CrossfadeAmbient(clip, fadeTime));
    }

    public void StopAmbient(float fadeTime = -1f)
    {
        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        if (ambientFadeRoutine != null) StopCoroutine(ambientFadeRoutine);
        ambientFadeRoutine = StartCoroutine(FadeOutAmbient(fadeTime));
    }

    IEnumerator CrossfadeAmbient(AudioClip newClip, float fadeTime)
    {
        float half = Mathf.Max(0.0001f, fadeTime * 0.5f);

        // Fade out current
        float startVol = ambientSource.volume;
        float t = 0f;

        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            ambientSource.volume = Mathf.Lerp(startVol, 0f, t / half);
            yield return null;
        }

        ambientSource.Stop();
        ambientSource.clip = newClip;
        ambientSource.volume = 0f;
        ambientSource.Play();

        // Fade in
        float targetVol = ambientVolume * masterVolume;
        t = 0f;

        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            ambientSource.volume = Mathf.Lerp(0f, targetVol, t / half);
            yield return null;
        }

        ambientSource.volume = targetVol;
        ambientFadeRoutine = null;
    }

    IEnumerator FadeOutAmbient(float fadeTime)
    {
        if (!ambientSource.isPlaying) { ambientFadeRoutine = null; yield break; }

        float t = 0f;
        float startVol = ambientSource.volume;

        if (fadeTime <= 0f) fadeTime = 0.0001f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            ambientSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }

        ambientSource.Stop();
        ambientSource.clip = null;
        ambientFadeRoutine = null;
    }

    // ============================================================
    // SFX
    // ============================================================

    /// <summary>Play a 2D one-shot SFX through the shared SFX source.</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume * masterVolume);
    }

    /// <summary>Play a 3D positional SFX at a world location. Creates a temporary
    /// AudioSource that destroys itself after the clip finishes.</summary>
    public AudioSource PlaySFXAt(AudioClip clip, Vector3 worldPos, float volumeScale = 1f, float spatialBlend = 1f, float minDistance = 1f, float maxDistance = 20f)
    {
        if (clip == null) return null;

        var go = new GameObject($"SFX_{clip.name}");
        go.transform.position = worldPos;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumeScale) * sfxVolume * masterVolume;
        src.spatialBlend = spatialBlend;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = defaultMixerGroup;
        src.Play();

        Destroy(go, clip.length + 0.1f);

        return src;
    }

    // ============================================================
    // VOLUME (call from UI sliders, settings menu, etc)
    // ============================================================

    public void SetMasterVolume(float v)  { masterVolume  = Mathf.Clamp01(v); }
    public void SetBGMVolume(float v)     { bgmVolume     = Mathf.Clamp01(v); }
    public void SetAmbientVolume(float v) { ambientVolume = Mathf.Clamp01(v); }
    public void SetSFXVolume(float v)     { sfxVolume     = Mathf.Clamp01(v); }

    // ============================================================
    // STATIC HELPERS (safe to call from anywhere without null checks)
    // ============================================================

    /// <summary>Plays a 3D positional SFX through the manager, falling back to a
    /// local AudioSource if the manager doesn't exist in the scene.</summary>
    public static void PlaySfxOrFallback(AudioClip clip, Vector3 worldPos, float volume = 1f, AudioSource fallback = null)
    {
        if (clip == null) return;

        if (Instance != null)
            Instance.PlaySFXAt(clip, worldPos, volume);
        else if (fallback != null)
            fallback.PlayOneShot(clip, volume);
    }

    /// <summary>Plays a 2D non-positional SFX through the manager, falling back
    /// to a local AudioSource if the manager doesn't exist.</summary>
    public static void Play2DOrFallback(AudioClip clip, float volume = 1f, AudioSource fallback = null)
    {
        if (clip == null) return;

        if (Instance != null)
            Instance.PlaySFX(clip, volume);
        else if (fallback != null)
            fallback.PlayOneShot(clip, volume);
    }
}
