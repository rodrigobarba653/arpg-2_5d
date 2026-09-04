using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modal listing every SpellItem in the shared party inventory that the target
/// character is COMPATIBLE with. Selecting one equips it in the requested slot
/// on the target PlayerMagic and closes the modal.
///
/// Also includes an "Unequip" option at the top so the player can clear a
/// slot without having to pick a new spell.
/// </summary>
public class MagicSpellSelectorPanel : MonoBehaviour
{
    [Header("Spawn")]
    public Transform listContainer;
    public MagicSpellListItemWidget rowPrefab;

    [Header("Optional")]
    public TMPro.TMP_Text headerText;
    public string headerFormat = "Slot {0}";

    PlayerMagic targetMagic;
    int targetSlot;
    Selectable returnFocus;

    readonly List<MagicSpellListItemWidget> spawned = new List<MagicSpellListItemWidget>();

    void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Open(PlayerMagic magic, int slotIdx, Selectable focusOnClose)
    {
        targetMagic = magic;
        targetSlot = slotIdx;
        returnFocus = focusOnClose;

        if (headerText != null) headerText.text = string.Format(headerFormat, slotIdx + 1);

        gameObject.SetActive(true);
        MenuManager.IsModalOpen = true;

        Rebuild();
        StartCoroutine(FocusFirstNextFrame());
    }

    bool closing;

    public void Close()
    {
        if (closing) return;
        closing = true;

        MenuManager.PlayCancelSound();
        StartCoroutine(HideNextFrame());
    }

    IEnumerator HideNextFrame()
    {
        yield return null;

        gameObject.SetActive(false);
        MenuManager.IsModalOpen = false;

        if (returnFocus != null && returnFocus.gameObject.activeInHierarchy
            && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(returnFocus.gameObject);
        }

        closing = false;
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (closing) return;

        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        if (UnityEngine.InputSystem.Gamepad.current != null &&
            UnityEngine.InputSystem.Gamepad.current.bButton.wasPressedThisFrame)
        {
            Close();
        }
    }

    void Rebuild()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        if (listContainer == null || rowPrefab == null || targetMagic == null) return;

        // Row 0: "Unequip" option, but only if the slot currently has something.
        if (targetMagic.GetEquipped(targetSlot) != null)
        {
            var unequipRow = Instantiate(rowPrefab, listContainer);
            unequipRow.BindUnequip(OnUnequipSelected);
            spawned.Add(unequipRow);
        }

        // Then every SpellItem in the SHARED party inventory that IS compatible
        // with the target character.
        var inv = targetMagic.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            var entries = inv.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.item == null) continue;
                var spell = entry.item as SpellItem;
                if (spell == null) continue;
                if (!targetMagic.IsCompatible(spell)) continue;

                var row = Instantiate(rowPrefab, listContainer);
                bool isEquippedInAnotherSlot = false;
                for (int s = 0; s < PlayerMagic.SlotCount; s++)
                {
                    if (s == targetSlot) continue;
                    if (targetMagic.GetEquipped(s) == spell) { isEquippedInAnotherSlot = true; break; }
                }
                row.Bind(spell, isEquippedInAnotherSlot, OnSpellSelected);
                spawned.Add(row);
            }
        }

        SetupNavigation();
    }

    void SetupNavigation()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            var sel = spawned[i].GetComponent<Selectable>();
            if (sel == null) continue;

            Selectable up   = i > 0                 ? spawned[i - 1].GetComponent<Selectable>() : null;
            Selectable down = i < spawned.Count - 1 ? spawned[i + 1].GetComponent<Selectable>() : null;

            sel.navigation = new Navigation
            {
                mode          = Navigation.Mode.Explicit,
                selectOnUp    = up,
                selectOnDown  = down,
                selectOnLeft  = null,
                selectOnRight = null,
            };
        }
    }

    void OnSpellSelected(SpellItem spell)
    {
        if (spell == null || targetMagic == null) return;
        targetMagic.Equip(spell, targetSlot);
        Close();
    }

    void OnUnequipSelected(SpellItem _)
    {
        if (targetMagic == null) return;
        targetMagic.Equip(null, targetSlot);
        Close();
    }

    IEnumerator FocusFirstNextFrame()
    {
        yield return null;
        if (EventSystem.current == null) yield break;

        if (spawned.Count > 0)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(spawned[0].gameObject);
        }
    }
}
