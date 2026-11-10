using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared "interact" input used by switches, doors, pickups, etc.
///
/// If a script provides its own InputActionReference (assigned in inspector),
/// that one is used. Otherwise the static fallback action is enabled with
/// sensible defaults:
///   Keyboard: Enter, E
///   Gamepad : ButtonSouth (X on PlayStation, A on Xbox)
///
/// KNOWN CONFLICT (Sprint 01 — binding migration deferred):
/// Jump in PlayerControls also binds Gamepad ButtonSouth. On gamepad, a single
/// South press can fire both Jump and Interact in the same frame. Do not change
/// those bindings in Sprint 01; preserve current feel. A formal Interact action
/// + remapping will be a later dedicated pass.
///
/// When <see cref="GameplayLock"/> holds the Interact channel, presses are ignored.
/// </summary>
public static class InteractInput
{
    static InputAction _default;

    public static InputAction GetOrCreateDefault()
    {
        if (_default == null)
        {
            _default = new InputAction("Interact", InputActionType.Button);
            _default.AddBinding("<Keyboard>/enter");
            _default.AddBinding("<Keyboard>/e");
            // Preserved intentionally — see class summary for South/Jump conflict.
            _default.AddBinding("<Gamepad>/buttonSouth");
            _default.Enable();
        }

        if (!_default.enabled)
            _default.Enable();

        return _default;
    }

    /// <summary>
    /// Returns true if the user pressed the interact button this frame, using
    /// either the assigned reference or the default fallback.
    /// </summary>
    public static bool WasPressedThisFrame(InputActionReference overrideAction)
    {
        if (GameplayLock.IsLocked(GameplayLockChannels.Interact))
            return false;

        if (overrideAction != null && overrideAction.action != null)
        {
            if (!overrideAction.action.enabled)
                overrideAction.action.Enable();

            return overrideAction.action.WasPressedThisFrame();
        }

        return GetOrCreateDefault().WasPressedThisFrame();
    }
}
