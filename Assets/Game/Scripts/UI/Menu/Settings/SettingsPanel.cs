using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Top-level "Settings" panel (one of MenuManager's main sidebar panels).
/// Contains its OWN mini-sidebar of sub-tabs — Audio / Gameplay / Display —
/// each showing a different MenuPanel. Mirrors MenuManager's tab-switching
/// logic, scoped locally.
///
/// Back navigation has TWO levels: while a sub-tab is open, Esc/Circle closes
/// JUST the sub-tab (back to the blank tab list). From the blank tab list, a
/// second Esc/Circle bubbles up to MenuManager's own back logic and leaves
/// Settings entirely. This mirrors the same "IsModalOpen" pattern used by
/// WeaponSlotsPanel/MagicSpellSelectorPanel — set true while a sub-tab is
/// open so MenuManager's Update() skips its own back handling for that press,
/// then this panel handles it (deferred one frame, same reason those modals
/// defer: so the same button press isn't also read by MenuManager this frame).
/// </summary>
public class SettingsPanel : MenuPanel
{
    [Header("Sub-Tabs & Panels (matched 1:1, same order)")]
    public Button[] subTabButtons;
    public MenuPanel[] subPanels;

    [Header("Active Sub-Tab Indicator (optional)")]
    [Tooltip("One GameObject per sub-tab button, e.g. an underline/highlight. " +
             "The one matching the current sub-tab is shown, the rest hidden.")]
    public GameObject[] subTabHighlights;

    [Header("Behaviour")]
    [Tooltip("If true, auto-opens 'Default Sub Tab Index' when this panel " +
             "opens. If false (default), Settings opens with NO sub-panel " +
             "shown — just the tab buttons — until the player picks one.")]
    public bool autoOpenDefaultSubTab = false;

    [Tooltip("Sub-tab index auto-opened on Open(), only used if " +
             "'Auto Open Default Sub Tab' is enabled.")]
    public int defaultSubTabIndex = 0;

    public int CurrentSubIndex { get; private set; } = -1;

    bool wired;
    bool closingSubTab; // set during the one-frame defer so Update() ignores input

    void Awake()
    {
        WireSubTabButtons();
    }

    void WireSubTabButtons()
    {
        if (wired || subTabButtons == null) return;
        wired = true;

        for (int i = 0; i < subTabButtons.Length; i++)
        {
            int idx = i; // capture for closure
            if (subTabButtons[i] != null)
                subTabButtons[i].onClick.AddListener(() => ShowSubTab(idx));
        }
    }

    public override void Open()
    {
        base.Open();

        if (autoOpenDefaultSubTab)
        {
            ShowSubTab(Mathf.Clamp(defaultSubTabIndex, 0, (subPanels?.Length ?? 1) - 1));
            return;
        }

        // No sub-tab auto-selected — make sure every sub-panel starts closed
        // (they may have been left active in the Editor) so Settings opens
        // showing just the tab buttons, blank until the player picks one.
        if (subPanels != null)
            for (int i = 0; i < subPanels.Length; i++)
                if (subPanels[i] != null) subPanels[i].Close();

        CurrentSubIndex = -1;
        ApplySubTabHighlight();
    }

    public override void Close()
    {
        if (subPanels != null)
            for (int i = 0; i < subPanels.Length; i++)
                if (subPanels[i] != null) subPanels[i].Close();

        CurrentSubIndex = -1;

        // Safety: if Settings itself gets closed (e.g. the player clicked a
        // different top-level sidebar tab) while a sub-tab was open, make sure
        // we don't leave IsModalOpen stuck true — that would silently block
        // Back/Esc menu-wide from then on.
        MenuManager.IsModalOpen = false;

        base.Close();
    }

    /// <summary>Nothing to refresh at this level — each sub-panel refreshes
    /// itself via its own Open() when shown.</summary>
    public override void Refresh() { }

    public void ShowSubTab(int index)
    {
        if (subPanels == null || index < 0 || index >= subPanels.Length) return;

        for (int i = 0; i < subPanels.Length; i++)
            if (subPanels[i] != null && i != index) subPanels[i].Close();

        if (subPanels[index] != null) subPanels[index].Open();

        CurrentSubIndex = index;
        ApplySubTabHighlight();

        // A sub-tab is now open — claim IsModalOpen so MenuManager's own
        // Back/Esc handling steps aside and lets THIS panel's Update() (below)
        // handle the next Back press instead of jumping straight out of Settings.
        MenuManager.IsModalOpen = true;

        MenuManager.PlaySelectSound();
        FocusFirstSelectableOfSubPanel(index);
    }

    void Update()
    {
        // Only intercept Back while a sub-tab is actually open. With no
        // sub-tab open (CurrentSubIndex == -1, IsModalOpen == false), Back
        // falls through to MenuManager's own Update() as normal — that's the
        // SECOND level of back navigation, leaving Settings entirely.
        if (CurrentSubIndex < 0) return;
        if (closingSubTab) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseSubTab();
            return;
        }
        if (Gamepad.current != null && Gamepad.current.bButton.wasPressedThisFrame)
        {
            CloseSubTab();
        }
    }

    /// <summary>Closes just the currently open sub-tab, returning to the
    /// blank Settings tab list with focus back on the sub-tab button the
    /// player came from.</summary>
    public void CloseSubTab()
    {
        if (closingSubTab) return;
        closingSubTab = true;

        MenuManager.PlayCancelSound();

        // Defer by one frame — same reason WeaponSlotsPanel/PartSelectorPanel
        // do: so MenuManager's own Update() doesn't ALSO react to this same
        // press this frame (it already skips it because IsModalOpen is still
        // true throughout the current frame; we only clear it next frame).
        StartCoroutine(CloseSubTabNextFrame());
    }

    System.Collections.IEnumerator CloseSubTabNextFrame()
    {
        yield return null;

        int sourceIndex = CurrentSubIndex;

        if (subPanels != null && sourceIndex >= 0 && sourceIndex < subPanels.Length
            && subPanels[sourceIndex] != null)
            subPanels[sourceIndex].Close();

        CurrentSubIndex = -1;
        ApplySubTabHighlight();
        MenuManager.IsModalOpen = false;

        FocusSubTabButton(sourceIndex);

        closingSubTab = false;
    }

    void FocusSubTabButton(int index)
    {
        if (EventSystem.current == null) return;
        if (subTabButtons == null || index < 0 || index >= subTabButtons.Length) return;

        var btn = subTabButtons[index];
        if (btn == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(btn.gameObject);
    }

    void FocusFirstSelectableOfSubPanel(int index)
    {
        if (EventSystem.current == null) return;
        if (subPanels == null || index < 0 || index >= subPanels.Length) return;

        var panel = subPanels[index];
        if (panel == null) return;

        var first = panel.GetFirstSelectable();
        if (first == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    void ApplySubTabHighlight()
    {
        if (subTabHighlights == null) return;
        for (int i = 0; i < subTabHighlights.Length; i++)
            if (subTabHighlights[i] != null)
                subTabHighlights[i].SetActive(i == CurrentSubIndex);
    }

    /// <summary>MenuManager focuses this after Open() — lands the player on the
    /// first sub-tab button so they can navigate tabs before diving into a
    /// specific sub-panel's controls.</summary>
    public override Selectable GetFirstSelectable()
    {
        if (subTabButtons == null) return null;
        for (int i = 0; i < subTabButtons.Length; i++)
            if (subTabButtons[i] != null && subTabButtons[i].gameObject.activeInHierarchy)
                return subTabButtons[i];
        return null;
    }
}
