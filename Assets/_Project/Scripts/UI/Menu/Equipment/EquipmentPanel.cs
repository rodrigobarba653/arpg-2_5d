using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sidebar panel for the Equipment system. Lists every PlayerHealth in the
/// party as a clickable row. Selecting a character opens the WeaponSlotsPanel
/// (modal) for that character.
///
/// Mirrors StatusPanel's structure — pre-place one CharacterEquipmentRowWidget
/// per party slot.
/// </summary>
public class EquipmentPanel : MenuPanel
{
    [Tooltip("Pre-placed widget GameObjects in the panel (one per party slot). " +
             "Widgets beyond the number of registered PlayerHealths are hidden.")]
    public CharacterEquipmentRowWidget[] widgets;

    [Tooltip("Modal opened when a character row is clicked.")]
    public WeaponSlotsPanel weaponSlotsPanel;

    void OnEnable()
    {
        Party.OnPartyChanged += Refresh;
        Party.OnActiveChanged += HandleActiveChanged;
    }

    void OnDisable()
    {
        Party.OnPartyChanged -= Refresh;
        Party.OnActiveChanged -= HandleActiveChanged;
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
                widgets[i].Bind(players[i], OnCharacterClicked);
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
    /// Explicit Up/Down chain through the visible widgets. Left/Right are dead
    /// so the player can't navigate back to the sidebar with the right stick /
    /// arrow keys — they have to press Esc/Circle for that.
    /// </summary>
    void SetupNavigation()
    {
        var active = new System.Collections.Generic.List<Selectable>();
        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null) continue;
            if (!widgets[i].gameObject.activeSelf) continue;
            var sel = widgets[i].GetComponent<Selectable>();
            if (sel != null) active.Add(sel);
        }

        for (int i = 0; i < active.Count; i++)
        {
            active[i].navigation = new Navigation
            {
                mode          = Navigation.Mode.Explicit,
                selectOnUp    = i > 0                 ? active[i - 1] : null,
                selectOnDown  = i < active.Count - 1  ? active[i + 1] : null,
                selectOnLeft  = null,
                selectOnRight = null,
            };
        }
    }

    public override Selectable GetFirstSelectable()
    {
        if (widgets == null) return null;
        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null || !widgets[i].gameObject.activeInHierarchy) continue;
            var sel = widgets[i].GetComponent<Selectable>();
            if (sel != null) return sel;
        }
        return null;
    }

    void OnCharacterClicked(PlayerHealth player)
    {
        if (weaponSlotsPanel == null || player == null) return;

        // Remember the button we came from so the modal restores focus on close.
        Selectable returnFocus = null;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            returnFocus = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();

        weaponSlotsPanel.Open(player, returnFocus);
    }
}
