using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Load Game panel shown from the title screen. Lists every save slot via
/// SaveSlotWidget; clicking a slot with data calls SaveManager.LoadFromSlot.
///
/// Esc / Circle / B closes the panel.
/// </summary>
public class LoadGamePanel : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Container with VerticalLayoutGroup. SaveSlotWidget prefabs are spawned here.")]
    public Transform slotsContainer;

    [Tooltip("Prefab of one slot row (SaveSlotWidget on its root).")]
    public SaveSlotWidget slotPrefab;

    [Header("Buttons")]
    public Button backButton;

    [Tooltip("Optional: button to refocus when this panel closes. Drag the 'Load Game' " +
             "button of the main menu here so focus returns to it after cancel.")]
    public Button returnFocusTo;

    [Header("Empty Slots")]
    [Tooltip("If true, empty slots are still listed (gray). If false they're skipped.")]
    public bool showEmptySlots = true;

    readonly List<SaveSlotWidget> spawned = new List<SaveSlotWidget>();

    void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(Close);
        // NOTE: Don't call gameObject.SetActive(false) here — it conflicts with
        // the first-time activation. Set the panel inactive in the Inspector.
    }

    void OnEnable()
    {
        Refresh();
        StartCoroutine(DelayedRefocus());
    }

    IEnumerator DelayedRefocus()
    {
        yield return null;
        FocusFirst();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

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

    public void Refresh()
    {
        // Clear existing
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        if (slotsContainer == null || slotPrefab == null) return;

        for (int i = 0; i < SaveSystem.MaxSlots; i++)
        {
            var data = SaveSystem.Load(i);

            if (data == null && !showEmptySlots) continue;

            var row = Instantiate(slotPrefab, slotsContainer);
            row.Bind(i, data);
            row.OnSelected += HandleSlotSelected;
            row.OnDeleteRequested += HandleDeleteRequested;
            spawned.Add(row);
        }

        SetupNavigation();
        FocusFirst();
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

    void FocusFirst()
    {
        if (spawned.Count == 0 || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(spawned[0].gameObject);
    }

    void HandleSlotSelected(int slot)
    {
        // Empty slot → just play cancel sound and stay open.
        if (!SaveSystem.HasSlot(slot))
        {
            MenuManager.PlayCancelSound();
            return;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[LoadGamePanel] No SaveManager in scene.");
            return;
        }

        SaveManager.Instance.LoadFromSlot(slot);
    }

    void HandleDeleteRequested(int slot)
    {
        SaveSystem.Delete(slot);
        MenuManager.PlayCancelSound();
        Refresh();
    }

    public void Close()
    {
        MenuManager.PlayCancelSound();

        // Restore focus to the button that opened this panel BEFORE deactivating.
        if (returnFocusTo != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(returnFocusTo.gameObject);
        }

        gameObject.SetActive(false);
    }
}
