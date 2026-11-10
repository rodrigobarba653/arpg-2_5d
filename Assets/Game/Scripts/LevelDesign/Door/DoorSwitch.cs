using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Switch / lever / button with a trigger Collider.
///
/// Modes:
/// - Legacy: presses interact → OpenDoor() on linked doors.
/// - Event System: presses interact → EventRunner.TryStart() (doors opened via OpenDoorAction).
/// - Toggle: second press runs deactivateRunner / CloseDoor on linked doors.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorSwitch : MonoBehaviour
{
    [Header("Doors (legacy direct link)")]
    [Tooltip("Doors this switch opens directly. Ignored when Event Runner is assigned " +
             "(use OpenDoorAction in that runner instead).")]
    public SimpleDoor[] doors;

    [Header("Event System")]
    [Tooltip("If set, Activate starts this runner instead of calling doors[].OpenDoor().")]
    public EventRunner eventRunner;

    [Tooltip("Optional runner when toggling OFF (toggleMode).")]
    public EventRunner deactivateEventRunner;

    [Tooltip("If true, a second interact turns the switch off.")]
    public bool toggleMode = false;

    [Header("Interaction")]
    [Tooltip("Optional: assign an InputAction (e.g. 'Interact' from PlayerControls). " +
             "If empty, defaults to Enter/E on keyboard + X/A button on gamepad.")]
    public InputActionReference interactAction;

    [Tooltip("If true the switch only works once (ignored when toggleMode is on).")]
    public bool oneShot = false;

    [Header("Persistence")]
    public string persistentId = "";
    public bool persistState = false;

    [Header("Prompt")]
    [Tooltip("Icon (child GameObject) shown while the player is in range. Hidden by default.")]
    public GameObject interactPrompt;

    [Header("Player Behavior")]
    [Tooltip("If true, the player can't jump while inside this trigger.")]
    public bool lockJumpInside = true;

    [Header("Visuals (optional)")]
    public GameObject visualOff;
    public GameObject visualOn;

    [Header("Audio")]
    [Tooltip("Optional fallback AudioSource used only if there's no AudioManager in the scene.")]
    public AudioSource sfx;

    public AudioClip activateSound;
    public AudioClip deactivateSound;

    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Events")]
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;

    public bool IsOn => isOn;

    PlayerInventory playerInRange;
    PlayerJump playerJump;
    bool jumpLockApplied;
    bool isOn;
    bool spent;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnEnable()
    {
        WorldState.OnRestored += ApplyPersistedState;
        WarnIfDuplicatePersistentId();
    }

    void OnDisable()
    {
        WorldState.OnRestored -= ApplyPersistedState;
        ReleaseJumpLock();
    }

    void Awake()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    void Start()
    {
        ApplyPersistedState();
        ApplyVisualState();
    }

    void Update()
    {
        if (spent && !toggleMode) return;
        if (playerInRange == null) return;

        // Block re-entry while a linked event sequence is running.
        if (eventRunner != null && eventRunner.IsRunning)
            return;
        if (deactivateEventRunner != null && deactivateEventRunner.IsRunning)
            return;

        if (InteractInput.WasPressedThisFrame(interactAction))
        {
            if (toggleMode && isOn)
                Deactivate();
            else
                Activate();
        }
    }

    public void Activate()
    {
        if (spent && !toggleMode) return;
        if (eventRunner != null && eventRunner.IsRunning) return;

        if (eventRunner != null)
        {
            if (!eventRunner.TryStart())
                return;
        }
        else if (doors != null)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                    doors[i].OpenDoor();
            }
        }

        isOn = true;
        ApplyVisualState();
        WritePersistedState();

        AudioManager.PlaySfxOrFallback(activateSound, transform.position, audioVolume, sfx);
        onActivated?.Invoke();

        if (oneShot && !toggleMode)
        {
            spent = true;
            ShowPrompt(false);
        }
    }

    public void Deactivate()
    {
        if (!toggleMode) return;
        if (deactivateEventRunner != null && deactivateEventRunner.IsRunning) return;

        if (deactivateEventRunner != null)
        {
            if (!deactivateEventRunner.TryStart())
                return;
        }
        else if (doors != null)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                    doors[i].CloseDoor();
            }
        }

        isOn = false;
        spent = false;
        ApplyVisualState();
        WritePersistedState();

        AudioManager.PlaySfxOrFallback(deactivateSound, transform.position, audioVolume, sfx);
        onDeactivated?.Invoke();
    }

    /// <summary>Silent restore for save/load — no events, sounds, or door motion.</summary>
    public void SetStateImmediate(bool on, bool markSpent = false)
    {
        isOn = on;
        spent = markSpent || (oneShot && on && !toggleMode);
        ApplyVisualState();
        if (spent)
            ShowPrompt(false);
    }

    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) return;

        playerInRange = inv;
        playerJump = other.GetComponentInParent<PlayerJump>();

        if (!spent || toggleMode)
            ShowPrompt(true);

        if (lockJumpInside && playerJump != null && !jumpLockApplied)
        {
            playerJump.AddExternalLock();
            jumpLockApplied = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv != null && inv == playerInRange)
        {
            playerInRange = null;
            ShowPrompt(false);
            ReleaseJumpLock();
        }
    }

    void ReleaseJumpLock()
    {
        if (jumpLockApplied && playerJump != null)
            playerJump.RemoveExternalLock();

        jumpLockApplied = false;
        playerJump = null;
    }

    void ShowPrompt(bool show)
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(show);
    }

    void ApplyVisualState()
    {
        if (visualOff) visualOff.SetActive(!isOn);
        if (visualOn) visualOn.SetActive(isOn);
    }

    void ApplyPersistedState()
    {
        if (!persistState || string.IsNullOrEmpty(persistentId))
            return;

        // Missing flag → New Game / cleared WorldState → Inspector default (off).
        if (!WorldState.HasFlag(OnFlagId()))
        {
            SetStateImmediate(false, markSpent: false);
            return;
        }

        bool on = WorldState.GetFlag(OnFlagId());
        SetStateImmediate(on, markSpent: oneShot && on && !toggleMode);
    }

    void WritePersistedState()
    {
        if (!persistState || string.IsNullOrEmpty(persistentId))
            return;

        WorldState.SetFlag(OnFlagId(), isOn);
    }

    string OnFlagId() => $"switch.{persistentId}.on";

    void WarnIfDuplicatePersistentId()
    {
        if (string.IsNullOrEmpty(persistentId))
            return;

        var switches = Object.FindObjectsByType<DoorSwitch>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < switches.Length; i++)
        {
            var other = switches[i];
            if (other == null || other == this)
                continue;
            if (other.persistentId != persistentId)
                continue;

            Debug.LogWarning(
                $"[DoorSwitch] Duplicate persistentId '{persistentId}' on '{name}' and '{other.name}'.",
                this);
            break;
        }
    }
}
