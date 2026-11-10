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
    static InputAction choicePrev;
    static InputAction choiceNext;
    static InputAction choiceStick;

    /// <summary>Ignore Confirm until this frame (prevents open+advance on same press).</summary>
    static int suppressConfirmUntilFrame = -1;

    static int navDir;
    static float navHeld;
    static float navRepeatAt = 0.28f;
    static bool stickArmed = true;

    const float StickThreshold = 0.5f;
    const float NavInitialRepeat = 0.28f;
    const float NavRepeat = 0.18f;

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

    /// <summary>
    /// -1 previous, +1 next, 0 none. Keys and d-pad repeat after 0.28s, then every 0.18s.
    /// The left stick steps once per deflection and waits until it returns inside 0.5.
    /// Call only while a choice menu is open. Ordinary lines never poll this.
    /// </summary>
    public static int ChoiceNavigationThisFrame()
    {
        Ensure();

        Vector2 stick = choiceStick.ReadValue<Vector2>();
        if (Mathf.Abs(stick.y) < StickThreshold)
            stickArmed = true;
        else if (stickArmed)
        {
            stickArmed = false;
            navDir = 0;
            navHeld = 0f;
            navRepeatAt = NavInitialRepeat;
            return stick.y > 0f ? -1 : 1;
        }

        bool prevEdge = choicePrev.WasPressedThisFrame();
        bool nextEdge = choiceNext.WasPressedThisFrame();
        int dir = 0;
        bool edge = false;
        if (prevEdge)
        {
            dir = -1;
            edge = true;
        }
        else if (nextEdge)
        {
            dir = 1;
            edge = true;
        }
        else if (choicePrev.IsPressed())
        {
            dir = -1;
        }
        else if (choiceNext.IsPressed())
        {
            dir = 1;
        }

        if (dir == 0)
        {
            navDir = 0;
            navHeld = 0f;
            navRepeatAt = NavInitialRepeat;
            return 0;
        }

        if (edge || dir != navDir)
        {
            navDir = dir;
            navHeld = 0f;
            navRepeatAt = NavInitialRepeat;
            return dir;
        }

        navHeld += Time.unscaledDeltaTime;
        if (navHeld < navRepeatAt)
            return 0;

        navHeld = 0f;
        navRepeatAt = NavRepeat;
        return dir;
    }

    /// <summary>Forget a stick deflection so the next menu does not inherit it.</summary>
    public static void ResetChoiceNavigation()
    {
        navDir = 0;
        navHeld = 0f;
        navRepeatAt = NavInitialRepeat;
        stickArmed = true;
    }

    static void Ensure()
    {
        if (confirm != null)
        {
            if (!confirm.enabled) confirm.Enable();
            if (!cancel.enabled) cancel.Enable();
            if (!fastForward.enabled) fastForward.Enable();
            if (!choicePrev.enabled) choicePrev.Enable();
            if (!choiceNext.enabled) choiceNext.Enable();
            if (!choiceStick.enabled) choiceStick.Enable();
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

        choicePrev = new InputAction("DialogueChoicePrev", InputActionType.Button);
        choicePrev.AddBinding("<Keyboard>/w");
        choicePrev.AddBinding("<Keyboard>/upArrow");
        choicePrev.AddBinding("<Gamepad>/dpad/up");
        choicePrev.Enable();

        choiceNext = new InputAction("DialogueChoiceNext", InputActionType.Button);
        choiceNext.AddBinding("<Keyboard>/s");
        choiceNext.AddBinding("<Keyboard>/downArrow");
        choiceNext.AddBinding("<Gamepad>/dpad/down");
        choiceNext.Enable();

        choiceStick = new InputAction("DialogueChoiceStick", InputActionType.Value, expectedControlType: "Vector2");
        choiceStick.AddBinding("<Gamepad>/leftStick");
        choiceStick.Enable();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        confirm?.Dispose();
        cancel?.Dispose();
        fastForward?.Dispose();
        choicePrev?.Dispose();
        choiceNext?.Dispose();
        choiceStick?.Dispose();
        confirm = null;
        cancel = null;
        fastForward = null;
        choicePrev = null;
        choiceNext = null;
        choiceStick = null;
        suppressConfirmUntilFrame = -1;
        navDir = 0;
        navHeld = 0f;
        navRepeatAt = NavInitialRepeat;
        stickArmed = true;
    }
#endif
}
