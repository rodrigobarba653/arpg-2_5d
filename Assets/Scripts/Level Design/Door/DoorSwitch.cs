using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Place this on a switch / lever / button (with a trigger Collider). When the
/// player is inside the trigger and presses interactKey, every linked SimpleDoor
/// receives OpenDoor().
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorSwitch : MonoBehaviour
{
    [Header("Doors")]
    [Tooltip("Doors this switch opens.")]
    public SimpleDoor[] doors;

    [Header("Interaction")]
    [Tooltip("Optional: assign an InputAction (e.g. 'Interact' from PlayerControls). " +
             "If empty, defaults to Enter/E on keyboard + X/A button on gamepad.")]
    public InputActionReference interactAction;

    [Tooltip("If true the switch only works once.")]
    public bool oneShot = false;

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

    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Events")]
    public UnityEvent onActivated;

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

    void Awake()
    {
        ApplyVisualState();

        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (spent) return;
        if (playerInRange == null) return;

        if (InteractInput.WasPressedThisFrame(interactAction))
            Activate();
    }

    public void Activate()
    {
        if (spent) return;

        if (doors != null)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                    doors[i].OpenDoor();
            }
        }

        isOn = true;
        ApplyVisualState();

        AudioManager.PlaySfxOrFallback(activateSound, transform.position, audioVolume, sfx);
        onActivated?.Invoke();

        if (oneShot)
        {
            spent = true;
            ShowPrompt(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) return;

        playerInRange = inv;
        playerJump = other.GetComponentInParent<PlayerJump>();

        if (!spent)
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

    void OnDisable()
    {
        ReleaseJumpLock();
    }

    void ReleaseJumpLock()
    {
        if (jumpLockApplied && playerJump != null)
        {
            playerJump.RemoveExternalLock();
        }

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
        if (visualOn)  visualOn.SetActive(isOn);
    }
}
