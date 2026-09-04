using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Always-visible 3-slot spell wheel arranged as LEFT / CENTER / RIGHT.
///
/// The spell in the CENTER position is always the one that will be cast — it
/// is not a fixed equip slot, it's whichever equip slot is currently
/// <see cref="PlayerMagic.selectedSlot"/>. Left/Right (Dpad or A/D) cycles
/// selectedSlot, which shifts what renders in each of the 3 positions.
///
/// Two visual states, animated (fade + scale, not instant snaps):
///   - MINIMAL (resting): only the center icon shows, scaled up, no
///     background, no left/right icons. This is what's on screen most of
///     the time.
///   - EXPANDED (while actively cycling): background + all 3 icons fade/scale
///     in, so the player can see what they're picking. Collapses back to
///     MINIMAL a short time after the last cycle input, or immediately on cast.
///
/// The center icon has its own persistent bigger scale (centerScale) and
/// pops with a little overshoot bounce whenever the selection changes.
///
/// Place ONE of these on a persistent GameObject (e.g. PartyRoot) with the
/// wheel UI wired up.
/// </summary>
public class MagicCastInput : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Cast button. Fires the spell currently centered in the wheel.")]
    public InputActionReference castAction;

    [Tooltip("Moves the wheel selection one slot to the left. Bind Dpad Left + 'A'.")]
    public InputActionReference cycleLeftAction;

    [Tooltip("Moves the wheel selection one slot to the right. Bind Dpad Right + 'D'.")]
    public InputActionReference cycleRightAction;

    [Header("Wheel UI — positions, not equip slots")]
    [Tooltip("Root GameObject of the wheel (all 3 icon positions). Shown " +
             "whenever the active party member has at least one spell " +
             "equipped anywhere; hidden entirely otherwise.")]
    public GameObject wheelRoot;

    [Tooltip("Backdrop panel behind ALL 3 icons. Only shown (faded in) while " +
             "EXPANDED (actively cycling); faded out in the resting MINIMAL " +
             "state. A CanvasGroup is required on this object — one is added " +
             "automatically if missing.")]
    public GameObject background;

    [Tooltip("Small frame/backdrop specifically for the CENTER icon — a " +
             "different, smaller background than the big wheel one above. " +
             "Shown in BOTH states whenever the center position has a spell, " +
             "and pops/scales together with the center icon.")]
    public GameObject centerIconFrame;

    [Tooltip("Icon for each screen POSITION, in order: [0] = left preview, " +
             "[1] = center (the spell that will be cast), [2] = right preview. " +
             "Which equipped slot renders in each position changes as you " +
             "cycle — position 1 always shows whatever is currently selected.")]
    public Image[] slotIcons = new Image[PlayerMagic.SlotCount];

    [Tooltip("Optional highlight/border per position. Typically only [1] " +
             "(center) is used, shown while EXPANDED to mark the pick.")]
    public GameObject[] slotHighlights = new GameObject[PlayerMagic.SlotCount];

    [Tooltip("Optional 'empty' overlay per position, shown when that position " +
             "has no spell (only relevant while EXPANDED; the center position " +
             "shows nothing at all in MINIMAL if empty).")]
    public GameObject[] slotEmptyOverlays = new GameObject[PlayerMagic.SlotCount];

    [Header("Cooldown Display")]
    [Tooltip("Optional countdown number per position, same order as slotIcons. " +
             "Hidden whenever that position's spell isn't on cooldown — counts " +
             "down (e.g. 3, 2, 1) and then disappears; never shows '0'.")]
    public TMP_Text[] slotCooldownTexts = new TMP_Text[PlayerMagic.SlotCount];

    [Tooltip("Format string wrapping the whole-seconds countdown number. " +
             "{0} = seconds remaining, rounded UP so it never displays 0 while " +
             "still on cooldown (e.g. 0.4s left still shows '1', then vanishes).")]
    public string cooldownFormat = "{0}";

    [Tooltip("Icon RGB multiplier while on cooldown — alpha is left untouched " +
             "so this never fights the fade animations. 1 = no darkening, " +
             "lower = darker.")]
    [Range(0.1f, 1f)]
    public float cooldownDarkenFactor = 0.4f;

    [Tooltip("How quickly the darken tint eases in/out as cooldown starts/ends.")]
    [Range(1f, 20f)]
    public float cooldownDarkenLerpSpeed = 8f;

    [Header("Charge / Ready FX (center icon only — the equipped/active spell)")]
    [Tooltip("How much the CENTER icon shrinks while its spell is charging " +
             "(just started cooldown), relative to its normal centered scale. " +
             "1 = no shrink.")]
    [Range(0.5f, 1f)]
    public float chargingScale = 0.85f;

    [Tooltip("Duration of the shrink-while-charging transition.")]
    [Range(0.05f, 0.6f)]
    public float chargingScaleDuration = 0.2f;

    [Tooltip("Duration of the little pop/pulse back to normal size the instant " +
             "the spell becomes ready again.")]
    [Range(0.05f, 0.6f)]
    public float readyPulseDuration = 0.25f;

    [Tooltip("Optional separate image placed on top of the center icon/frame " +
             "(e.g. a plain white glow, burst or ring sprite, same size or a " +
             "bit bigger than the icon) that pulses in/out when the spell " +
             "becomes ready. Much more visible than tinting the icon itself, " +
             "since the icon's own art already has color and tinting fights " +
             "it. Assign it, set it inactive/alpha 0 in the scene, and this " +
             "script drives its alpha. Leave empty to fall back to tinting " +
             "the icon (readyFlashColor below).")]
    public Image readyFlashOverlay;

    [Tooltip("Peak alpha of the overlay image at each pulse. Only used when " +
             "Ready Flash Overlay is assigned.")]
    [Range(0f, 1f)]
    public float readyFlashOverlayAlpha = 1f;

    [Tooltip("Fallback tint color the icon flashes toward when NO Ready Flash " +
             "Overlay is assigned. RGB can go above 1 (HDR-style) for a " +
             "punchier flash — a white icon barely changes at 1,1,1, so push " +
             "it brighter than white or tint it (e.g. warm yellow).")]
    public Color readyFlashColor = new Color(2f, 2f, 1.4f);

    [Tooltip("Peak strength of the fallback tint flash, multiplies Ready " +
             "Flash Color on top of its own RGB — fades back to normal over " +
             "readyFlashDuration. 1 = flash color as-is, 0 = no flash at all. " +
             "Only used when Ready Flash Overlay is NOT assigned.")]
    [Range(0f, 3f)]
    public float readyFlashBrightness = 1.8f;

    [Tooltip("How long the ready flash takes to fade back to normal.")]
    [Range(0.05f, 0.6f)]
    public float readyFlashDuration = 0.25f;

    [Tooltip("How many times the icon blinks between Ready Flash Color and " +
             "normal before settling. 1 = a single fade (no blink), 2-3 reads " +
             "much more clearly as a 'flash' than a plain fade.")]
    [Range(1, 5)]
    public int readyFlashPulseCount = 3;

    [Header("Expand / Collapse")]
    [Tooltip("Seconds after the last cycle input before the wheel collapses " +
             "back to MINIMAL (center icon only, no background).")]
    [Min(0f)]
    public float expandedHoldDuration = 1.2f;

    [Header("Slow-Mo (only when Settings ▸ Gameplay ▸ Combat Style = Relaxed)")]
    [Tooltip("Time.timeScale applied while the wheel is EXPANDED. Entirely " +
             "ignored when GameSettings.RelaxedCombatMode is off (Active mode) " +
             "— the wheel then behaves at normal speed, same as before.")]
    [Range(0.05f, 1f)]
    public float relaxedTimeScale = 0.35f;

    [Tooltip("Seconds (unscaled) to lerp into/out of the slow-mo timescale.")]
    [Range(0f, 0.3f)]
    public float slowMoTransitionDuration = 0.08f;

    [Header("Animation")]
    [Tooltip("How much bigger the CENTER icon is, permanently, compared to " +
             "its normal size. 1 = same size, 1.2 = 20% bigger.")]
    [Range(1f, 1.6f)]
    public float centerScale = 1.2f;

    [Tooltip("Size of the LEFT/RIGHT preview icons relative to normal, while " +
             "expanded. Slightly smaller than the center makes it read as " +
             "'receding' behind the selection.")]
    [Range(0.5f, 1f)]
    public float sideScale = 0.85f;

    [Tooltip("Duration of the background + side-icon fade in/out.")]
    [Range(0.05f, 0.6f)]
    public float fadeDuration = 0.15f;

    [Tooltip("Duration of the scale pop/settle animation.")]
    [Range(0.05f, 0.6f)]
    public float scaleDuration = 0.18f;

    [Header("Behaviour")]
    public bool blockWhileMenuOpen = true;
    public bool blockWhilePaused = true;

    [Tooltip("Minimum real (unscaled) time between two accepted cast button " +
             "presses, regardless of the spell's own cooldown. Guards against " +
             "a single physical press firing the Input System's 'performed' " +
             "callback twice in the same frame — a known quirk with some " +
             "gamepad drivers — which would otherwise cast the spell twice " +
             "even before its per-spell cooldown check ever gets a chance to " +
             "matter (both calls happen at ~the same Time.time).")]
    [Range(0.05f, 0.5f)]
    public float pressDebounce = 0.12f;

    [Header("Audio (optional)")]
    public AudioClip cycleSound;
    public AudioClip deniedSound;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Debug")]
    public bool debugLog = false;

    // Tracks whichever PlayerMagic we're currently displaying/controlling —
    // rebound whenever the active party member changes.
    PlayerMagic activeMagic;
    PlayerHealth boundHealth; // used to detect active-member changes cheaply

    bool expanded;
    float lastCycleUnscaledTime = -999f;
    bool centerHasSpell; // set by RefreshWheel, read by ApplyVisibility

    // Smoothed 0..1 darken amount per position (0=index left, 1=center, 2=right),
    // eased toward 1 while that position's spell is on cooldown.
    readonly float[] cooldownDarken = new float[PlayerMagic.SlotCount];

    // Edge-detection for the center icon's charge/ready transition (shrink on
    // cooldown start, pop + flash on cooldown end). "Known" guards the very
    // first frame so we don't fire a spurious flash on startup.
    bool centerWasOnCooldown;
    bool centerCooldownStateKnown;
    bool centerFlashActive; // true while the ready-flash coroutine owns the center icon's color
    Coroutine centerFlashCoroutine;

    float lastCastPressUnscaledTime = -999f;

    const float BaseTimeScale = 1f;
    float baseFixedDeltaTime;
    Coroutine timeScaleCoroutine;

    CanvasGroup backgroundCg;

    // Generic per-target tween tracking so re-triggering an animation cancels
    // whatever was previously playing on that same element instead of stacking.
    // Behaviour is the common base of both Graphic (Image) and CanvasGroup
    // (Graphic -> UIBehaviour -> Behaviour; CanvasGroup -> Behaviour directly),
    // so this one dictionary can track fade tweens for either kind of target.
    readonly Dictionary<Behaviour, Coroutine> alphaTweens = new Dictionary<Behaviour, Coroutine>();
    readonly Dictionary<RectTransform, Coroutine> scaleTweens = new Dictionary<RectTransform, Coroutine>();

    void Awake()
    {
        // Only wheelRoot (the overall container) and centerIconFrame use plain
        // SetActive on/off. Everything that fades (background, left/right
        // icons) must stay GameObject-active permanently — their VISIBILITY is
        // driven by alpha/scale, not by (de)activating them — otherwise a
        // fade-in coroutine would have nothing active to animate.
        if (wheelRoot != null) wheelRoot.SetActive(false);

        if (background != null)
        {
            background.SetActive(true);
            backgroundCg = background.GetComponent<CanvasGroup>();
            if (backgroundCg == null) backgroundCg = background.AddComponent<CanvasGroup>();
            backgroundCg.alpha = 0f;
            backgroundCg.interactable = false;
            backgroundCg.blocksRaycasts = false;
        }

        if (slotIcons != null)
        {
            for (int i = 0; i < slotIcons.Length; i++)
            {
                if (slotIcons[i] == null) continue;
                slotIcons[i].gameObject.SetActive(true);
                // Side positions (0, 2) start fully transparent + shrunk; the
                // center (1) starts opaque — ApplyVisibility corrects all of
                // this on the first RefreshWheel anyway, this is just a sane
                // pre-animation default so there's no one-frame flash.
                if (i != 1) SetAlpha(slotIcons[i], 0f);
            }
        }

        if (centerIconFrame != null) centerIconFrame.SetActive(false);

        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    void OnEnable()
    {
        if (castAction != null && castAction.action != null)
        {
            castAction.action.performed += OnCastPerformed;
            castAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[MagicCastInput] No castAction assigned.", this);
        }

        if (cycleLeftAction != null && cycleLeftAction.action != null)
        {
            cycleLeftAction.action.performed += OnCycleLeft;
            cycleLeftAction.action.Enable();
        }

        if (cycleRightAction != null && cycleRightAction.action != null)
        {
            cycleRightAction.action.performed += OnCycleRight;
            cycleRightAction.action.Enable();
        }

        Party.OnActiveChanged += HandleActiveChanged;
        RebindToActive();
    }

    void OnDisable()
    {
        if (castAction != null && castAction.action != null)
            castAction.action.performed -= OnCastPerformed;

        if (cycleLeftAction != null && cycleLeftAction.action != null)
            cycleLeftAction.action.performed -= OnCycleLeft;

        if (cycleRightAction != null && cycleRightAction.action != null)
            cycleRightAction.action.performed -= OnCycleRight;

        Party.OnActiveChanged -= HandleActiveChanged;
        Unbind();

        // Safety: never leave the game stuck in slow-mo if this component gets
        // disabled mid-transition (scene unload, etc). Skip if GamePause has
        // Time.timeScale = 0 for an unrelated reason (menu open) — that's not
        // ours to touch.
        if (!GamePause.IsPaused)
            RestoreTimeScaleImmediate();

        if (centerFlashCoroutine != null)
        {
            StopCoroutine(centerFlashCoroutine);
            centerFlashCoroutine = null;
            centerFlashActive = false;
        }
    }

    void Update()
    {
        // Self-heal: if the active party member changed without us catching
        // the event (e.g. this component enabled before PartyRoot spawned
        // anyone, so Party.Active was null during OnEnable), pick it up here.
        // Cheap — just a reference compare, GetComponent only runs on change.
        var active = Party.Active;
        if (active != boundHealth)
            RebindToActive();

        // Auto-collapse the expanded wheel back to minimal after the hold window.
        if (expanded && Time.unscaledTime - lastCycleUnscaledTime >= expandedHoldDuration)
        {
            expanded = false;
            ApplyVisibility();
            ApplySlowMo();
        }

        UpdateCooldownDisplays();
    }

    // ============================================================
    // BINDING — follow whichever party member is active
    // ============================================================
    void HandleActiveChanged(PlayerHealth _) => RebindToActive();

    void RebindToActive()
    {
        Unbind();

        // Reset any in-progress wheel interaction from the PREVIOUS character —
        // switching active party members mid-pick shouldn't leave the game
        // stuck in slow-mo or with a stale expanded wheel.
        if (expanded)
        {
            expanded = false;
            ApplySlowMo();
        }

        // The new character's cooldown state is unrelated to the previous
        // one's — re-arm the "first frame" guard so we don't compare against
        // stale state and fire a spurious charge/ready flash on swap.
        centerCooldownStateKnown = false;
        if (centerFlashCoroutine != null)
        {
            StopCoroutine(centerFlashCoroutine);
            centerFlashCoroutine = null;
            centerFlashActive = false;
        }

        var active = Party.Active;
        boundHealth = active;

        if (active == null)
        {
            RefreshWheel();
            return;
        }

        activeMagic = active.GetComponent<PlayerMagic>();
        if (activeMagic != null)
        {
            activeMagic.OnSlotChanged += HandleSlotChanged;
            activeMagic.OnSelectedSlotChanged += HandleSelectedSlotChanged;
        }

        RefreshWheel();
    }

    void Unbind()
    {
        if (activeMagic != null)
        {
            activeMagic.OnSlotChanged -= HandleSlotChanged;
            activeMagic.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
            activeMagic = null;
        }
    }

    void HandleSlotChanged(int _, SpellItem __) => RefreshWheel();
    void HandleSelectedSlotChanged(int _)       => RefreshWheel();

    // ============================================================
    // INPUT
    // ============================================================
    bool IsBlocked()
    {
        if (blockWhileMenuOpen && MenuManager.Instance != null && MenuManager.Instance.IsOpen) return true;
        if (blockWhilePaused && GamePause.IsPaused) return true;
        return false;
    }

    void OnCycleLeft(InputAction.CallbackContext ctx)
    {
        if (IsBlocked()) return;
        Cycle(-1);
    }

    void OnCycleRight(InputAction.CallbackContext ctx)
    {
        if (IsBlocked()) return;
        Cycle(1);
    }

    void Cycle(int direction)
    {
        if (activeMagic == null) return;
        if (activeMagic.EquippedCount() == 0) return;

        int next = ((activeMagic.selectedSlot + direction) % PlayerMagic.SlotCount + PlayerMagic.SlotCount) % PlayerMagic.SlotCount;
        activeMagic.SetSelectedSlot(next); // fires HandleSelectedSlotChanged -> RefreshWheel (rebinds + re-applies)

        // Reveal the full wheel while the player is actively picking.
        expanded = true;
        lastCycleUnscaledTime = Time.unscaledTime;
        ApplyVisibility();
        ApplySlowMo();

        if (cycleSound != null) AudioManager.Play2DOrFallback(cycleSound, volume);
    }

    void OnCastPerformed(InputAction.CallbackContext ctx)
    {
        if (IsBlocked()) return;
        if (activeMagic == null) return;

        // Debounce at the input level, BEFORE touching PlayerMagic at all. This
        // catches the case where a single physical press fires 'performed'
        // twice in immediate succession (both calls would otherwise land at
        // ~the same Time.time, before the per-spell cooldown has a chance to
        // separate them).
        float now = Time.unscaledTime;
        if (now - lastCastPressUnscaledTime < pressDebounce) return;
        lastCastPressUnscaledTime = now;

        bool ok = activeMagic.Cast(activeMagic.selectedSlot);
        if (!ok)
        {
            if (debugLog) Debug.Log($"[MagicCastInput] Cast denied on slot {activeMagic.selectedSlot}.");
            if (deniedSound != null) AudioManager.Play2DOrFallback(deniedSound, volume);
            return;
        }

        // Successful cast: snap straight back to the clean minimal look — just
        // the spell that was used, no background, no side previews.
        expanded = false;
        ApplyVisibility();
        ApplySlowMo();
    }

    // ============================================================
    // DATA BINDING — which spell renders in which screen position
    // ============================================================
    /// <summary>Rebuilds which SpellItem's icon renders in each of the 3
    /// screen POSITIONS (left/center/right) based on selectedSlot, then
    /// applies the current expand/collapse visibility.</summary>
    void RefreshWheel()
    {
        if (activeMagic == null)
        {
            if (wheelRoot != null) wheelRoot.SetActive(false);
            return;
        }

        bool hasAnySpell = activeMagic.EquippedCount() > 0;
        if (wheelRoot != null) wheelRoot.SetActive(hasAnySpell);
        if (!hasAnySpell) return;

        int center = activeMagic.selectedSlot;
        int left   = (center - 1 + PlayerMagic.SlotCount) % PlayerMagic.SlotCount;
        int right  = (center + 1) % PlayerMagic.SlotCount;

        var centerSpell = activeMagic.GetEquipped(center);
        centerHasSpell = centerSpell != null;

        BindPosition(0, activeMagic.GetEquipped(left));
        BindPosition(1, centerSpell);
        BindPosition(2, activeMagic.GetEquipped(right));

        ApplyVisibility();
    }

    void BindPosition(int positionIdx, SpellItem spell)
    {
        if (slotIcons != null && positionIdx < slotIcons.Length && slotIcons[positionIdx] != null)
        {
            slotIcons[positionIdx].sprite = spell != null ? spell.icon : null;
            slotIcons[positionIdx].enabled = slotIcons[positionIdx].sprite != null;
        }

        if (slotEmptyOverlays != null && positionIdx < slotEmptyOverlays.Length && slotEmptyOverlays[positionIdx] != null)
            slotEmptyOverlays[positionIdx].SetActive(spell == null);
    }

    // ============================================================
    // COOLDOWN DISPLAY — countdown number + darken, per position, every frame
    // ============================================================
    /// <summary>Refreshes the cooldown countdown text and icon darken tint for
    /// all 3 positions. Runs every frame (from Update()) since cooldowns tick
    /// down continuously, independent of the discrete events that drive
    /// RefreshWheel/ApplyVisibility.</summary>
    void UpdateCooldownDisplays()
    {
        if (activeMagic == null) return;

        int center = activeMagic.selectedSlot;
        int left = (center - 1 + PlayerMagic.SlotCount) % PlayerMagic.SlotCount;
        int right = (center + 1) % PlayerMagic.SlotCount;

        UpdateCooldownForPosition(0, left);
        bool centerOnCooldown = UpdateCooldownForPosition(1, center);
        UpdateCooldownForPosition(2, right);

        HandleCenterChargeState(centerOnCooldown);
    }

    /// <summary>Updates the countdown text + darken tint for one position.
    /// Returns whether that position's spell is currently on cooldown.</summary>
    bool UpdateCooldownForPosition(int positionIdx, int equipSlotIdx)
    {
        var spell = activeMagic.GetEquipped(equipSlotIdx);
        float remaining = spell != null ? activeMagic.CooldownRemaining(equipSlotIdx) : 0f;
        bool onCooldown = remaining > 0f;

        if (slotCooldownTexts != null && positionIdx < slotCooldownTexts.Length && slotCooldownTexts[positionIdx] != null)
        {
            var text = slotCooldownTexts[positionIdx];
            text.gameObject.SetActive(onCooldown);
            if (onCooldown)
            {
                // Round UP so the last fraction of a second still reads as "1",
                // not "0" — the number should count down and then vanish, never
                // sit on-screen showing "0" while the spell is still blocked.
                int secondsLeft = Mathf.CeilToInt(remaining);
                text.text = string.Format(cooldownFormat, secondsLeft);
            }
        }

        float target = onCooldown ? 1f : 0f;
        cooldownDarken[positionIdx] = Mathf.MoveTowards(cooldownDarken[positionIdx], target,
                                                          Time.unscaledDeltaTime * cooldownDarkenLerpSpeed);

        // While the center icon's one-shot ready-flash is playing, it owns the
        // color for that frame — skip so we don't fight it (see TriggerReadyFlash).
        bool skipColorWrite = positionIdx == 1 && centerFlashActive;

        if (!skipColorWrite && slotIcons != null && positionIdx < slotIcons.Length && slotIcons[positionIdx] != null)
        {
            var icon = slotIcons[positionIdx];
            float rgb = Mathf.Lerp(1f, cooldownDarkenFactor, cooldownDarken[positionIdx]);
            Color c = icon.color; // preserve whatever alpha the fade system currently has set
            icon.color = new Color(rgb, rgb, rgb, c.a);
        }

        return onCooldown;
    }

    /// <summary>Edge-triggered charge/ready FX for the CENTER icon only (the
    /// equipped/active spell): shrinks the instant its cooldown starts
    /// (casting), and pops back to normal size with a quick bright flash the
    /// instant it becomes ready again. Reuses the same ScaleTo tween system
    /// as the rest of the wheel, so it never fights the expand/collapse pop.</summary>
    void HandleCenterChargeState(bool onCooldown)
    {
        if (!centerCooldownStateKnown)
        {
            // First frame we ever see this — just record the state, don't fire
            // a spurious shrink/flash on startup.
            centerWasOnCooldown = onCooldown;
            centerCooldownStateKnown = true;
            return;
        }

        if (slotIcons == null || slotIcons.Length < 2 || slotIcons[1] == null)
        {
            centerWasOnCooldown = onCooldown;
            return;
        }

        var icon = slotIcons[1];
        float normalScale = centerHasSpell ? centerScale : 1f;

        if (onCooldown && !centerWasOnCooldown)
        {
            // Just started charging (cast happened) — shrink a bit.
            ScaleTo(icon.rectTransform, normalScale * chargingScale, chargingScaleDuration, bounce: false);
        }
        else if (!onCooldown && centerWasOnCooldown)
        {
            // Just became ready — pop back to normal size (the overshoot bounce
            // reads as a quick pulse) and flash bright for an instant.
            ScaleTo(icon.rectTransform, normalScale, readyPulseDuration, bounce: true);
            TriggerReadyFlash(icon);
        }

        centerWasOnCooldown = onCooldown;
    }

    void TriggerReadyFlash(Image icon)
    {
        if (centerFlashCoroutine != null) StopCoroutine(centerFlashCoroutine);
        centerFlashCoroutine = StartCoroutine(ReadyFlashRoutine(icon));
    }

    IEnumerator ReadyFlashRoutine(Image icon)
    {
        centerFlashActive = true;

        bool useOverlay = readyFlashOverlay != null;
        int pulses = Mathf.Max(1, readyFlashPulseCount);

        // Tint fallback setup (only touched if there's no overlay image).
        float iconAlpha = icon.color.a;
        Color peak = Color.Lerp(Color.white, readyFlashColor, readyFlashBrightness);

        if (useOverlay)
        {
            readyFlashOverlay.gameObject.SetActive(true);
            SetAlpha(readyFlashOverlay, 0f);
        }

        float t = 0f;
        while (t < readyFlashDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / readyFlashDuration);

            // Blink `pulses` times, eased down to 0 by the end so it settles
            // cleanly instead of cutting off mid-blink.
            float wave = 0.5f - 0.5f * Mathf.Cos(k * pulses * Mathf.PI * 2f);
            float envelope = 1f - k;

            if (useOverlay)
            {
                SetAlpha(readyFlashOverlay, wave * envelope * readyFlashOverlayAlpha);
            }
            else
            {
                Color c = Color.Lerp(Color.white, peak, wave * envelope);
                icon.color = new Color(c.r, c.g, c.b, iconAlpha);
            }

            yield return null;
        }

        if (useOverlay)
        {
            SetAlpha(readyFlashOverlay, 0f);
            readyFlashOverlay.gameObject.SetActive(false);
        }
        else
        {
            Color final = icon.color;
            icon.color = new Color(1f, 1f, 1f, final.a);
        }

        centerFlashActive = false;
        centerFlashCoroutine = null;
    }

    // ============================================================
    // VISIBILITY / ANIMATION — MINIMAL vs EXPANDED
    // ============================================================
    /// <summary>Applies MINIMAL vs EXPANDED visibility to the wheel, animated,
    /// without touching which spell is bound to which position (that's
    /// RefreshWheel's job). Safe to call on its own when only the expand
    /// state changes.</summary>
    void ApplyVisibility()
    {
        if (activeMagic == null) return;
        if (wheelRoot == null || !wheelRoot.activeSelf) return;

        // Background fades in/out with the expand state.
        if (backgroundCg != null)
            FadeCanvasGroup(backgroundCg, expanded ? 1f : 0f, fadeDuration);

        // Center frame + icon: always visible (if it has a spell), permanently
        // bigger than the sides, and pops with a little bounce whenever the
        // bound spell / expand state changes.
        if (centerIconFrame != null)
        {
            centerIconFrame.SetActive(centerHasSpell);
            var frameRt = centerIconFrame.GetComponent<RectTransform>();
            if (frameRt != null) ScaleTo(frameRt, centerHasSpell ? centerScale : 1f, scaleDuration, bounce: true);
        }
        if (slotIcons != null && slotIcons.Length > 1 && slotIcons[1] != null)
            ScaleTo(slotIcons[1].rectTransform, centerHasSpell ? centerScale : 1f, scaleDuration, bounce: true);

        // Left/Right previews fade + scale in while expanded, fade + shrink
        // back out when collapsing.
        AnimateSide(0);
        AnimateSide(2);

        if (slotHighlights != null)
        {
            for (int i = 0; i < slotHighlights.Length; i++)
                if (slotHighlights[i] != null)
                    slotHighlights[i].SetActive(i == 1 && expanded);
        }
    }

    void AnimateSide(int positionIdx)
    {
        if (slotIcons == null || positionIdx >= slotIcons.Length || slotIcons[positionIdx] == null) return;

        var icon = slotIcons[positionIdx];
        FadeTo(icon, expanded ? 1f : 0f, fadeDuration);
        ScaleTo(icon.rectTransform, expanded ? sideScale : sideScale * 0.8f, scaleDuration, bounce: false);

        if (slotEmptyOverlays != null && positionIdx < slotEmptyOverlays.Length && slotEmptyOverlays[positionIdx] != null)
        {
            bool positionEmpty = icon.sprite == null;
            slotEmptyOverlays[positionIdx].SetActive(expanded && positionEmpty);
        }
    }

    // ============================================================
    // TWEEN HELPERS (unscaled time — plays even during slow-mo/pause elsewhere)
    // ============================================================
    void FadeCanvasGroup(CanvasGroup cg, float target, float duration)
    {
        StartTween<Behaviour>(alphaTweens, cg, FadeCanvasGroupRoutine(cg, target, duration));
    }

    IEnumerator FadeCanvasGroupRoutine(CanvasGroup cg, float target, float duration)
    {
        float start = cg.alpha;
        if (duration <= 0f) { cg.alpha = target; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        cg.alpha = target;
    }

    void FadeTo(Graphic g, float targetAlpha, float duration)
    {
        StartTween<Behaviour>(alphaTweens, g, FadeRoutine(g, targetAlpha, duration));
    }

    IEnumerator FadeRoutine(Graphic g, float targetAlpha, float duration)
    {
        float start = g.color.a;
        if (duration <= 0f) { SetAlpha(g, targetAlpha); yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(g, Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAlpha(g, targetAlpha);
    }

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    void ScaleTo(RectTransform rt, float targetScale, float duration, bool bounce)
    {
        StartTween(scaleTweens, rt, ScaleRoutine(rt, targetScale, duration, bounce));
    }

    IEnumerator ScaleRoutine(RectTransform rt, float targetScale, float duration, bool bounce)
    {
        float start = rt.localScale.x;
        if (duration <= 0f) { rt.localScale = Vector3.one * targetScale; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = bounce ? EaseOutBack(k) : EaseOutCubic(k);
            float v = Mathf.LerpUnclamped(start, targetScale, eased);
            rt.localScale = new Vector3(v, v, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one * targetScale;
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    /// <summary>Starts a coroutine tracked per-target so a re-triggered
    /// animation cancels whatever was previously playing on that same
    /// element instead of stacking multiple tweens on it.</summary>
    void StartTween<T>(Dictionary<T, Coroutine> tracker, T key, IEnumerator routine)
    {
        if (key == null) return;
        if (tracker.TryGetValue(key, out var existing) && existing != null)
            StopCoroutine(existing);
        tracker[key] = StartCoroutine(routine);
    }

    // ============================================================
    // SLOW-MO — only when GameSettings.RelaxedCombatMode is on
    // ============================================================
    /// <summary>Called right after `expanded` changes. In Active mode
    /// (RelaxedCombatMode off) this is a no-op — except it snaps back to
    /// normal speed immediately if the setting was toggled OFF while the
    /// wheel happened to be mid-slow-mo, so the change takes effect instantly
    /// instead of waiting for the wheel to close.</summary>
    void ApplySlowMo()
    {
        if (!GameSettings.RelaxedCombatMode)
        {
            if (!Mathf.Approximately(Time.timeScale, BaseTimeScale))
                RestoreTimeScaleImmediate();
            return;
        }

        float target = expanded ? relaxedTimeScale : BaseTimeScale;
        StartTimeScaleTransition(target, slowMoTransitionDuration);
    }

    void StartTimeScaleTransition(float target, float durationUnscaled)
    {
        if (timeScaleCoroutine != null) StopCoroutine(timeScaleCoroutine);
        timeScaleCoroutine = StartCoroutine(TimeScaleRoutine(target, durationUnscaled));
    }

    IEnumerator TimeScaleRoutine(float target, float durationUnscaled)
    {
        float start = Time.timeScale;

        if (durationUnscaled <= 0f)
        {
            SetTimeScale(target);
            timeScaleCoroutine = null;
            yield break;
        }

        float t = 0f;
        while (t < durationUnscaled)
        {
            t += Time.unscaledDeltaTime;
            SetTimeScale(Mathf.Lerp(start, target, Mathf.Clamp01(t / durationUnscaled)));
            yield return null;
        }

        SetTimeScale(target);
        timeScaleCoroutine = null;
    }

    void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        // Keep physics feeling right under slow-mo — scale fixedDeltaTime to match.
        Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0.01f, scale);
    }

    void RestoreTimeScaleImmediate()
    {
        if (timeScaleCoroutine != null)
        {
            StopCoroutine(timeScaleCoroutine);
            timeScaleCoroutine = null;
        }
        SetTimeScale(BaseTimeScale);
    }
}
