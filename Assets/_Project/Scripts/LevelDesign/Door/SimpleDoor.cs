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
    public bool slideToRight = true; // true = derecha, false = izquierda

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
    public AudioClip lockedSound;

    [Range(0f, 1f)] public float audioVolume = 1f;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onLockedRefused;

    public bool IsOpen => isOpen;

    bool isOpen;
    bool isMoving;

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
        // asegurar que solo un modo de movimiento esté activo (prioridad:
        // Rotate > SlideUp > SlideHorizontal > DoubleHorizontal)
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

    void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        closedPosition = transform.position;
        closedRotation = transform.rotation;

        openRotation = Quaternion.Euler(
            transform.eulerAngles + new Vector3(0f, openAngle, 0f)
        );

        if (useSlideUp)
        {
            openPosition = closedPosition + transform.up * openHeight;
        }

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
        // Key mode: while player is inside the trigger, listen for the interact button.
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

        // Lock jump for any player entering the trigger (Auto / Key).
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

        // Switch mode: trigger does nothing — caller invokes OpenDoor().
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

    void OnDisable()
    {
        ReleaseJumpLock();
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
    /// Generic open. Used by Auto, Switch, and after a successful key check.
    /// </summary>
    public void OpenDoor()
    {
        if (isOpen || isMoving) return;
        StartCoroutine(OpenRoutine());
    }

    /// <summary>
    /// Try to open this door using the given inventory. Returns true if it opened.
    /// </summary>
    public bool TryOpenWithKey(PlayerInventory inventory)
    {
        if (isOpen) return true;

        // 1) Direct ItemDefinition reference — preferred path.
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

            OpenDoor();
            return true;
        }

        // 2) No key required at all → just open.
        if (string.IsNullOrEmpty(requiredKeyId))
        {
            OpenDoor();
            return true;
        }

        // 3) Fallback: match by KeyItem.keyId string.
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

        OpenDoor();
        return true;
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

        // 🔥 ROTATE
        if (useRotate)
        {
            while (Quaternion.Angle(transform.rotation, openRotation) > 0.5f)
            {
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    openRotation,
                    Time.deltaTime * rotateSpeed
                );

                yield return null;
            }

            transform.rotation = openRotation;
        }

        // 🔥 SLIDE UP
        else if (useSlideUp)
        {
            while (Vector3.Distance(transform.position, openPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    openPosition,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = openPosition;
        }

        // 🔥 SLIDE HORIZONTAL
        else if (useSlideHorizontal)
        {
            while (Vector3.Distance(transform.position, openPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    openPosition,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = openPosition;
        }

        // 🔥 DOUBLE HORIZONTAL
        else if (useDoubleHorizontal)
        {
            bool leftDone, rightDone;

            do
            {
                leftDone  = true;
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

            if (leftPanel  != null) leftPanel.position  = leftPanelOpen;
            if (rightPanel != null) rightPanel.position = rightPanelOpen;
        }

        isOpen = true;
        isMoving = false;

        ShowPrompt(false);
        onOpened?.Invoke();
    }
}
