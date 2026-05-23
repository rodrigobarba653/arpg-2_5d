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
/// This way the system works out of the box on PC and gamepad, and the user
/// can later switch to a proper action defined inside PlayerControls.inputactions
/// without changing any code.
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
            _default.AddBinding("<Gamepad>/buttonSouth"); // X on PS, A on Xbox
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
        if (overrideAction != null && overrideAction.action != null)
        {
            if (!overrideAction.action.enabled)
                overrideAction.action.Enable();

            return overrideAction.action.WasPressedThisFrame();
        }

        return GetOrCreateDefault().WasPressedThisFrame();
    }
}
