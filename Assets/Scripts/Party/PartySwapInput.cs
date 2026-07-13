using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens for the swap-character input (e.g. L2 on gamepad, Q on keyboard)
/// and calls Party.SwapNext() if the cooldown allows it. If the menu is open
/// or the game is paused, the input is ignored.
///
/// Place one of these on the PartyRoot (or anywhere persistent). Set the
/// InputActionReference in the Inspector — bind it to whatever buttons you
/// want via your input actions asset.
/// </summary>
public class PartySwapInput : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("InputAction asset reference. Bind L2 / Q / etc. inside the asset.")]
    public InputActionReference swapAction;

    [Header("Cooldown")]
    [Tooltip("Seconds between swaps. Applied to Party.SwapCooldown on Awake.")]
    public float cooldownSeconds = 1f;

    [Header("Behaviour")]
    [Tooltip("Block the swap while the menu is open.")]
    public bool blockWhileMenuOpen = true;

    [Tooltip("Block the swap while the game is paused (Time.timeScale = 0).")]
    public bool blockWhilePaused = true;

    [Header("State Restrictions")]
    [Tooltip("Only allow swap when grounded (no jumping / falling).")]
    public bool requireGrounded = true;

    [Tooltip("Seconds the active player must have been continuously grounded " +
             "before a swap is allowed. Prevents swapping the instant a fall " +
             "ends — which can leave the new active stuck in a fall animation.")]
    [Range(0f, 1f)]
    public float minGroundedDuration = 0.15f;

    [Tooltip("Block swap while rolling.")]
    public bool blockWhileRolling = true;

    [Tooltip("Block swap while attacking.")]
    public bool blockWhileAttacking = true;

    [Tooltip("Block swap while taking damage / knockback.")]
    public bool blockWhileTakingDamage = true;

    [Tooltip("Extra grace period after the player took damage during which swap " +
             "is blocked, in seconds. Prevents input-timing races where the swap " +
             "input arrives on the same frame as the hit and slips through before " +
             "isTakingDamage is set. Also swallows reflex L2 presses caused by " +
             "the shock of a hit or DualSense trigger jitter from rumble.")]
    [Range(0f, 2f)]
    public float postDamageGracePeriod = 0.5f;

    [Tooltip("Block swap while swimming.")]
    public bool blockWhileSwimming = true;

    [Header("Audio (optional)")]
    [Tooltip("Played when the swap succeeds.")]
    public AudioClip swapSound;

    [Tooltip("Played when the player tries to swap but it's on cooldown.")]
    public AudioClip cooldownDeniedSound;

    [Tooltip("Played when the player tries to swap but the active is in a blocked state " +
             "(jumping, rolling, attacking, taking damage, swimming).")]
    public AudioClip stateDeniedSound;

    [Tooltip("Played when the player tries to swap but there's only one member.")]
    public AudioClip noSecondMemberSound;

    [Range(0f, 1f)] public float volume = 1f;

    [Header("Debug")]
    public bool debugLog = true;

    void Awake()
    {
        Party.SwapCooldown = cooldownSeconds;
    }

    float groundedSince = -1f;

    void Update()
    {
        // Track how long the active player has been continuously grounded so
        // we can require a brief settle after landing before allowing swap.
        var active = Party.Active;
        bool grounded = false;
        if (active != null)
        {
            var jump = active.GetComponent<PlayerJump>();
            if (jump != null) grounded = jump.IsGrounded;
            else
            {
                var motor = active.GetComponent<PlayerMotor>();
                if (motor != null) grounded = motor.IsGrounded();
            }
        }

        if (grounded)
        {
            if (groundedSince < 0f) groundedSince = Time.unscaledTime;
        }
        else
        {
            groundedSince = -1f;
        }
    }

    void OnEnable()
    {
        if (swapAction != null && swapAction.action != null)
        {
            swapAction.action.performed += OnSwapPerformed;
            swapAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[PartySwapInput] No swapAction assigned. " +
                             "Drag an InputActionReference into the Inspector.", this);
        }
    }

    void OnDisable()
    {
        if (swapAction != null && swapAction.action != null)
        {
            swapAction.action.performed -= OnSwapPerformed;
            swapAction.action.Disable();
        }
    }

    void OnSwapPerformed(InputAction.CallbackContext ctx)
    {
        if (blockWhileMenuOpen && MenuManager.Instance != null && MenuManager.Instance.IsOpen)
            return;

        if (blockWhilePaused && GamePause.IsPaused)
            return;

        if (Party.MemberCount < 2)
        {
            if (debugLog) Debug.Log("[PartySwapInput] Only one party member — nothing to swap to.");
            if (noSecondMemberSound != null)
                AudioManager.Play2DOrFallback(noSecondMemberSound, volume);
            return;
        }

        if (!Party.CanSwap)
        {
            if (debugLog) Debug.Log($"[PartySwapInput] Swap on cooldown ({Party.SwapCooldownRemaining:0.00}s remaining).");
            if (cooldownDeniedSound != null)
                AudioManager.Play2DOrFallback(cooldownDeniedSound, volume);
            return;
        }

        // Verify the active player isn't in a state that should block the swap.
        string blockReason = GetStateBlockReason();
        if (blockReason != null)
        {
            if (debugLog) Debug.Log($"[PartySwapInput] Swap blocked — {blockReason}.");
            if (stateDeniedSound != null)
                AudioManager.Play2DOrFallback(stateDeniedSound, volume);
            return;
        }

        if (Party.SwapNext())
        {
            if (debugLog)
            {
                string newName = Party.Active != null ? Party.Active.name : "?";
                Debug.Log($"[PartySwapInput] Swapped to '{newName}'.");
            }
            if (swapSound != null)
                AudioManager.Play2DOrFallback(swapSound, volume);
        }
    }

    /// <summary>
    /// Check the active player's components and return a non-null reason if
    /// they are in a state that should block the swap (jumping, rolling,
    /// attacking, taking damage, swimming). Returns null if the swap is allowed.
    /// </summary>
    string GetStateBlockReason()
    {
        var active = Party.Active;
        if (active == null) return null;

        if (requireGrounded)
        {
            var jump = active.GetComponent<PlayerJump>();
            var cc   = active.GetComponent<CharacterController>();

            // Use the CharacterController's LIVE grounded state — the most
            // reliable signal. PlayerJump's cached value is a fallback for
            // when there's no CC.
            bool ccSaysGrounded   = cc != null && cc.enabled && cc.isGrounded;
            bool jumpSaysGrounded = jump != null && jump.IsGrounded;

            // If CC exists, trust it. Otherwise fall back to PlayerJump cache.
            bool grounded = cc != null ? ccSaysGrounded : jumpSaysGrounded;

            if (!grounded)
            {
                if (!ccSaysGrounded) return "in the air (CC not grounded)";
                return "in the air (jumping / falling)";
            }

            // Also require grounded for a minimum continuous duration so the
            // player doesn't swap on the same frame they land.
            if (minGroundedDuration > 0f)
            {
                if (groundedSince < 0f) return "just landed";
                float elapsed = Time.unscaledTime - groundedSince;
                if (elapsed < minGroundedDuration)
                    return $"just landed ({elapsed:0.00}/{minGroundedDuration:0.00}s)";
            }
        }

        if (blockWhileRolling)
        {
            var motor = active.GetComponent<PlayerMotor>();
            if (motor != null && motor.rollActive) return "rolling";
        }

        if (blockWhileAttacking)
        {
            var combat = active.GetComponent<PlayerCombatController>();
            if (combat != null && combat.IsAttacking()) return "attacking";
        }

        if (blockWhileTakingDamage)
        {
            var health = active.GetComponent<PlayerHealth>();
            if (health != null && health.isTakingDamage) return "taking damage";

            // Party-wide last-damage timestamp — set at the very top of
            // PlayerHealth.TakeDamage, before isTakingDamage flips. This
            // reliably blocks reflex L2 presses (or DualSense trigger jitter
            // from rumble) that fire on/around the same frame as the hit.
            if (postDamageGracePeriod > 0f)
            {
                float elapsed = Time.time - Party.LastActiveDamageTime;
                if (elapsed >= 0f && elapsed < postDamageGracePeriod)
                    return $"post-damage grace ({elapsed:0.00}/{postDamageGracePeriod:0.00}s)";
            }
        }

        if (blockWhileSwimming)
        {
            var swim = active.GetComponent<PlayerSwimming>();
            if (swim != null && swim.IsSwimming()) return "swimming";
        }

        return null;
    }
}
