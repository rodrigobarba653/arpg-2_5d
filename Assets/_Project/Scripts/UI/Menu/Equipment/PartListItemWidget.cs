using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the PartSelectorPanel — represents one WeaponPart the player
/// owns. Shows icon + name + the stat the part contributes + quantity in
/// inventory. Click → invokes the onClicked callback.
/// </summary>
[RequireComponent(typeof(Button))]
public class PartListItemWidget : MonoBehaviour
{
    [Header("Visuals")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text statText;
    public TMP_Text quantityText;

    [Header("Format")]
    public string quantityFormat = "x{0}";

    Button button;
    WeaponPart bound;
    Action<WeaponPart> onClicked;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    public void Bind(WeaponPart part, int quantity, Action<WeaponPart> onClick)
    {
        bound = part;
        onClicked = onClick;

        if (icon != null)
        {
            icon.sprite = part != null ? part.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (nameText != null)
            nameText.text = part != null ? part.displayName : "";

        if (statText != null)
            statText.text = part != null ? FormatStat(part) : "";

        if (quantityText != null)
            quantityText.text = quantity > 1 ? string.Format(quantityFormat, quantity) : "";
    }

    static string FormatStat(WeaponPart p)
    {
        switch (p.partKind)
        {
            case WeaponPartKind.Blade:  return $"+{p.damageBonus} DMG";
            case WeaponPartKind.Guard:  return $"+{p.defenseBonus} DEF";
            case WeaponPartKind.Handle: return $"+{p.attackSpeedBonus:0.00} SPD";
            default: return "";
        }
    }

    void HandleClick()
    {
        if (bound == null) return;
        MenuManager.PlaySelectSound();
        onClicked?.Invoke(bound);
    }
}
