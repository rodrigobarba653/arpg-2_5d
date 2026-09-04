using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sidebar panel for the Magic system. Lists every PlayerHealth in the party
/// as a row of 3 spell slots (via CharacterMagicRowWidget). Pressing a slot
/// opens the MagicSpellSelectorPanel modal for (that player, that slot).
///
/// Navigation: Up/Down walks through slots. Slots in the same character row
/// are horizontal siblings (Left/Right). Vertical (Up/Down) crosses between
/// characters at the same column.
/// </summary>
public class MagicPanel : MenuPanel
{
    [Tooltip("Pre-placed widget GameObjects in the panel (one per party slot). " +
             "Widgets beyond the number of registered PlayerHealths are hidden.")]
    public CharacterMagicRowWidget[] widgets;

    [Tooltip("Modal opened when a slot button is clicked.")]
    public MagicSpellSelectorPanel spellSelectorPanel;

    PlayerInventory subscribedInv;

    void OnEnable()
    {
        Party.OnPartyChanged += Refresh;
        Party.OnActiveChanged += HandleActiveChanged;

        // Refresh whenever the shared inventory changes — spells added or
        // removed should re-render the slot previews.
        var players = PlayerHealth.All;
        if (players.Count > 0)
        {
            subscribedInv = players[0].GetComponent<PlayerInventory>();
            if (subscribedInv != null) subscribedInv.OnChanged += Refresh;
        }
    }

    void OnDisable()
    {
        Party.OnPartyChanged -= Refresh;
        Party.OnActiveChanged -= HandleActiveChanged;
        if (subscribedInv != null)
        {
            subscribedInv.OnChanged -= Refresh;
            subscribedInv = null;
        }
    }

    void HandleActiveChanged(PlayerHealth _) => Refresh();

    public override void Refresh()
    {
        if (widgets == null || widgets.Length == 0) return;

        var players = PlayerHealth.All;

        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null) continue;

            if (i < players.Count)
            {
                widgets[i].gameObject.SetActive(true);
                widgets[i].Bind(players[i], OnSlotClicked);
            }
            else
            {
                widgets[i].Bind(null, null);
                widgets[i].gameObject.SetActive(false);
            }
        }

        SetupNavigation();
    }

    /// <summary>
    /// Build an explicit navigation grid: Left/Right walks slots within the
    /// same character row; Up/Down jumps to the same slot column on the
    /// row above/below. Sidebar re-entry is via Esc/Circle only.
    /// </summary>
    void SetupNavigation()
    {
        // Collect the active rows.
        var activeRows = new System.Collections.Generic.List<CharacterMagicRowWidget>();
        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null) continue;
            if (!widgets[i].gameObject.activeSelf) continue;
            activeRows.Add(widgets[i]);
        }

        Debug.Log($"[MagicPanel] SetupNavigation: {activeRows.Count} active row(s). " +
                  $"widgets[] length = {widgets.Length}.");

        // Diagnostic: verify each row has its slots wired.
        for (int r = 0; r < activeRows.Count; r++)
        {
            int wiredSlots = 0;
            for (int s = 0; s < PlayerMagic.SlotCount; s++)
                if (activeRows[r].GetSlotButton(s) != null) wiredSlots++;
            if (wiredSlots < PlayerMagic.SlotCount)
                Debug.LogWarning($"[MagicPanel] Row {r} ('{activeRows[r].name}') has only " +
                                 $"{wiredSlots}/{PlayerMagic.SlotCount} slot buttons wired. " +
                                 "Fix the slots[] array in the CharacterMagicRowWidget component.",
                                 activeRows[r]);
        }

        for (int r = 0; r < activeRows.Count; r++)
        {
            var row = activeRows[r];
            for (int s = 0; s < PlayerMagic.SlotCount; s++)
            {
                var btn = row.GetSlotButton(s);
                if (btn == null) continue;

                Button left  = s > 0                       ? row.GetSlotButton(s - 1) : null;
                Button right = s < PlayerMagic.SlotCount-1 ? row.GetSlotButton(s + 1) : null;
                Button up    = r > 0                       ? activeRows[r - 1].GetSlotButton(s) : null;
                Button down  = r < activeRows.Count - 1    ? activeRows[r + 1].GetSlotButton(s) : null;

                btn.navigation = new Navigation
                {
                    mode          = Navigation.Mode.Explicit,
                    selectOnLeft  = left,
                    selectOnRight = right,
                    selectOnUp    = up,
                    selectOnDown  = down,
                };
            }
        }
    }

    public override Selectable GetFirstSelectable()
    {
        if (widgets == null) return null;
        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null || !widgets[i].gameObject.activeInHierarchy) continue;
            var btn = widgets[i].GetSlotButton(0);
            if (btn != null) return btn;
        }
        return null;
    }

    void OnSlotClicked(PlayerHealth player, int slotIdx)
    {
        if (spellSelectorPanel == null || player == null) return;

        var magic = player.GetComponent<PlayerMagic>();
        if (magic == null) return;

        Selectable returnFocus = null;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            returnFocus = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();

        spellSelectorPanel.Open(magic, slotIdx, returnFocus);
    }
}
