using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens to GamePause and freezes the player while the game is paused:
/// disables PlayerInput, locks the motor, cancels any in-flight combat.
///
/// Place on the Player GameObject (same one that has PlayerMotor).
/// </summary>
public class PlayerPauseHandler : MonoBehaviour
{
    [Header("Refs (auto-resolved if empty)")]
    public PlayerInput playerInput;
    public PlayerMotor motor;
    public PlayerCombatController combat;

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!motor)       motor       = GetComponent<PlayerMotor>();
        if (!combat)      combat      = GetComponent<PlayerCombatController>();
    }

    void OnEnable()
    {
        GamePause.OnPauseChanged += HandlePauseChanged;
    }

    void OnDisable()
    {
        GamePause.OnPauseChanged -= HandlePauseChanged;
    }

    void HandlePauseChanged(bool paused)
    {
        if (paused)
            FreezePlayer();
        else
            UnfreezePlayer();
    }

    void FreezePlayer()
    {
        // Cancel any combat action so the player snaps cleanly out of attack/roll.
        if (combat != null)
            combat.CancelCombatImmediate();

        // Lock the motor (no movement input is applied to the controller).
        if (motor != null)
        {
            motor.LockMovement(true);
        }

        // Disable via gate so GameplayLock / party dormancy stay consistent.
        PlayerInputGate.Refresh(playerInput);
    }

    void UnfreezePlayer()
    {
        // Never blindly re-enable — GameplayLock, LoadingScreen→pause, or a
        // dormant party member may still own the disabled state.
        PlayerInputGate.Refresh(playerInput);

        if (motor != null)
            motor.LockMovement(false);
    }
}
