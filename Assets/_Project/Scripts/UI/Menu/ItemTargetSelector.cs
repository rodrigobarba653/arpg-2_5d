using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Popup shown when the player chooses a consumable in the Items panel.
/// Spawns one ItemTargetEntryWidget per PlayerHealth in the scene.
///
/// Behavior:
///   - Selecting a target USES the item but the popup STAYS OPEN.
///   - On every use, entries refresh (HP/MP shown updated) so you can use again.
///   - When the item count hits 0, the popup auto-closes.
///   - Esc / Circle / B closes the popup and returns focus to the items grid.
/// </summary>
public class ItemTargetSelector : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Root GameObject of the popup. Toggled active/inactive on Show/Hide.")]
    public GameObject root;

    [Tooltip("Container where entry rows are instantiated (vertical layout recommended).")]
    public Transform entriesContainer;

    [Tooltip("Prefab of one entry row (icon + HP + name).")]
    public ItemTargetEntryWidget entryPrefab;

    [Tooltip("Optional cancel button (gamepad B / Esc also work).")]
    public Button cancelButton;

    ItemDefinition pendingItem;
    PlayerInventory inventory;
    Action<PlayerHealth> onItemUsed;
    Action onClosed;

    readonly List<ItemTargetEntryWidget> spawned = new List<ItemTargetEntryWidget>();

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        if (root != null) root.SetActive(false);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancelPressed);
    }

    void OnDisable()
    {
        // If we get disabled (parent panel closing, scene change, etc) without
        // a clean Hide() call, reset the modal flag so the main menu navigation
        // doesn't stay blocked.
        if (MenuManager.IsModalOpen)
            MenuManager.IsModalOpen = false;

        if (inventory != null)
        {
            inventory.OnChanged -= HandleInventoryChanged;
            inventory = null;
        }

        pendingItem = null;
    }

    void Update()
    {
        if (!IsOpen) return;

        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleCancelPressed();
            return;
        }

        if (UnityEngine.InputSystem.Gamepad.current != null &&
            UnityEngine.InputSystem.Gamepad.current.bButton.wasPressedThisFrame)
        {
            HandleCancelPressed();
        }
    }

    /// <summary>
    /// Open the popup for the given item. The selector subscribes to the
    /// inventory so it can refresh after each use and auto-close when out.
    /// </summary>
    public void Show(ItemDefinition item, PlayerInventory inventory,
                     Action<PlayerHealth> onItemUsed, Action onClosed)
    {
        if (root == null || entryPrefab == null || entriesContainer == null)
        {
            Debug.LogWarning("[ItemTargetSelector] Missing references.", this);
            return;
        }

        this.pendingItem = item;
        this.inventory   = inventory;
        this.onItemUsed  = onItemUsed;
        this.onClosed    = onClosed;

        SpawnEntries();

        if (inventory != null)
            inventory.OnChanged += HandleInventoryChanged;

        root.SetActive(true);
        MenuManager.IsModalOpen = true;

        FocusFirstEntry();
    }

    void Hide()
    {
        if (inventory != null)
            inventory.OnChanged -= HandleInventoryChanged;

        inventory   = null;
        pendingItem = null;

        if (root != null) root.SetActive(false);
        MenuManager.IsModalOpen = false;
    }

    void Close()
    {
        var cb = onClosed;
        onClosed = null;

        Hide();

        cb?.Invoke();
    }

    void SpawnEntries()
    {
        // Clear previous
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        var players = PlayerHealth.All;
        for (int i = 0; i < players.Count; i++)
        {
            var entry = Instantiate(entryPrefab, entriesContainer);
            entry.Bind(players[i], pendingItem);
            entry.OnSelected += HandleEntrySelected;
            spawned.Add(entry);
        }

        SetupNavigation();
    }

    void RebindEntries()
    {
        // Re-bind in place (no respawn) so focused entry stays focused.
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null)
                spawned[i].Bind(spawned[i].Target, pendingItem);
    }

    void FocusFirstEntry()
    {
        if (spawned.Count == 0 || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(spawned[0].gameObject);
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
                mode = Navigation.Mode.Explicit,
                selectOnUp    = up,
                selectOnDown  = down,
                selectOnLeft  = null,
                selectOnRight = null,
            };
        }
    }

    void HandleEntrySelected(PlayerHealth target)
    {
        if (pendingItem == null) return;

        if (pendingItem.CanUse(target))
        {
            onItemUsed?.Invoke(target);
            // Inventory's OnChanged will fire → HandleInventoryChanged refreshes or auto-closes.
        }
        else
        {
            MenuManager.PlayCancelSound();
        }
    }

    void HandleCancelPressed()
    {
        MenuManager.PlayCancelSound();
        Close();
    }

    void HandleInventoryChanged()
    {
        if (pendingItem == null || inventory == null) return;

        if (inventory.GetCount(pendingItem) <= 0)
        {
            // Ran out of this item — close the popup and let the items panel rebuild.
            Close();
            return;
        }

        // Still have some — refresh the entries to show updated HP/MP.
        RebindEntries();
    }
}
