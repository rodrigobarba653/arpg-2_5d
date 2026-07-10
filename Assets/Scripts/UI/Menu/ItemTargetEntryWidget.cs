using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the ItemTargetSelector — shows a character's portrait + HP and
/// emits OnSelected when clicked.
/// </summary>
[RequireComponent(typeof(Button))]
public class ItemTargetEntryWidget : MonoBehaviour
{
    [Header("Visual")]
    public Image portrait;
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text mpText;

    [Header("Format (HP / MP)")]
    public string statFormat = "{0}/{1}";

    [Header("Disabled Look")]
    [Tooltip("Label alpha when the pending item CANNOT be used on this target.")]
    [Range(0f, 1f)]
    public float disabledAlpha = 0.4f;

    Button button;
    PlayerHealth target;
    bool canUseNow;

    public PlayerHealth Target => target;

    /// <summary>Fired when the row is clicked. Receives the target PlayerHealth.</summary>
    public event Action<PlayerHealth> OnSelected;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    /// <summary>Bind to a target. pendingItem is used to dim the entry if the
    /// item can't be used on this target (player at full HP, dead, etc).</summary>
    public void Bind(PlayerHealth player, ItemDefinition pendingItem)
    {
        target = player;

        var def = player != null ? player.character : null;

        if (portrait != null)
        {
            portrait.sprite = def != null ? def.portrait : null;
            portrait.enabled = portrait.sprite != null;
        }

        if (nameText != null)
            nameText.text = def != null ? def.displayName : (player != null ? player.name : "?");

        if (hpText != null && player != null)
            hpText.text = string.Format(statFormat, player.currentHealth, player.maxHealth);

        if (mpText != null && player != null)
            mpText.text = string.Format(statFormat, player.currentMana, player.maxMana);

        canUseNow = pendingItem != null && pendingItem.CanUse(player);

        if (button != null) button.interactable = true; // always navigable

        // Dim labels if can't use
        float alpha = canUseNow ? 1f : disabledAlpha;
        DimTexts(alpha);
    }

    void DimTexts(float alpha)
    {
        if (nameText != null) { var c = nameText.color; c.a = alpha; nameText.color = c; }
        if (hpText   != null) { var c = hpText.color;   c.a = alpha; hpText.color   = c; }
        if (mpText   != null) { var c = mpText.color;   c.a = alpha; mpText.color   = c; }
    }

    void HandleClick()
    {
        OnSelected?.Invoke(target);
    }

    public bool CanUseNow => canUseNow;
}
