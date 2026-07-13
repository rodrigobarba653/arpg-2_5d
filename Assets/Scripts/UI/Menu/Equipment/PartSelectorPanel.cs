using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modal listing every WeaponPart in the player's inventory that matches a
/// requested kind (Handle / Guard / Blade). Selecting one equips it on the
/// target PlayerEquipment and closes the modal.
///
/// Build: panel root with CanvasGroup, a container (VerticalLayoutGroup) for
/// rows, and a PartListItemWidget prefab spawned once per matching item.
/// </summary>
public class PartSelectorPanel : MonoBehaviour
{
    [Header("Spawn")]
    public Transform listContainer;
    public PartListItemWidget rowPrefab;

    [Header("Optional")]
    public TMPro.TMP_Text headerText;
    public string headerFormat = "Choose {0}";  // {0} = kind

    PlayerEquipment targetEquipment;
    WeaponPartKind targetKind;
    Selectable returnFocus;

    readonly List<PartListItemWidget> spawned = new List<PartListItemWidget>();

    void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Open(PlayerEquipment equipment, WeaponPartKind kind, Selectable focusOnClose)
    {
        targetEquipment = equipment;
        targetKind = kind;
        returnFocus = focusOnClose;

        if (headerText != null) headerText.text = string.Format(headerFormat, kind);

        gameObject.SetActive(true);
        MenuManager.IsModalOpen = true;

        Rebuild();
        StartCoroutine(FocusFirstNextFrame());
    }

    bool closing;  // set during the one-frame delay so Update ignores input

    public void Close()
    {
        if (closing) return;
        closing = true;

        MenuManager.PlayCancelSound();

        // Defer the hide + IsModalOpen reset + focus restoration by one frame so:
        //  (a) the parent WeaponSlotsPanel.Update doesn't see us inactive in the
        //      same frame and treat the SAME Circle press as its own close.
        //  (b) MenuManager.Update doesn't process the same press either.
        //  (c) Focus is restored AFTER our rows are deactivated, so Unity
        //      doesn't reset it back to null.
        StartCoroutine(HideNextFrame());
    }

    System.Collections.IEnumerator HideNextFrame()
    {
        yield return null;

        gameObject.SetActive(false);
        MenuManager.IsModalOpen = false;

        // Restore focus AFTER hide so the EventSystem doesn't clear it when
        // our active selectable (a part row) becomes inactive.
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
        // Clear previous rows.
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        if (listContainer == null || rowPrefab == null || targetEquipment == null) return;

        // Pull parts of the requested kind from the player's inventory.
        var inv = targetEquipment.GetComponent<PlayerInventory>();
        if (inv == null) return;

        foreach (var entry in inv.Entries)
        {
            if (entry == null || entry.item == null) continue;
            if (!(entry.item is WeaponPart part)) continue;
            if (part.partKind != targetKind) continue;

            var row = Instantiate(rowPrefab, listContainer);
            row.Bind(part, entry.quantity, OnPartSelected);
            spawned.Add(row);
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

    void OnPartSelected(WeaponPart part)
    {
        if (part == null || targetEquipment == null) return;

        targetEquipment.EquipPart(part);

        // Equipping is the action — return to the WeaponSlotsPanel.
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
