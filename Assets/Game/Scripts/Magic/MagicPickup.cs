using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drop this on a world GameObject (with a trigger Collider) to make it a
/// "spell shrine": when the player interacts with it, the equipped active
/// party member learns the associated <see cref="magic"/> — but only if the
/// spell's compatibleCharacters allow it.
///
/// After being consumed, the GameObject deactivates and its <see cref="pickupId"/>
/// is stored in PickupRegistry so it doesn't reappear on Load.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MagicPickup : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id for this shrine so it stays consumed across saves. " +
             "Empty = not persistent (respawns every load).")]
    public string pickupId = "";

    [Header("Magic")]
    [Tooltip("Which SpellItem to add to the shared party inventory when picked " +
             "up. Assign an asset created via Create → ARPG → Items → Spell.")]
    public SpellItem spell;

    [Header("Behaviour")]
    [Tooltip("If true, requires the player to press the interact button while " +
             "in range. If false, learns automatically on overlap.")]
    public bool requireInteract = true;

    [Tooltip("InputAction for the interact key. If empty, defaults to E on " +
             "keyboard + South button on gamepad.")]
    public InputActionReference interactAction;

    [Header("Prompt")]
    [Tooltip("Optional child GameObject (e.g. an 'E' icon) shown while the " +
             "player is in range and Require Interact is ON.")]
    public GameObject interactPrompt;

    [Header("FX (optional)")]
    public AudioClip learnSound;
    public AudioClip incompatibleSound;
    public GameObject learnVfxPrefab;

    [Range(0f, 1f)] public float volume = 1f;

    [Header("Debug")]
    public bool debugLog = true;

    PlayerMagic playerInRange;
    PlayerJump lockedJump;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col && !col.isTrigger)
            Debug.LogWarning($"[MagicPickup] '{name}' collider is not a trigger.", this);

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        // If already consumed in a previous session, hide immediately.
        if (PickupRegistry.IsConsumed(pickupId))
            gameObject.SetActive(false);
    }

    void Update()
    {
        if (!requireInteract) return;
        if (playerInRange == null) return;

        if (InteractInput.WasPressedThisFrame(interactAction))
            TryLearn(playerInRange);
    }

    void OnTriggerEnter(Collider other)
    {
        var pm = other.GetComponentInParent<PlayerMagic>();
        if (pm == null) return;

        if (requireInteract)
        {
            playerInRange = pm;
            LockJump(pm);
            if (interactPrompt != null) interactPrompt.SetActive(true);
            return;
        }

        // Automatic: try to learn immediately.
        TryLearn(pm);
    }

    void OnTriggerExit(Collider other)
    {
        var pm = other.GetComponentInParent<PlayerMagic>();
        if (pm != null && pm == playerInRange)
        {
            playerInRange = null;
            UnlockJump();
            if (interactPrompt != null) interactPrompt.SetActive(false);
        }
    }

    void OnDisable()
    {
        // Defensive: if the pickup gets disabled or destroyed while the player
        // is still in range, don't leave the jump lock hanging.
        UnlockJump();
        if (interactPrompt != null) interactPrompt.SetActive(false);
        playerInRange = null;
    }

    void LockJump(PlayerMagic pm)
    {
        if (lockedJump != null) return; // already locked from this pickup
        var jump = pm.GetComponent<PlayerJump>();
        if (jump != null)
        {
            jump.AddExternalLock();
            lockedJump = jump;
        }
    }

    void UnlockJump()
    {
        if (lockedJump == null) return;
        lockedJump.RemoveExternalLock();
        lockedJump = null;
    }

    void TryLearn(PlayerMagic pm)
    {
        if (spell == null)
        {
            Debug.LogWarning($"[MagicPickup] '{name}' has no SpellItem assigned.", this);
            return;
        }

        // Add to the SHARED party inventory. Any character can pick up any
        // spell — compatibility is only enforced when equipping.
        var inv = pm.GetComponent<PlayerInventory>();
        if (inv == null)
        {
            Debug.LogWarning($"[MagicPickup] '{pm.name}' has no PlayerInventory.", this);
            return;
        }

        // Unique — one per party. If the party already has it, do nothing (no
        // duplicate stacks are allowed for SpellItems).
        if (inv.HasItem(spell))
        {
            if (debugLog)
                Debug.Log($"[MagicPickup] Party already has '{spell.displayName}'.");
            if (incompatibleSound != null)
                AudioManager.PlaySfxOrFallback(incompatibleSound, transform.position, volume);
            return;
        }

        inv.Add(spell, 1);

        // Auto-equip on the picker if they are compatible + have a free slot.
        pm.TryAutoEquip(spell);

        // Success.
        if (debugLog)
            Debug.Log($"[MagicPickup] '{pm.name}' picked up '{spell.displayName}'.", this);

        if (learnSound != null)
            AudioManager.PlaySfxOrFallback(learnSound, transform.position, volume);

        if (learnVfxPrefab != null)
            Instantiate(learnVfxPrefab, transform.position, Quaternion.identity);

        // Persist consumption + deactivate.
        PickupRegistry.MarkConsumed(pickupId);

        if (interactPrompt != null) interactPrompt.SetActive(false);
        playerInRange = null;

        gameObject.SetActive(false);
    }
}
