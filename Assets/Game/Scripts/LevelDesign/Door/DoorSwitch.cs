using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Switch / lever / button with a trigger Collider.
/// Prefer EventRunner as the motor: Interact → EventRunner.TryStart()
/// (Open Door, Wait, Camera, Dialogue, …). Legacy <see cref="doors"/> is fallback only.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorSwitch : MonoBehaviour
{
    [Header("Event Runner (motor)")]
    [Tooltip("Started on Interact. Put Open Door / Wait / Camera / Dialogue here. " +
             "Start Mode on the runner should stay Manual Only — this switch starts it.")]
    public EventRunner eventRunner;

    [Tooltip("Optional. Used when Toggle Mode is on and the player turns the switch OFF.")]
    public EventRunner deactivateEventRunner;

    [Tooltip("If true, a second Interact turns the switch off (runs Deactivate Event Runner or closes legacy doors).")]
    public bool toggleMode = false;

    [Header("Legacy doors (fallback only)")]
    [Tooltip("Used only when Event Runner is empty. Prefer Open Door inside the EventRunner.")]
    public SimpleDoor[] doors;

    [Header("Interaction")]
    [Tooltip("Optional InputAction. Empty = game default Interact.")]
    public InputActionReference interactAction;

    [Tooltip("Legacy. Prefer EventRunner Execution Mode = Once. " +
             "If true the switch only accepts Interact once (ignored when Toggle Mode is on).")]
    public bool oneShot = false;

    [Header("Persistence (legacy)")]
    [Tooltip("Legacy switch ON flag. Prefer SimpleDoor Persist + EventRunner Once.")]
    public string persistentId = "";

    [Tooltip("Legacy. Prefer persisting the door / EventRunner Once.")]
    public bool persistState = false;

    [Header("Prompt (legacy)")]
    [Tooltip("Legacy local icon. Prefer InteractivePromptTrigger on this object.")]
    public GameObject interactPrompt;

    [Header("Player Behavior")]
    [Tooltip("If true, the player can't jump while inside this trigger.")]
    public bool lockJumpInside = true;

    [Header("Visuals (optional)")]
    [Tooltip("Shown when the switch is OFF. Hidden when ON.")]
    public GameObject visualOff;

    [Tooltip("Shown when the switch is ON. Hidden when OFF.")]
    public GameObject visualOn;

    [Header("Audio")]
    [Tooltip("Fallback AudioSource if there is no AudioManager.")]
    public AudioSource sfx;

    public AudioClip activateSound;
    public AudioClip deactivateSound;

    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Unity Events (optional / advanced)")]
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

        if (eventRunner == null)
            eventRunner = GetComponent<EventRunner>();
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
        else if (doors != null && doors.Length > 0)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                    doors[i].OpenDoor();
            }
        }
        else
        {
            Debug.LogWarning(
                $"[DoorSwitch:{name}] Nothing to run — assign Event Runner " +
                "(preferred) or legacy Doors.",
                this);
            return;
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
        else if (doors != null && doors.Length > 0)
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
