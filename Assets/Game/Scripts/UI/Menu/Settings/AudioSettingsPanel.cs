using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings ▸ Audio sub-tab. Sliders wired directly to AudioManager's four
/// channels. Values are pulled from AudioManager on Refresh() (so they
/// reflect whatever was loaded from PlayerPrefs) and pushed back live as the
/// player drags — AudioManager itself persists each change.
/// </summary>
public class AudioSettingsPanel : MenuPanel
{
    [Header("Sliders (0-1)")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider ambientSlider;
    public Slider sfxSlider;

    // Guards against the listener writing back a value while we're just
    // syncing the slider FROM AudioManager (Refresh), which would otherwise
    // immediately re-save the same value — harmless but wasteful, and would
    // fire an extra PlayerPrefs.Save() every time the panel opens.
    bool syncingFromSource;

    void Awake()
    {
        if (masterSlider  != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider   != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (ambientSlider != null) ambientSlider.onValueChanged.AddListener(OnAmbientChanged);
        if (sfxSlider     != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    public override void Refresh()
    {
        if (AudioManager.Instance == null) return;

        syncingFromSource = true;
        if (masterSlider  != null) masterSlider.value  = AudioManager.Instance.masterVolume;
        if (musicSlider   != null) musicSlider.value   = AudioManager.Instance.bgmVolume;
        if (ambientSlider != null) ambientSlider.value = AudioManager.Instance.ambientVolume;
        if (sfxSlider     != null) sfxSlider.value     = AudioManager.Instance.sfxVolume;
        syncingFromSource = false;
    }

    void OnMasterChanged(float v)
    {
        if (syncingFromSource || AudioManager.Instance == null) return;
        AudioManager.Instance.SetMasterVolume(v);
    }

    void OnMusicChanged(float v)
    {
        if (syncingFromSource || AudioManager.Instance == null) return;
        AudioManager.Instance.SetBGMVolume(v);
    }

    void OnAmbientChanged(float v)
    {
        if (syncingFromSource || AudioManager.Instance == null) return;
        AudioManager.Instance.SetAmbientVolume(v);
    }

    void OnSfxChanged(float v)
    {
        if (syncingFromSource || AudioManager.Instance == null) return;
        AudioManager.Instance.SetSFXVolume(v);
    }

    public override Selectable GetFirstSelectable() => masterSlider;
}
