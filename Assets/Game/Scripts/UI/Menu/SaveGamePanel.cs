using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// In-game save panel opened from the pause menu Save button. Lists the three
/// slots; selecting a slot saves the current state there (silently overwrites).
/// Esc / Circle / B closes.
///
/// Acts as a modal — sets MenuManager.IsModalOpen while visible so the main
/// menu doesn't process its toggle / back input.
/// </summary>
public class SaveGamePanel : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Container with VerticalLayoutGroup. SaveSlotWidget prefabs are spawned here.")]
    public Transform slotsContainer;

    [Tooltip("Prefab of one slot row (SaveSlotWidget on its root).")]
    public SaveSlotWidget slotPrefab;

    [Header("Buttons")]
    public Button backButton;

    [Header("Behavior")]
    [Tooltip("If true, the panel closes itself after a successful save.")]
    public bool closeAfterSave = false;

    readonly List<SaveSlotWidget> spawned = new List<SaveSlotWidget>();

    void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(Close);
        // NOTE: Don't call gameObject.SetActive(false) here — it conflicts with
        // the first-time activation. Set the panel inactive in the Inspector.
    }

    void OnEnable()
    {
        MenuManager.IsModalOpen = true;
        Refresh();
        // FocusFirst happens at the end of Refresh, but EventSystem sometimes
        // ignores SetSelectedGameObject the same frame the GameObject activates.
        // Re-apply it one frame later as a defensive guarantee.
        StartCoroutine(DelayedRefocus());
    }

    IEnumerator DelayedRefocus()
    {
        yield return null;
        FocusFirst();
    }

    void OnDisable()
    {
        if (MenuManager.IsModalOpen)
            MenuManager.IsModalOpen = false;
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
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();

        if (slotsContainer == null || slotPrefab == null) return;

        for (int i = 0; i < SaveSystem.MaxSlots; i++)
        {
            var data = SaveSystem.Load(i);

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
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[SaveGamePanel] No SaveManager in scene.");
            return;
        }

        SaveManager.Instance.SaveToSlot(slot);

        if (closeAfterSave)
        {
            Close();
        }
        else
        {
            // Re-render so the freshly-saved slot shows its new timestamp.
            Refresh();
        }
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

        // Restore focus to the Save button in the sidebar BEFORE deactivating
        // (after SetActive(false) the selection would be lost).
        var saveBtn = MenuManager.Instance != null ? MenuManager.Instance.saveButton : null;
        if (saveBtn != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(saveBtn.gameObject);
        }

        gameObject.SetActive(false);
    }
}
