using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

public class SimpleDoor : MonoBehaviour
{
    public enum OpenMode
    {
        Auto,    // opens on player trigger enter
        Switch,  // opens only via DoorSwitch.OpenDoor() / external call
        Key      // opens on E inside trigger, requires a KeyItem with matching keyId
    }

    [Header("Open Mode")]
    public OpenMode openMode = OpenMode.Auto;

    [Header("Persistence")]
    [Tooltip("Stable id for WorldState (open + unlocked). Required when Persist State is on.")]
    public string persistentId = "";

    [Tooltip("Save/load open and unlocked via WorldState. No disk write until the player saves.")]
    public bool persistState = false;

    [Tooltip("Inspector default when no saved state exists (New Game).")]
    public bool startOpen = false;

    [Header("Motion Mode")]
    public bool useRotate = true;
    public bool useSlideUp = false;
    public bool useSlideHorizontal = false;
    public bool useDoubleHorizontal = false;

    [Header("Rotate Door")]
    public float openAngle = 90f;
    public float rotateSpeed = 3f;

    [Header("Slide Vertical")]
    public float openHeight = 3f;
    public float moveSpeed = 3f;

    [Header("Slide Horizontal")]
    public float openDistance = 3f;
    public bool slideToRight = true;

    [Header("Double Horizontal")]
    [Tooltip("Left panel — slides AWAY from the door's center along -transform.right.")]
    public Transform leftPanel;

    [Tooltip("Right panel — slides AWAY from the door's center along +transform.right.")]
    public Transform rightPanel;

    [Tooltip("How far each panel slides apart from its closed position.")]
    public float doubleOpenDistance = 1.5f;

    [Header("Auto / Key Trigger")]
    [Tooltip("Trigger collider that detects the player. Defaults to a Collider on this object marked IsTrigger.")]
    public Collider triggerZone;

    [Header("Key Settings")]
    [Tooltip("Drag the item ScriptableObject that opens this door (e.g. 'Small Key'). " +
             "Any instance of this item in the inventory unlocks the door. " +
             "Recommended over the string id.")]
    public ItemDefinition requiredKey;

    [Tooltip("Optional fallback: match by KeyItem.keyId string. Used only if Required Key is empty.")]
    public string requiredKeyId;

    [Tooltip("If true the key is removed from the inventory when used.")]
    public bool consumeKeyOnUse = false;

    [Tooltip("Optional: assign an InputAction (e.g. 'Interact' from PlayerControls). " +
             "If empty, defaults to Enter/E on keyboard + X/A button on gamepad.")]
    public InputActionReference interactAction;

    [Header("Prompt")]
    [Tooltip("Icon (child GameObject) shown while the player is in range. Hidden by default.")]
    public GameObject interactPrompt;

    [Header("Player Behavior")]
    [Tooltip("If true, the player can't jump while inside this door's trigger.")]
    public bool lockJumpInside = true;

    [Header("Audio")]
    [Tooltip("Optional fallback AudioSource used only if there's no AudioManager in the scene.")]
    public AudioSource sfx;

    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;
    public UnityEvent onLockedRefused;

    public bool IsOpen => isOpen;
    public bool IsMoving => isMoving;
    public bool IsUnlocked => isUnlocked || isOpen;

    bool isOpen;
    bool isMoving;
    bool isUnlocked;
    bool posesReady;
    Coroutine motionRoutine;

    Vector3 closedPosition;
    Vector3 openPosition;

    Quaternion closedRotation;
    Quaternion openRotation;

    Vector3 leftPanelClosed;
    Vector3 rightPanelClosed;
    Vector3 leftPanelOpen;
    Vector3 rightPanelOpen;

    PlayerInventory playerInRange;
    PlayerJump playerJump;
    bool jumpLockApplied;

    void OnValidate()
    {
        int activeModes = 0;
        if (useRotate)           activeModes++;
        if (useSlideUp)          activeModes++;
        if (useSlideHorizontal)  activeModes++;
        if (useDoubleHorizontal) activeModes++;

        if (activeModes > 1)
        {
            if (useRotate)
            {
                useSlideUp = false;
                useSlideHorizontal = false;
                useDoubleHorizontal = false;
            }
            else if (useSlideUp)
            {
                useSlideHorizontal = false;
                useDoubleHorizontal = false;
            }
            else if (useSlideHorizontal)
            {
                useDoubleHorizontal = false;
            }
        }
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

    void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        CachePoses();
        posesReady = true;

        if (persistState && !string.IsNullOrEmpty(persistentId))
            ApplyPersistedState();
        else if (startOpen)
            SetStateImmediate(true, unlocked: true);
    }

    void CachePoses()
    {
        closedPosition = transform.position;
        closedRotation = transform.rotation;

        openRotation = Quaternion.Euler(
            transform.eulerAngles + new Vector3(0f, openAngle, 0f)
        );

        if (useSlideUp)
            openPosition = closedPosition + transform.up * openHeight;

        if (useSlideHorizontal)
        {
            Vector3 dir = slideToRight ? transform.right : -transform.right;
            openPosition = closedPosition + dir * openDistance;
        }

        if (useDoubleHorizontal)
        {
            if (leftPanel != null)
            {
                leftPanelClosed = leftPanel.position;
                leftPanelOpen   = leftPanelClosed + (-transform.right) * doubleOpenDistance;
            }

            if (rightPanel != null)
            {
                rightPanelClosed = rightPanel.position;
                rightPanelOpen   = rightPanelClosed + transform.right * doubleOpenDistance;
            }
        }
    }

    void Update()
    {
        if (openMode == OpenMode.Key && playerInRange != null && !isOpen)
        {
            if (InteractInput.WasPressedThisFrame(interactAction))
                TryOpenWithKey(playerInRange);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        var jump = other.GetComponentInParent<PlayerJump>();

        if (lockJumpInside && jump != null && !jumpLockApplied && openMode != OpenMode.Switch)
        {
            playerJump = jump;
            playerJump.AddExternalLock();
            jumpLockApplied = true;
        }

        if (openMode == OpenMode.Auto)
        {
            if (other.CompareTag("Player") || inv != null)
                OpenDoor();
            return;
        }

        if (openMode == OpenMode.Key && inv != null)
        {
            playerInRange = inv;
            if (!isOpen) ShowPrompt(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var jump = other.GetComponentInParent<PlayerJump>();
        if (jump != null && jump == playerJump)
            ReleaseJumpLock();

        if (openMode != OpenMode.Key) return;

        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv != null && inv == playerInRange)
        {
            playerInRange = null;
            ShowPrompt(false);
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

    /// <summary>
    /// Generic open. Used by Auto, Switch, Event actions, and after a successful key check.
    /// No-ops if already open or mid-motion.
    /// </summary>
    public void OpenDoor()
    {
        if (isOpen || isMoving) return;
        motionRoutine = StartCoroutine(OpenRoutine());
    }

    /// <summary>
    /// Close the door. No-ops if already closed or mid-motion.
    /// </summary>
    public void CloseDoor()
    {
        if (!isOpen || isMoving) return;
        motionRoutine = StartCoroutine(CloseRoutine());
    }

    /// <summary>
    /// Instant open/close for save restore / unlock. No sound, no UnityEvents.
    /// </summary>
    /// <param name="writePersist">
    /// False when applying a loaded WorldState (avoid rewriting defaults into flags).
    /// True when unlocking via gameplay actions.
    /// </param>
    public void SetStateImmediate(bool open, bool unlocked = false, bool writePersist = true)
    {
        if (motionRoutine != null)
        {
            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }

        isMoving = false;
        isOpen = open;
        if (open || unlocked)
            isUnlocked = true;
        else
            isUnlocked = unlocked;

        if (posesReady)
            ApplyPose(open);

        ShowPrompt(false);

        if (writePersist)
            WritePersistedState();
    }

    /// <summary>
    /// Try to open this door using the given inventory. Returns true if it opened
    /// or was already open/unlocked.
    /// Unlocking and opening are separate: once unlocked, the key is never consumed again.
    /// </summary>
    public bool TryOpenWithKey(PlayerInventory inventory)
    {
        if (isOpen) return true;

        // Already unlocked (this session or from save) — open without consuming another key.
        if (isUnlocked)
        {
            OpenDoor();
            return true;
        }

        if (requiredKey != null)
        {
            if (inventory == null || !inventory.HasItem(requiredKey))
            {
                RefuseLocked();
                return false;
            }

            bool consume = consumeKeyOnUse;
            if (requiredKey is KeyItem keyAsKey && keyAsKey.consumeOnUse)
                consume = true;

            if (consume)
                inventory.Remove(requiredKey, 1);

            MarkUnlocked();
            OpenDoor();
            return true;
        }

        if (string.IsNullOrEmpty(requiredKeyId))
        {
            MarkUnlocked();
            OpenDoor();
            return true;
        }

        if (inventory == null)
        {
            RefuseLocked();
            return false;
        }

        var key = inventory.FindKey(requiredKeyId);
        if (key == null)
        {
            RefuseLocked();
            return false;
        }

        if (consumeKeyOnUse || key.consumeOnUse)
            inventory.Remove(key, 1);

        MarkUnlocked();
        OpenDoor();
        return true;
    }

    void MarkUnlocked()
    {
        isUnlocked = true;
        WritePersistedState();
    }

    void RefuseLocked()
    {
        AudioManager.PlaySfxOrFallback(lockedSound, transform.position, audioVolume, sfx);
        onLockedRefused?.Invoke();
    }

    IEnumerator OpenRoutine()
    {
        isMoving = true;
        AudioManager.PlaySfxOrFallback(openSound, transform.position, audioVolume, sfx);

        yield return AnimateTowardOpen();

        isOpen = true;
        isUnlocked = true;
        isMoving = false;
        motionRoutine = null;

        ShowPrompt(false);
        WritePersistedState();
        onOpened?.Invoke();
    }

    IEnumerator CloseRoutine()
    {
        isMoving = true;
        AudioManager.PlaySfxOrFallback(closeSound, transform.position, audioVolume, sfx);

        yield return AnimateTowardClosed();

        isOpen = false;
        isMoving = false;
        motionRoutine = null;

        WritePersistedState();
        onClosed?.Invoke();
    }

    IEnumerator AnimateTowardOpen()
    {
        if (useRotate)
        {
            while (Quaternion.Angle(transform.rotation, openRotation) > 0.5f)
            {
                transform.rotation = Quaternion.Lerp(
                    transform.rotation, openRotation, Time.deltaTime * rotateSpeed);
                yield return null;
            }
            transform.rotation = openRotation;
        }
        else if (useSlideUp || useSlideHorizontal)
        {
            while (Vector3.Distance(transform.position, openPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, openPosition, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = openPosition;
        }
        else if (useDoubleHorizontal)
        {
            bool leftDone, rightDone;
            do
            {
                leftDone = true;
                rightDone = true;

                if (leftPanel != null)
                {
                    leftPanel.position = Vector3.MoveTowards(
                        leftPanel.position, leftPanelOpen, moveSpeed * Time.deltaTime);
                    leftDone = Vector3.Distance(leftPanel.position, leftPanelOpen) <= 0.01f;
                }

                if (rightPanel != null)
                {
                    rightPanel.position = Vector3.MoveTowards(
                        rightPanel.position, rightPanelOpen, moveSpeed * Time.deltaTime);
                    rightDone = Vector3.Distance(rightPanel.position, rightPanelOpen) <= 0.01f;
                }

                yield return null;
            } while (!leftDone || !rightDone);

            if (leftPanel != null) leftPanel.position = leftPanelOpen;
            if (rightPanel != null) rightPanel.position = rightPanelOpen;
        }
    }

    IEnumerator AnimateTowardClosed()
    {
        if (useRotate)
        {
            while (Quaternion.Angle(transform.rotation, closedRotation) > 0.5f)
            {
                transform.rotation = Quaternion.Lerp(
                    transform.rotation, closedRotation, Time.deltaTime * rotateSpeed);
                yield return null;
            }
            transform.rotation = closedRotation;
        }
        else if (useSlideUp || useSlideHorizontal)
        {
            while (Vector3.Distance(transform.position, closedPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, closedPosition, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = closedPosition;
        }
        else if (useDoubleHorizontal)
        {
            bool leftDone, rightDone;
            do
            {
                leftDone = true;
                rightDone = true;

                if (leftPanel != null)
                {
                    leftPanel.position = Vector3.MoveTowards(
                        leftPanel.position, leftPanelClosed, moveSpeed * Time.deltaTime);
                    leftDone = Vector3.Distance(leftPanel.position, leftPanelClosed) <= 0.01f;
                }

                if (rightPanel != null)
                {
                    rightPanel.position = Vector3.MoveTowards(
                        rightPanel.position, rightPanelClosed, moveSpeed * Time.deltaTime);
                    rightDone = Vector3.Distance(rightPanel.position, rightPanelClosed) <= 0.01f;
                }

                yield return null;
            } while (!leftDone || !rightDone);

            if (leftPanel != null) leftPanel.position = leftPanelClosed;
            if (rightPanel != null) rightPanel.position = rightPanelClosed;
        }
    }

    void ApplyPose(bool open)
    {
        if (useRotate)
        {
            transform.rotation = open ? openRotation : closedRotation;
        }
        else if (useSlideUp || useSlideHorizontal)
        {
            transform.position = open ? openPosition : closedPosition;
        }
        else if (useDoubleHorizontal)
        {
            if (leftPanel != null)
                leftPanel.position = open ? leftPanelOpen : leftPanelClosed;
            if (rightPanel != null)
                rightPanel.position = open ? rightPanelOpen : rightPanelClosed;
        }
    }

    void ApplyPersistedState()
    {
        if (!posesReady || !persistState)
            return;

        if (string.IsNullOrEmpty(persistentId))
        {
            Debug.LogWarning(
                $"[SimpleDoor] Persist State enabled on '{name}' but persistentId is empty.",
                this);
            return;
        }

        // No saved keys yet → Inspector default.
        if (!WorldState.HasFlag(OpenFlagId()) && !WorldState.HasFlag(UnlockedFlagId()))
        {
            if (startOpen)
                SetStateImmediate(true, unlocked: true, writePersist: false);
            else
                SetStateImmediate(false, unlocked: false, writePersist: false);
            return;
        }

        bool unlocked = WorldState.GetFlag(UnlockedFlagId());
        bool open = WorldState.GetFlag(OpenFlagId());
        SetStateImmediate(open, unlocked || open, writePersist: false);
    }

    void WritePersistedState()
    {
        if (!persistState || string.IsNullOrEmpty(persistentId))
            return;

        WorldState.SetFlag(OpenFlagId(), isOpen);
        WorldState.SetFlag(UnlockedFlagId(), isUnlocked || isOpen);
    }

    string OpenFlagId() => $"door.{persistentId}.open";
    string UnlockedFlagId() => $"door.{persistentId}.unlocked";

    void WarnIfDuplicatePersistentId()
    {
        if (string.IsNullOrEmpty(persistentId))
            return;

        var doors = Object.FindObjectsByType<SimpleDoor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < doors.Length; i++)
        {
            var other = doors[i];
            if (other == null || other == this)
                continue;
            if (other.persistentId != persistentId)
                continue;

            Debug.LogWarning(
                $"[SimpleDoor] Duplicate persistentId '{persistentId}' on '{name}' and '{other.name}'.",
                this);
            break;
        }
    }
}
