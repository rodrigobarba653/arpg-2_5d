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

        // Disable input so Input System callbacks stop firing entirely.
        if (playerInput != null)
            playerInput.enabled = false;
    }

    void UnfreezePlayer()
    {
        if (playerInput != null)
            playerInput.enabled = true;

        if (motor != null)
            motor.LockMovement(false);
    }
}
