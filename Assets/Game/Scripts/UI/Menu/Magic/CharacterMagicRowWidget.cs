using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the MagicPanel — represents ONE party member. Shows portrait +
/// name + THREE spell slot BUTTONS inline. Each slot button, when clicked,
/// invokes the parent panel's callback so the spell selector modal can open
/// for (this player, that slot index).
///
/// Prefab layout: root GameObject with portrait / name text and 3 slot
/// children. Each slot child is a Button with:
///   - Image  (spell icon, auto-populated)
///   - TMP_Text (spell name)
///   - TMP_Text (MP cost, optional)
///   - TMP_Text (display damage, optional)
///
/// NOTE: the root itself is NOT a Button anymore — the SLOTS are the
/// interactive elements.
/// </summary>
public class CharacterMagicRowWidget : MonoBehaviour
{
    [Serializable]
    public class SlotWidget
    {
        [Tooltip("The clickable button for this slot.")]
        public Button button;

        [Tooltip("Icon of the equipped spell (or blank when empty).")]
        public Image icon;

        [Tooltip("Name of the equipped spell (or the empty label).")]
        public TMP_Text nameText;

        [Tooltip("Optional: 'MP {0}' text for the equipped spell.")]
        public TMP_Text mpText;

        [Tooltip("Optional: 'DMG {0}' text for the equipped spell (hidden if 0).")]
        public TMP_Text dmgText;
    }

    [Header("Character Info")]
    public Image portrait;
    public TMP_Text nameText;

    [Header("Three Slot Buttons (in order 0, 1, 2)")]
    public SlotWidget[] slots = new SlotWidget[PlayerMagic.SlotCount];

    [Header("Display Format")]
    public string emptySlotLabel = "(empty)";
    public string mpFormat = "MP {0}";
    public string dmgFormat = "DMG {0}";

    PlayerHealth bound;
    PlayerMagic boundMagic;
    Action<PlayerHealth, int> onSlotClicked;

    void Awake()
    {
        // Wire each slot's button to call onSlotClicked with its index. This
        // is set once — even before Bind() — so the buttons are usable as soon
        // as the widget appears.
        for (int i = 0; i < slots.Length; i++)
        {
            int capturedIdx = i;
            if (slots[i] != null && slots[i].button != null)
                slots[i].button.onClick.AddListener(() => HandleSlotClicked(capturedIdx));
        }
    }

    void OnDestroy() { Unhook(); }

    void Unhook()
    {
        if (boundMagic != null)
        {
            boundMagic.OnSlotChanged -= HandleSlotChanged;
            boundMagic = null;
        }
    }

    public void Bind(PlayerHealth player, Action<PlayerHealth, int> onSlotClick)
    {
        Unhook();
        bound = player;
        onSlotClicked = onSlotClick;

        if (bound != null)
        {
            boundMagic = bound.GetComponent<PlayerMagic>();
            if (boundMagic != null)
                boundMagic.OnSlotChanged += HandleSlotChanged;
        }

        Refresh();
    }

    /// <summary>Expose slot buttons so the parent MagicPanel can set up
    /// navigation across all rows.</summary>
    public Button GetSlotButton(int slotIdx)
    {
        if (slotIdx < 0 || slotIdx >= slots.Length) return null;
        return slots[slotIdx] != null ? slots[slotIdx].button : null;
    }

    void HandleSlotClicked(int slotIdx)
    {
        if (bound == null) return;
        MenuManager.PlaySelectSound();
        onSlotClicked?.Invoke(bound, slotIdx);
    }

    void HandleSlotChanged(int _, SpellItem __) => Refresh();

    void Refresh()
    {
        if (bound != null)
        {
            if (portrait != null)
            {
                portrait.sprite = bound.character != null ? bound.character.portrait : null;
                portrait.enabled = portrait.sprite != null;

                Debug.Log($"[CharacterMagicRowWidget] '{name}' bound to '{bound.name}' " +
                          $"(character={(bound.character != null ? bound.character.name : "NULL")}), " +
                          $"portrait sprite='{(portrait.sprite != null ? portrait.sprite.name : "NULL")}', " +
                          $"on Image instanceID={portrait.GetInstanceID()}", this);
            }
            if (nameText != null)
                nameText.text = bound.character != null ? bound.character.displayName : bound.name;
        }
        else
        {
            if (portrait != null) portrait.enabled = false;
            if (nameText != null) nameText.text = "";
        }

        for (int i = 0; i < slots.Length; i++)
            BindSlotVisuals(i);
    }

    void BindSlotVisuals(int slotIdx)
    {
        var slot = slots[slotIdx];
        if (slot == null) return;

        var def = boundMagic != null ? boundMagic.GetEquipped(slotIdx) : null;

        if (slot.icon != null)
        {
            slot.icon.sprite = def != null ? def.icon : null;
            slot.icon.enabled = slot.icon.sprite != null;
        }
        if (slot.nameText != null)
            slot.nameText.text = def != null ? def.displayName : emptySlotLabel;
        if (slot.mpText != null)
            slot.mpText.text = def != null ? string.Format(mpFormat, def.manaCost) : "";
        if (slot.dmgText != null)
            slot.dmgText.text = def != null && def.displayDamage > 0
                ? string.Format(dmgFormat, def.displayDamage) : "";
    }
}
