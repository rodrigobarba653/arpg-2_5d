using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Items panel: lists every item in the player's inventory, optionally filtered
/// by category. Each row is an ItemSlotWidget instantiated from a prefab into
/// a vertical layout container.
/// </summary>
public class ItemsPanel : MenuPanel
{
    [Header("Filter")]
    [Tooltip("If true, only items in the selected categories show. Otherwise all items.")]
    public bool useCategoryFilter = true;

    [Tooltip("Categories included when useCategoryFilter is ON.")]
    public ItemCategory[] visibleCategories =
    {
        ItemCategory.Potion,
        ItemCategory.Ether,
        ItemCategory.Revive,
        ItemCategory.Key,
        ItemCategory.Craft
    };

    [Header("List")]
    [Tooltip("Prefab of one slot row. Must have an ItemSlotWidget component on the root.")]
    public ItemSlotWidget slotPrefab;

    [Tooltip("Container (with a Vertical Layout Group) that the slot prefabs are instantiated into.")]
    public Transform slotsContainer;

    [Header("Optional")]
    [Tooltip("Shown when the filtered list is empty.")]
    public GameObject emptyMessage;

    [Tooltip("Target selector popup shown when the player chooses a consumable. " +
             "If null, the item is used directly on the first PlayerHealth.")]
    public ItemTargetSelector targetSelector;

    PlayerInventory inventory;

    readonly List<ItemSlotWidget> spawned = new List<ItemSlotWidget>();

    public override void Refresh()
    {
        EnsureInventoryHooked();
        RebuildList();
    }

    void OnEnable()
    {
        EnsureInventoryHooked();
    }

    void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= RebuildList;
            inventory = null;
        }
    }

    void EnsureInventoryHooked()
    {
        if (inventory != null) return;

        // First PlayerHealth in the scene is "the player" for now.
        var all = PlayerHealth.All;
        if (all.Count == 0) return;

        inventory = all[0].GetComponent<PlayerInventory>();
        if (inventory == null) return;

        inventory.OnChanged += RebuildList;
    }

    void RebuildList()
    {
        if (slotsContainer == null || slotPrefab == null)
        {
            Debug.LogWarning("[ItemsPanel] Slots container or prefab not assigned.", this);
            return;
        }

        // Clear existing
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        if (inventory == null)
        {
            if (emptyMessage != null) emptyMessage.SetActive(true);
            return;
        }

        int shown = 0;
        var entries = inventory.Entries;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.item == null) continue;
            if (e.quantity <= 0) continue;

            if (useCategoryFilter && !IsVisible(e.item.Category))
                continue;

            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.Bind(e.item, e.quantity);
            slot.OnUseClicked += HandleUseClicked;

            spawned.Add(slot);
            shown++;
        }

        if (emptyMessage != null)
            emptyMessage.SetActive(shown == 0);

        SetupGridNavigation();
    }

    /// <summary>
    /// Force Explicit Navigation on each spawned slot so D-Pad / arrow keys
    /// only walk through the inventory grid — never jump to the sidebar.
    /// </summary>
    void SetupGridNavigation()
    {
        if (spawned.Count == 0) return;

        int columns = ResolveColumnCount();

        for (int i = 0; i < spawned.Count; i++)
        {
            var sel = spawned[i] != null ? spawned[i].GetComponent<Selectable>() : null;
            if (sel == null) continue;

            int row = i / columns;
            int col = i % columns;

            Selectable left  = (col > 0)                                    ? SelOf(i - 1)       : null;
            Selectable right = (col < columns - 1 && i + 1 < spawned.Count) ? SelOf(i + 1)       : null;
            Selectable up    = (row > 0)                                    ? SelOf(i - columns) : null;
            Selectable down  = (i + columns < spawned.Count)                ? SelOf(i + columns) : null;

            sel.navigation = new Navigation
            {
                mode         = Navigation.Mode.Explicit,
                selectOnLeft  = left,
                selectOnRight = right,
                selectOnUp    = up,
                selectOnDown  = down,
            };
        }
    }

    int ResolveColumnCount()
    {
        if (slotsContainer == null) return 3;

        var grid = slotsContainer.GetComponent<GridLayoutGroup>();
        if (grid == null) return 3;

        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            return Mathf.Max(1, grid.constraintCount);

        return 3; // fallback
    }

    Selectable SelOf(int index)
    {
        if (index < 0 || index >= spawned.Count) return null;
        if (spawned[index] == null) return null;
        return spawned[index].GetComponent<Selectable>();
    }

    bool IsVisible(ItemCategory cat)
    {
        if (visibleCategories == null) return true;

        for (int i = 0; i < visibleCategories.Length; i++)
            if (visibleCategories[i] == cat) return true;

        return false;
    }

    void HandleUseClicked(ItemDefinition item)
    {
        if (inventory == null || item == null) return;

        // If a target selector is wired, open it and let the player pick whom.
        // The popup stays open after each use; it closes itself on cancel or
        // when the item runs out.
        if (targetSelector != null)
        {
            targetSelector.Show(
                item,
                inventory,
                onItemUsed: target => inventory.UseItem(item, target),
                onClosed: RestoreFocusToFirstSlot
            );
            return;
        }

        // Fallback: use on the first PlayerHealth available.
        var all = PlayerHealth.All;
        if (all.Count == 0) return;

        if (!inventory.UseItem(item, all[0]))
            MenuManager.PlayCancelSound();
    }

    void RestoreFocusToFirstSlot()
    {
        if (spawned.Count == 0) return;
        if (UnityEngine.EventSystems.EventSystem.current == null) return;

        var first = GetFirstSelectable();
        if (first != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public override Selectable GetFirstSelectable()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            var sel = spawned[i].GetComponent<Selectable>();
            if (sel != null && sel.interactable) return sel;
        }

        // No usable slots — return any non-null slot so focus still lands inside the panel.
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            var sel = spawned[i].GetComponent<Selectable>();
            if (sel != null) return sel;
        }

        return null;
    }
}
