using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens for the "cast magic" button (Triangle on gamepad, configurable key
/// on keyboard) and calls Cast() on the active party member's PlayerMagic.
///
/// Place ONE of these on a persistent GameObject (e.g. PartyRoot). Bind an
/// InputActionReference in the Inspector — usually bound to
/// <c>&lt;Gamepad&gt;/buttonNorth</c> (Triangle / Y) and a keyboard key.
/// </summary>
public class MagicCastInput : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("InputActionReference for the cast button. Bind Triangle (buttonNorth) " +
             "and a keyboard key inside the action asset.")]
    public InputActionReference castAction;

    [Header("Behaviour")]
    [Tooltip("Block cast while the menu is open.")]
    public bool blockWhileMenuOpen = true;

    [Tooltip("Block cast while the game is paused.")]
    public bool blockWhilePaused = true;

    [Header("Debug")]
    public bool debugLog = true;

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
    }

    void OnDisable()
    {
        if (castAction != null && castAction.action != null)
        {
            castAction.action.performed -= OnCastPerformed;
        }
    }

    void OnCastPerformed(InputAction.CallbackContext ctx)
    {
        if (blockWhileMenuOpen && MenuManager.Instance != null && MenuManager.Instance.IsOpen)
            return;

        if (blockWhilePaused && GamePause.IsPaused)
            return;

        var active = Party.Active;
        if (active == null) return;

        var magic = active.GetComponent<PlayerMagic>();
        if (magic == null)
        {
            if (debugLog) Debug.Log($"[MagicCastInput] Active '{active.name}' has no PlayerMagic component.");
            return;
        }

        bool ok = magic.Cast();
        if (debugLog && ok)
            Debug.Log($"[MagicCastInput] '{active.name}' cast '{magic.equipped?.displayName}'.");
    }
}
