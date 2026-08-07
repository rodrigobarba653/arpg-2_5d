using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modal panel that shows ONE character's weapon and its 3 part slots
/// (Handle / Guard / Blade). Each slot button opens the PartSelectorPanel
/// filtered by that slot's kind.
///
/// Build: panel root with CanvasGroup, header showing weapon icon + name,
/// then three SlotRow children — each a Button with child Image + TMP_Text to
/// display the currently equipped part.
/// </summary>
public class WeaponSlotsPanel : MonoBehaviour
{
    [Header("Header (weapon)")]
    public Image weaponIcon;
    public TMP_Text weaponNameText;
    public TMP_Text characterNameText;

    [Header("Slot 1 — kind defined by the weapon (Handle / Knuckle / etc.)")]
    public Button slot1Button;
    public Image slot1Icon;
    public TMP_Text slot1NameText;
    [Tooltip("Optional: shows the slot's kind name (HANDLE / KNUCKLE / etc).")]
    public TMP_Text slot1KindLabel;

    [Header("Slot 2 — kind defined by the weapon (Guard / Palm / etc.)")]
    public Button slot2Button;
    public Image slot2Icon;
    public TMP_Text slot2NameText;
    public TMP_Text slot2KindLabel;

    [Header("Slot 3 — kind defined by the weapon (Blade / Wrist / etc.)")]
    public Button slot3Button;
    public Image slot3Icon;
    public TMP_Text slot3NameText;
    public TMP_Text slot3KindLabel;

    [Header("Stats Preview (optional)")]
    public TMP_Text damageText;
    public TMP_Text defenseText;
    public TMP_Text attackSpeedText;

    [Header("Stat Animation")]
    [Tooltip("Seconds to lerp from old stat value to new when a part changes.")]
    public float statAnimDuration = 0.4f;

    [Tooltip("Optional color flash applied to the stat text while animating.")]
    public Color statFlashColor = new Color(1f, 0.85f, 0.2f);

    // Currently-displayed (animated) values, separate from the source so we can
    // tween smoothly even when the user spam-equips parts.
    float displayedDamage;
    float displayedDefense;
    float displayedAttackSpeed;

    Coroutine damageTween;
    Coroutine defenseTween;
    Coroutine attackSpeedTween;

    [Header("Refs")]
    public PartSelectorPanel partSelectorPanel;

    PlayerHealth currentPlayer;
    PlayerEquipment currentEq;
    Selectable returnFocus;

    void Awake()
    {
        if (slot1Button != null) slot1Button.onClick.AddListener(() => OpenSelectorForSlot(0, slot1Button));
        if (slot2Button != null) slot2Button.onClick.AddListener(() => OpenSelectorForSlot(1, slot2Button));
        if (slot3Button != null) slot3Button.onClick.AddListener(() => OpenSelectorForSlot(2, slot3Button));

        gameObject.SetActive(false);
    }

    void OpenSelectorForSlot(int slotIdx, Button sourceButton)
    {
        if (partSelectorPanel == null || currentEq == null || currentEq.CurrentWeapon == null) return;
        MenuManager.PlaySelectSound();
        WeaponPartKind kind = currentEq.CurrentWeapon.GetSlotKind(slotIdx);
        partSelectorPanel.Open(currentEq, kind, sourceButton);
    }

    public void Open(PlayerHealth player, Selectable focusOnClose)
    {
        currentPlayer = player;
        currentEq = player != null ? player.GetComponent<PlayerEquipment>() : null;
        returnFocus = focusOnClose;

        // Reset the displayed snapshot — first appearance shouldn't animate.
        if (currentEq != null)
        {
            displayedDamage      = currentEq.TotalDamage;
            displayedDefense     = currentEq.TotalDefense;
            displayedAttackSpeed = currentEq.TotalAttackSpeed;

            currentEq.OnWeaponChanged += HandleWeaponChanged;
            currentEq.OnPartsChanged  += Refresh;
        }

        gameObject.SetActive(true);
        MenuManager.IsModalOpen = true;

        SetupSlotNavigation();
        Refresh();
        StartCoroutine(FocusFirstNextFrame());
    }

    /// <summary>
    /// Explicit navigation among the 3 slot buttons. Up/Down cycle through
    /// Handle → Guard → Blade; Left/Right are dead so the player can't escape
    /// to whatever's behind the modal.
    /// </summary>
    void SetupSlotNavigation()
    {
        SetExplicit(slot1Button, prev: null,        next: slot2Button);
        SetExplicit(slot2Button, prev: slot1Button, next: slot3Button);
        SetExplicit(slot3Button, prev: slot2Button, next: null);
    }

    static void SetExplicit(Button btn, Button prev, Button next)
    {
        if (btn == null) return;
        btn.navigation = new Navigation
        {
            mode          = Navigation.Mode.Explicit,
            selectOnUp    = prev,
            selectOnDown  = next,
            selectOnLeft  = null,
            selectOnRight = null,
        };
    }

    bool closing;  // set during the one-frame delay so Update ignores input

    public void Close()
    {
        if (closing) return;
        closing = true;

        if (currentEq != null)
        {
            currentEq.OnWeaponChanged -= HandleWeaponChanged;
            currentEq.OnPartsChanged  -= Refresh;
            currentEq = null;
        }
        currentPlayer = null;

        MenuManager.PlayCancelSound();

        // Defer hide + IsModalOpen reset + focus restoration by one frame.
        // Focus is restored AFTER hide so Unity doesn't clear it when our
        // selected button gets deactivated.
        StartCoroutine(HideNextFrame());
    }

    System.Collections.IEnumerator HideNextFrame()
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

        // Only close OURSELVES if no sub-modal (PartSelector) is open on top.
        if (partSelectorPanel != null && partSelectorPanel.gameObject.activeSelf) return;

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

    void HandleWeaponChanged(WeaponItem _) => Refresh();

    void Refresh()
    {
        if (currentPlayer == null || currentEq == null) return;

        // Header
        if (characterNameText != null)
            characterNameText.text = currentPlayer.character != null
                ? currentPlayer.character.displayName
                : currentPlayer.name;

        var w = currentEq.CurrentWeapon;
        if (weaponIcon != null)
        {
            weaponIcon.sprite = w != null ? w.icon : null;
            weaponIcon.enabled = weaponIcon.sprite != null;
        }
        if (weaponNameText != null)
            weaponNameText.text = w != null ? w.displayName : "(no weapon)";

        // Bind each slot using the weapon's slot kinds and the equipped parts.
        BindSlot(0, w, currentEq.GetSlotPart(0), slot1Icon, slot1NameText, slot1KindLabel);
        BindSlot(1, w, currentEq.GetSlotPart(1), slot2Icon, slot2NameText, slot2KindLabel);
        BindSlot(2, w, currentEq.GetSlotPart(2), slot3Icon, slot3NameText, slot3KindLabel);

        TweenDamage(currentEq.TotalDamage);
        TweenDefense(currentEq.TotalDefense);
        TweenAttackSpeed(currentEq.TotalAttackSpeed);
    }

    static void BindSlot(int slotIdx, WeaponItem weapon, WeaponPart part, Image icon, TMP_Text nameLabel, TMP_Text kindLabel)
    {
        if (icon != null)
        {
            icon.sprite = part != null ? part.icon : null;
            icon.enabled = icon.sprite != null;
        }
        if (nameLabel != null)
            nameLabel.text = part != null ? part.displayName : "(empty)";

        if (kindLabel != null)
        {
            kindLabel.text = weapon != null
                ? weapon.GetSlotKind(slotIdx).ToString().ToUpper()
                : "";
        }
    }

    void TweenDamage(float target)
    {
        if (damageText == null) return;
        if (Mathf.Approximately(displayedDamage, target))
        {
            damageText.text = target.ToString("0");
            return;
        }
        if (damageTween != null) StopCoroutine(damageTween);
        damageTween = StartCoroutine(StatTweenRoutine(damageText, displayedDamage, target, "0", v => displayedDamage = v));
    }

    void TweenDefense(float target)
    {
        if (defenseText == null) return;
        if (Mathf.Approximately(displayedDefense, target))
        {
            defenseText.text = target.ToString("0");
            return;
        }
        if (defenseTween != null) StopCoroutine(defenseTween);
        defenseTween = StartCoroutine(StatTweenRoutine(defenseText, displayedDefense, target, "0", v => displayedDefense = v));
    }

    void TweenAttackSpeed(float target)
    {
        if (attackSpeedText == null) return;
        if (Mathf.Approximately(displayedAttackSpeed, target))
        {
            attackSpeedText.text = target.ToString("0.00");
            return;
        }
        if (attackSpeedTween != null) StopCoroutine(attackSpeedTween);
        attackSpeedTween = StartCoroutine(StatTweenRoutine(attackSpeedText, displayedAttackSpeed, target, "0.00", v => displayedAttackSpeed = v));
    }

    IEnumerator StatTweenRoutine(TMP_Text text, float from, float to, string format, System.Action<float> writeBack)
    {
        // Tween updates displayed value, color flashes for the duration, then returns to original.
        Color originalColor = text.color;
        if (statAnimDuration <= 0f)
        {
            text.text = to.ToString(format);
            writeBack(to);
            yield break;
        }

        float t = 0f;
        while (t < statAnimDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / statAnimDuration);
            // Ease-out for a snappier feel.
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            float current = Mathf.Lerp(from, to, eased);
            text.text = current.ToString(format);
            text.color = Color.Lerp(statFlashColor, originalColor, eased);
            yield return null;
        }

        text.text = to.ToString(format);
        text.color = originalColor;
        writeBack(to);
    }

    IEnumerator FocusFirstNextFrame()
    {
        yield return null;
        if (EventSystem.current == null) yield break;

        var first = slot1Button != null && slot1Button.interactable ? slot1Button
                  : slot2Button != null && slot2Button.interactable ? slot2Button
                  : slot3Button;

        if (first != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }
}
