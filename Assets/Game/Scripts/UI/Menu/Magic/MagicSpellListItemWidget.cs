using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the MagicSpellSelectorPanel — either a spell (name + MP + DMG +
/// icon) or the special "Unequip" option. Click → invokes onClicked with the
/// bound SpellItem (or null for the Unequip row).
/// </summary>
[RequireComponent(typeof(Button))]
public class MagicSpellListItemWidget : MonoBehaviour
{
    [Header("Visuals")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text mpText;
    public TMP_Text dmgText;

    [Tooltip("Small badge shown when the spell is already equipped in a " +
             "different slot on this character.")]
    public GameObject equippedElsewhereBadge;

    [Header("Format")]
    public string mpFormat = "MP {0}";
    public string dmgFormat = "DMG {0}";
    public string unequipLabel = "(unequip)";

    Button button;
    SpellItem bound;
    Action<SpellItem> onClicked;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    public void Bind(SpellItem def, bool equippedElsewhere, Action<SpellItem> onClick)
    {
        bound = def;
        onClicked = onClick;

        if (icon != null)
        {
            icon.sprite = def != null ? def.icon : null;
            icon.enabled = icon.sprite != null;
        }
        if (nameText != null)
            nameText.text = def != null ? def.displayName : "";
        if (mpText != null)
            mpText.text = def != null ? string.Format(mpFormat, def.manaCost) : "";
        if (dmgText != null)
            dmgText.text = def != null && def.displayDamage > 0 ? string.Format(dmgFormat, def.displayDamage) : "";

        if (equippedElsewhereBadge != null)
            equippedElsewhereBadge.SetActive(equippedElsewhere);
    }

    /// <summary>Bind this row as the special "Unequip" option.</summary>
    public void BindUnequip(Action<SpellItem> onClick)
    {
        bound = null;
        onClicked = onClick;

        if (icon != null) icon.enabled = false;
        if (nameText != null) nameText.text = unequipLabel;
        if (mpText != null) mpText.text = "";
        if (dmgText != null) dmgText.text = "";
        if (equippedElsewhereBadge != null) equippedElsewhereBadge.SetActive(false);
    }

    void HandleClick()
    {
        MenuManager.PlaySelectSound();
        onClicked?.Invoke(bound);
    }
}
