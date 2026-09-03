using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One row representing a save slot. Shows the character / location / play
/// time / timestamp if the slot has data, or "Empty" otherwise.
///
/// The whole row is a Button — click selects it (load / save handled by the
/// owning panel). Holding the delete button (Backspace / Square / X-West) while
/// the slot is focused fills the progress bar and deletes the save on completion.
/// Reused by both Load Game and Save panels.
/// </summary>
[RequireComponent(typeof(Button))]
public class SaveSlotWidget : MonoBehaviour
{
    [Header("Visual")]
    public TMP_Text slotLabel;       // "Slot 1"
    public TMP_Text characterText;   // "Lv 3 — Ax Lume" or "Empty"
    public TMP_Text locationText;    // "Feral Island West"
    public TMP_Text timestampText;   // "2026-05-22 14:32"
    public TMP_Text playTimeText;    // "1h 23m"

    [Header("State Roots (optional)")]
    [Tooltip("Shown when the slot HAS data.")]
    public GameObject filledRoot;

    [Tooltip("Shown when the slot is EMPTY.")]
    public GameObject emptyRoot;

    [Header("Hold to Delete")]
    [Tooltip("Optional. If empty, fallback bindings are used: " +
             "Keyboard Backspace + Gamepad West button (Square / X).")]
    public InputActionReference deleteAction;

    [Tooltip("Image (Image Type = Filled, Fill Method = Horizontal/Radial) " +
             "that fills as the player holds the delete button.")]
    public Image deleteProgressFill;

    [Tooltip("Optional GameObject shown only while the player is holding to delete " +
             "(label like 'Hold to delete...').")]
    public GameObject holdHint;

    [Tooltip("Seconds the player must hold the delete button to actually delete.")]
    public float holdToDeleteDuration = 1.0f;

    int slotIndex;
    SaveData data;

    public int Slot => slotIndex;
    public bool IsEmpty => data == null;

    /// <summary>Fired when the row is clicked (load / save).</summary>
    public event Action<int> OnSelected;

    /// <summary>Fired when the hold-to-delete completes successfully.</summary>
    public event Action<int> OnDeleteRequested;

    Button button;
    float holdTimer;
    bool holding;
    static InputAction fallbackDelete;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClick);

        if (holdHint != null) holdHint.SetActive(false);
        ResetProgressVisual();
    }

    void OnEnable()
    {
        EnsureFallbackAction();
        if (deleteAction != null && deleteAction.action != null)
            deleteAction.action.Enable();
    }

    static void EnsureFallbackAction()
    {
        if (fallbackDelete != null) return;

        fallbackDelete = new InputAction("SaveSlotDelete", InputActionType.Button);
        fallbackDelete.AddBinding("<Keyboard>/backspace");
        fallbackDelete.AddBinding("<Keyboard>/delete");
        fallbackDelete.AddBinding("<Gamepad>/buttonWest"); // Square on PS, X on Xbox
        fallbackDelete.Enable();
    }

    public void Bind(int slot, SaveData data)
    {
        this.slotIndex = slot;
        this.data = data;

        if (slotLabel != null)
            slotLabel.text = $"Slot {slot + 1}";

        bool hasData = data != null;

        if (filledRoot != null) filledRoot.SetActive(hasData);
        if (emptyRoot  != null) emptyRoot.SetActive(!hasData);

        if (!hasData)
        {
            if (characterText != null) characterText.text = "Empty";
            if (locationText  != null) locationText.text  = "";
            if (timestampText != null) timestampText.text = "";
            if (playTimeText  != null) playTimeText.text  = "";
            return;
        }

        if (characterText != null)
        {
            string charLabel = ResolveCharacterLabel(data);
            characterText.text = $"Lv {data.level} — {charLabel}";
        }

        if (locationText != null)
            locationText.text = data.currentLocation;

        if (timestampText != null)
            timestampText.text = FormatTimestamp(data.timestamp);

        if (playTimeText != null)
            playTimeText.text = FormatPlayTime(data.playTimeSeconds);
    }

    void Update()
    {
        // Empty slots can't be deleted.
        if (IsEmpty) { ResetHold(); return; }

        // Only respond while this slot is focused.
        if (!IsFocused()) { ResetHold(); return; }

        bool pressed = IsDeleteHeld();

        if (pressed)
        {
            if (!holding)
            {
                holding = true;
                holdTimer = 0f;
                if (holdHint != null) holdHint.SetActive(true);
            }

            holdTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, holdToDeleteDuration));

            if (deleteProgressFill != null)
                deleteProgressFill.fillAmount = t;

            if (holdTimer >= holdToDeleteDuration)
                CompleteDelete();
        }
        else if (holding)
        {
            ResetHold();
        }
    }

    bool IsFocused()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.currentSelectedGameObject == gameObject;
    }

    bool IsDeleteHeld()
    {
        if (deleteAction != null && deleteAction.action != null)
            return deleteAction.action.IsPressed();

        return fallbackDelete != null && fallbackDelete.IsPressed();
    }

    void CompleteDelete()
    {
        var cb = OnDeleteRequested;
        ResetHold();
        cb?.Invoke(slotIndex);
    }

    void ResetHold()
    {
        holding = false;
        holdTimer = 0f;
        ResetProgressVisual();
        if (holdHint != null) holdHint.SetActive(false);
    }

    void ResetProgressVisual()
    {
        if (deleteProgressFill != null) deleteProgressFill.fillAmount = 0f;
    }

    void HandleClick()
    {
        // If the player just completed a delete, the slot is about to be
        // refreshed away — skip the click to avoid load/save firing.
        if (holding && holdTimer >= holdToDeleteDuration) return;

        OnSelected?.Invoke(slotIndex);
    }

    static string ResolveCharacterLabel(SaveData d)
    {
        if (d == null || string.IsNullOrEmpty(d.characterId)) return "?";

        var players = PlayerHealth.All;
        for (int i = 0; i < players.Count; i++)
        {
            var def = players[i] != null ? players[i].character : null;
            if (def != null && def.id == d.characterId)
                return def.displayName;
        }
        return d.characterId;
    }

    static string FormatTimestamp(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        if (DateTime.TryParse(iso, out var dt))
            return dt.ToString("yyyy-MM-dd HH:mm");
        return iso;
    }

    static string FormatPlayTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        int h = total / 3600;
        int m = (total / 60) % 60;
        if (h > 0) return $"{h}h {m:00}m";
        return $"{m}m";
    }
}
