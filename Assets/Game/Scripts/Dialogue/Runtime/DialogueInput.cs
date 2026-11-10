using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dialogue-owned input actions (survive PlayerInput disable under GameplayLock).
///
/// Confirm = Enter / E / Gamepad South (matches Interact feel).
/// Cancel  = Esc / Gamepad East (never Confirm).
/// Fast Forward = Right Shoulder / Left Shift only — NOT hold-Confirm
/// (hold-Confirm was auto-finishing the next line after advance).
/// </summary>
public static class DialogueInput
{
    static InputAction confirm;
    static InputAction cancel;
    static InputAction fastForward;

    /// <summary>Ignore Confirm until this frame (prevents open+advance on same press).</summary>
    static int suppressConfirmUntilFrame = -1;

    public static void SuppressConfirmForCurrentPress()
    {
        suppressConfirmUntilFrame = Time.frameCount + 1;
    }

    /// <summary>Call after advancing a line so a held E cannot instantly fill the next line.</summary>
    public static void SuppressConfirmUntilReleased()
    {
        Ensure();
        // Keep suppressing while held; PlayLine waits on ConfirmReleased.
        suppressConfirmUntilFrame = int.MaxValue;
    }

    public static bool ConfirmReleased()
    {
        Ensure();
        return !confirm.IsPressed();
    }

    public static void ClearConfirmSuppress()
    {
        suppressConfirmUntilFrame = -1;
    }

    public static bool ConfirmPressedThisFrame()
    {
        Ensure();
        if (Time.frameCount <= suppressConfirmUntilFrame)
            return false;
        if (suppressConfirmUntilFrame == int.MaxValue)
            return false;
        return confirm.WasPressedThisFrame();
    }

    public static bool CancelPressedThisFrame()
    {
        Ensure();
        return cancel.WasPressedThisFrame();
    }

    public static bool FastForwardHeld()
    {
        Ensure();
        // Dedicated binding only — do not use Confirm hold.
        return fastForward.IsPressed();
    }

    static void Ensure()
    {
        if (confirm != null)
        {
            if (!confirm.enabled) confirm.Enable();
            if (!cancel.enabled) cancel.Enable();
            if (!fastForward.enabled) fastForward.Enable();
            return;
        }

        confirm = new InputAction("DialogueConfirm", InputActionType.Button);
        confirm.AddBinding("<Keyboard>/enter");
        confirm.AddBinding("<Keyboard>/e");
        confirm.AddBinding("<Gamepad>/buttonSouth");
        confirm.Enable();

        cancel = new InputAction("DialogueCancel", InputActionType.Button);
        cancel.AddBinding("<Keyboard>/escape");
        cancel.AddBinding("<Gamepad>/buttonEast");
        cancel.Enable();

        fastForward = new InputAction("DialogueFastForward", InputActionType.Button);
        fastForward.AddBinding("<Gamepad>/rightShoulder");
        fastForward.AddBinding("<Keyboard>/leftShift");
        fastForward.Enable();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        confirm?.Dispose();
        cancel?.Dispose();
        fastForward?.Dispose();
        confirm = null;
        cancel = null;
        fastForward = null;
        suppressConfirmUntilFrame = -1;
    }
#endif
}
