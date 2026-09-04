using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings ▸ Gameplay sub-tab. Currently just the Combat Style toggle:
///   OFF (Active)  — no slow-mo on the spell wheel, casting is instant.
///   ON  (Relaxed) — the world slows down while the wheel is expanded
///                   (actively cycling spells), giving more time to pick.
/// Backed by GameSettings (PlayerPrefs) — MagicCastInput reads
/// GameSettings.RelaxedCombatMode directly, no extra wiring needed there.
/// </summary>
public class GameplaySettingsPanel : MenuPanel
{
    [Header("Combat Style")]
    public Toggle relaxedCombatToggle;

    [Tooltip("Optional label reflecting the current mode as text.")]
    public TMP_Text modeLabel;

    public string activeLabel = "Active";
    public string relaxedLabel = "Relaxed";

    bool syncingFromSource;

    void Awake()
    {
        if (relaxedCombatToggle != null)
            relaxedCombatToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    public override void Refresh()
    {
        syncingFromSource = true;
        if (relaxedCombatToggle != null)
            relaxedCombatToggle.isOn = GameSettings.RelaxedCombatMode;
        syncingFromSource = false;

        UpdateLabel();
    }

    void OnToggleChanged(bool isOn)
    {
        if (syncingFromSource) return;
        GameSettings.RelaxedCombatMode = isOn;
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (modeLabel == null) return;
        modeLabel.text = GameSettings.RelaxedCombatMode ? relaxedLabel : activeLabel;
    }

    public override Selectable GetFirstSelectable() => relaxedCombatToggle;
}
