using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drop this on a world GameObject (with a trigger Collider) to make it
/// pickupable. When the player walks into it, the assigned item is added
/// to their inventory and the pickup destroys itself.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id for THIS specific pickup in the world. Saved per slot " +
             "so the pickup doesn't come back after Load. Pick something stable " +
             "and unique per scene, e.g. 'town_chest_a', 'forest_potion_01'. " +
             "Empty = pickup is not persistent (always respawns).")]
    public string pickupId = "";

    [Header("Item")]
    public ItemDefinition item;

    [Min(1)]
    public int quantity = 1;

    [Header("Behavior")]
    [Tooltip("If true, requires the player to press the interact button while in range. " +
             "If false, picked up automatically on overlap.")]
    public bool requireInteract = false;

    [Tooltip("Optional: assign an InputAction. If empty, defaults to Enter/E on keyboard + X/A button on gamepad.")]
    public InputActionReference interactAction;

    [Tooltip("Destroy this GameObject after pickup.")]
    public bool destroyOnPickup = true;

    [Header("Prompt")]
    [Tooltip("Icon (child GameObject) shown while the player is in range. Only used when Require Interact = ON.")]
    public GameObject interactPrompt;

    [Header("FX")]
    [Tooltip("Optional fallback AudioSource used only if there's no AudioManager in the scene.")]
    public AudioSource sfx;

    public AudioClip pickupSound;

    [Range(0f, 1f)] public float pickupVolume = 1f;

    public GameObject pickupVfx;

    [Header("Player Behavior")]
    [Tooltip("If true, the player can't jump while inside this pickup's trigger. " +
             "Most useful when Require Interact = ON.")]
    public bool lockJumpInside = true;

    PlayerInventory playerInRange;
    PlayerJump playerJump;
    bool jumpLockApplied;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col && !col.isTrigger)
            Debug.LogWarning($"ItemPickup '{name}' collider is not a trigger. " +
                             "Pickup may not work as expected.", this);

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        // If this pickup was already collected in a previous play / before the
        // current load, hide it. The PickupRegistry is populated from SaveData
        // on Load and cleared on New Game.
        if (PickupRegistry.IsConsumed(pickupId))
            gameObject.SetActive(false);
    }

    void Update()
    {
        if (!requireInteract) return;
        if (playerInRange == null) return;

        if (InteractInput.WasPressedThisFrame(interactAction))
            DoPickup(playerInRange);
    }

    void OnTriggerEnter(Collider other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) return;

        if (requireInteract)
        {
            playerInRange = inv;
            playerJump = other.GetComponentInParent<PlayerJump>();

            ShowPrompt(true);
            ApplyJumpLock();
            return;
        }

        // Automatic pickup — grab immediately without locking anything.
        DoPickup(inv);
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

    void DoPickup(PlayerInventory inv)
    {
        if (item == null)
        {
            Debug.LogWarning($"ItemPickup '{name}' has no item assigned.", this);
            return;
        }

        inv.Add(item, quantity);

        // Persistent pickups remember they were consumed so they don't respawn
        // after Load. Empty id = not persistent.
        PickupRegistry.MarkConsumed(pickupId);

        AudioManager.PlaySfxOrFallback(pickupSound, transform.position, pickupVolume, sfx);

        if (pickupVfx)
            Instantiate(pickupVfx, transform.position, Quaternion.identity);

        ReleaseJumpLock();
        ShowPrompt(false);
        playerInRange = null;

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    void ApplyJumpLock()
    {
        if (!lockJumpInside) return;
        if (jumpLockApplied) return;
        if (playerJump == null) return;

        playerJump.AddExternalLock();
        jumpLockApplied = true;
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
}
