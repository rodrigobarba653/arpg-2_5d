using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One cell in the Items grid. Pure text: "Potion x3". The whole slot is the
/// Button — clicking it tries to use the item.
///
/// Every slot stays interactable so keyboard/gamepad navigation can land on
/// every item (otherwise Unity skips non-interactable buttons and you can
/// only select one slot). Slots that can't be used right now are visually
/// dimmed via the label color and silently ignore clicks.
/// </summary>
[RequireComponent(typeof(Button))]
public class ItemSlotWidget : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Text that shows the item: 'Potion x3'.")]
    public TMP_Text label;

    [Header("Format")]
    [Tooltip("Format string. {0} = item name, {1} = count.")]
    public string format = "{0} x{1}";

    [Header("Disabled Look")]
    [Tooltip("Label alpha when the item can't be used right now (not usable / full HP / etc).")]
    [Range(0f, 1f)]
    public float disabledLabelAlpha = 0.35f;

    Button button;
    ItemDefinition item;
    int count;
    bool canUseNow;

    /// <summary>Fired when the slot is clicked AND the item can actually be used.</summary>
    public event Action<ItemDefinition> OnUseClicked;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(HandleClicked);
    }

    public void Bind(ItemDefinition item, int count)
    {
        this.item = item;
        this.count = count;

        Refresh();
    }

    void Refresh()
    {
        if (item == null)
        {
            if (label != null) label.text = "";
            if (button != null) button.interactable = false;
            canUseNow = false;
            return;
        }

        // The button stays interactable ALWAYS so it can be navigated to with
        // D-Pad / arrow keys. Whether it can be USED is a separate concept
        // tracked in canUseNow and shown via dimming.
        if (button != null)
            button.interactable = true;

        if (label != null)
        {
            string name = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
            label.text = string.Format(format, name, count);
        }

        var target = ResolveTarget();
        canUseNow = item.IsUsable && item.CanUse(target);

        if (label != null)
        {
            Color c = label.color;
            c.a = canUseNow ? 1f : disabledLabelAlpha;
            label.color = c;
        }
    }

    void HandleClicked()
    {
        if (item == null) return;

        // If the item isn't usable right now (Key, Craft, full HP, etc), play
        // the cancel sound and ignore the click.
        if (!canUseNow)
        {
            MenuManager.PlayCancelSound();
            return;
        }

        MenuManager.PlaySelectSound();
        OnUseClicked?.Invoke(item);
    }

    static PlayerHealth ResolveTarget()
    {
        var all = PlayerHealth.All;
        return all.Count > 0 ? all[0] : null;
    }
}
