using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One clickable row in the EquipmentPanel — represents one party member.
/// Shows portrait + name + a preview of the currently equipped weapon.
/// Click → invokes the onClicked callback with this row's PlayerHealth.
///
/// Build the prefab with: Button on the root, child Image for portrait, child
/// TMP_Text for name, child Image for weapon icon.
/// </summary>
[RequireComponent(typeof(Button))]
public class CharacterEquipmentRowWidget : MonoBehaviour
{
    [Header("Visuals")]
    public Image portrait;
    public TMP_Text nameText;

    [Header("Equipped Weapon Preview (optional)")]
    public Image weaponIcon;
    public TMP_Text weaponNameText;

    [Header("Resource Stats (format CURRENT/MAX)")]
    public TMP_Text hpText;
    public TMP_Text mpText;
    public TMP_Text apText;

    [Header("Combat Stats (from PlayerEquipment)")]
    public TMP_Text strText;
    public TMP_Text defText;
    public TMP_Text spdText;

    [Header("Display Format")]
    public string statFormat = "{0}/{1}";
    public string intStatFormat = "{0}";
    public string floatStatFormat = "{0:0.00}";

    Button button;
    PlayerHealth bound;
    Action<PlayerHealth> onClicked;
    PlayerEquipment boundEq;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    void OnDestroy()
    {
        if (bound != null) bound.OnStatsChanged -= Refresh;
        if (boundEq != null)
        {
            boundEq.OnWeaponChanged -= HandleWeaponChanged;
            boundEq.OnPartsChanged  -= Refresh;
        }
    }

    public void Bind(PlayerHealth player, Action<PlayerHealth> onClick)
    {
        // Unhook previous bindings.
        if (bound != null) bound.OnStatsChanged -= Refresh;
        if (boundEq != null)
        {
            boundEq.OnWeaponChanged -= HandleWeaponChanged;
            boundEq.OnPartsChanged  -= Refresh;
            boundEq = null;
        }

        bound = player;
        onClicked = onClick;

        if (bound != null)
        {
            bound.OnStatsChanged += Refresh;
            boundEq = bound.GetComponent<PlayerEquipment>();
            if (boundEq != null)
            {
                boundEq.OnWeaponChanged += HandleWeaponChanged;
                boundEq.OnPartsChanged  += Refresh;
            }
        }

        Refresh();
    }

    void HandleWeaponChanged(WeaponItem _) => Refresh();

    void Refresh()
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
        if (nameText != null) nameText.text = def != null ? def.displayName : bound.name;

        var w = boundEq != null ? boundEq.CurrentWeapon : null;
        if (weaponIcon != null)
        {
            weaponIcon.sprite = w != null ? w.icon : null;
            weaponIcon.enabled = weaponIcon.sprite != null;
        }
        if (weaponNameText != null)
            weaponNameText.text = w != null ? w.displayName : "(no weapon)";

        // Resource stats from PlayerHealth.
        if (hpText != null) hpText.text = string.Format(statFormat, bound.currentHealth, bound.maxHealth);
        if (mpText != null) mpText.text = string.Format(statFormat, bound.currentMana,   bound.maxMana);
        if (apText != null) apText.text = string.Format(statFormat, bound.currentAP,     bound.maxAP);

        // Combat stats from PlayerEquipment.
        if (strText != null)
            strText.text = string.Format(intStatFormat, boundEq != null ? boundEq.TotalDamage : 0);
        if (defText != null)
            defText.text = string.Format(intStatFormat, boundEq != null ? boundEq.TotalDefense : 0);
        if (spdText != null)
            spdText.text = string.Format(floatStatFormat, boundEq != null ? boundEq.TotalAttackSpeed : 0f);
    }

    void ClearVisuals()
    {
        if (portrait != null)       portrait.enabled = false;
        if (nameText != null)       nameText.text = "";
        if (weaponIcon != null)     weaponIcon.enabled = false;
        if (weaponNameText != null) weaponNameText.text = "";
        if (hpText != null)         hpText.text = "";
        if (mpText != null)         mpText.text = "";
        if (apText != null)         apText.text = "";
        if (strText != null)        strText.text = "";
        if (defText != null)        defText.text = "";
        if (spdText != null)        spdText.text = "";
    }

    void HandleClick()
    {
        if (bound == null) return;
        MenuManager.PlaySelectSound();
        onClicked?.Invoke(bound);
    }
}
