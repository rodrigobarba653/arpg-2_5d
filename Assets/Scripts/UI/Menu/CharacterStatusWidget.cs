using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual widget for ONE character's status row: portrait, name, class,
/// HP/MP/AP and element. Binds directly to a PlayerHealth (which holds both
/// the identity reference and the stats).
/// </summary>
public class CharacterStatusWidget : MonoBehaviour
{
    [Header("Header")]
    public Image portrait;
    public TMP_Text nameText;
    public TMP_Text classText;

    [Header("Stats (format CURRENT/MAX)")]
    public TMP_Text hpText;
    public TMP_Text mpText;
    public TMP_Text apText;

    [Header("Combat Stats (from PlayerEquipment)")]
    [Tooltip("STR = TotalDamage (weapon + blade-kind parts).")]
    public TMP_Text strText;

    [Tooltip("DEF = TotalDefense (guard-kind parts).")]
    public TMP_Text defText;

    [Tooltip("SPD = TotalAttackSpeed (weapon + handle-kind parts).")]
    public TMP_Text spdText;

    [Header("Element")]
    public TMP_Text elementText;
    public Image elementIcon;

    [Header("Display Format")]
    public string statFormat = "{0}/{1}";

    [Tooltip("Format for STR / DEF (integer stats).")]
    public string intStatFormat = "{0}";

    [Tooltip("Format for SPD (float stat).")]
    public string floatStatFormat = "{0:0.00}";

    PlayerHealth bound;
    PlayerEquipment boundEquipment;

    public void Bind(PlayerHealth health)
    {
        // Unhook previous bindings.
        if (bound != null) bound.OnStatsChanged -= Refresh;
        if (boundEquipment != null)
        {
            boundEquipment.OnWeaponChanged -= HandleWeaponChanged;
            boundEquipment.OnPartsChanged  -= Refresh;
        }

        bound = health;
        boundEquipment = bound != null ? bound.GetComponent<PlayerEquipment>() : null;

        if (bound != null)
            bound.OnStatsChanged += Refresh;

        if (boundEquipment != null)
        {
            boundEquipment.OnWeaponChanged += HandleWeaponChanged;
            boundEquipment.OnPartsChanged  += Refresh;
        }

        Refresh();
    }

    void HandleWeaponChanged(WeaponItem _) => Refresh();

    void OnDestroy()
    {
        if (bound != null) bound.OnStatsChanged -= Refresh;
        if (boundEquipment != null)
        {
            boundEquipment.OnWeaponChanged -= HandleWeaponChanged;
            boundEquipment.OnPartsChanged  -= Refresh;
        }
    }

    public void Refresh()
    {
        if (bound == null)
        {
            ClearVisuals();
            return;
        }

        var def = bound.character;

        if (portrait != null)
        {
            portrait.sprite = def != null ? def.portrait : null;
            portrait.enabled = portrait.sprite != null;
        }

        if (nameText  != null) nameText.text  = def != null ? def.displayName : bound.name;
        if (classText != null) classText.text = def != null ? def.className : "";

        if (hpText != null) hpText.text = string.Format(statFormat, bound.currentHealth, bound.maxHealth);
        if (mpText != null) mpText.text = string.Format(statFormat, bound.currentMana,   bound.maxMana);
        if (apText != null) apText.text = string.Format(statFormat, bound.currentAP,     bound.maxAP);

        // Combat stats from PlayerEquipment (weapon base + equipped parts).
        if (strText != null)
            strText.text = string.Format(intStatFormat, boundEquipment != null ? boundEquipment.TotalDamage : 0);
        if (defText != null)
            defText.text = string.Format(intStatFormat, boundEquipment != null ? boundEquipment.TotalDefense : 0);
        if (spdText != null)
            spdText.text = string.Format(floatStatFormat, boundEquipment != null ? boundEquipment.TotalAttackSpeed : 0f);

        if (elementText != null)
            elementText.text = def != null ? def.element.ToString().ToUpper() : "";

        if (elementIcon != null)
        {
            elementIcon.sprite = def != null ? def.elementIcon : null;
            elementIcon.enabled = elementIcon.sprite != null;
        }
    }

    void ClearVisuals()
    {
        if (portrait != null)    portrait.enabled = false;
        if (nameText != null)    nameText.text = "";
        if (classText != null)   classText.text = "";
        if (hpText != null)      hpText.text = "";
        if (mpText != null)      mpText.text = "";
        if (apText != null)      apText.text = "";
        if (strText != null)     strText.text = "";
        if (defText != null)     defText.text = "";
        if (spdText != null)     spdText.text = "";
        if (elementText != null) elementText.text = "";
        if (elementIcon != null) elementIcon.enabled = false;
    }
}
