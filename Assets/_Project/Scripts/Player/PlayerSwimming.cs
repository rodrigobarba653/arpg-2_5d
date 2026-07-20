using UnityEngine;
using System.Collections;

public class PlayerSwimming : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor motor;
    public PlayerJump jump;
    public Animator animator;
    PlayerCombatController combat;

    [Header("Swim Settings")]
    public float swimSpeed = 2.5f;
    public float swimGravity = -2f;

    bool isSwimming;

    float originalSpeed;
    float originalGravity;

    void Start()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!jump) jump = GetComponent<PlayerJump>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        combat = GetComponent<PlayerCombatController>();

        originalSpeed = motor.moveSpeed;
        originalGravity = motor.gravity;
    }

    public void EnterWater()
    {
        if (isSwimming) return;

        // 🔥 1. CAMBIAR ESTADO PRIMERO
        isSwimming = true;

        // 🔥 2. cancelar combate
        if (combat != null)
            combat.CancelCombatImmediate();

        // 🔥 3. cancelar salto completamente
        if (jump != null)
            jump.ForceExitAirState();

        // 🔥 4. reset físico
        motor.SetVerticalVelocity(0f);

        // 🔥 5. aplicar movimiento
        motor.moveSpeed = swimSpeed;
        motor.gravity = swimGravity;

        // 🔥 6. animación inmediata
        if (animator)
        {
            animator.SetBool("isSwimming", true);
            animator.Update(0f);
        }

        // 🔥 7. fix transición
        StartCoroutine(ForceSwimTransition());
    }

    public void ExitWater()
    {
        if (!isSwimming) return;

        // 🔥 1. CAMBIAR ESTADO PRIMERO
        isSwimming = false;

        // 🔺 restaurar movimiento
        motor.moveSpeed = originalSpeed;
        motor.gravity = originalGravity;

        // 🔺 reset físico
        motor.SetVerticalVelocity(0f);

        // 🔺 animación
        if (animator)
        {
            animator.SetBool("isSwimming", false);
            animator.Update(0f);
        }

        // 🔺 fix transición
        StartCoroutine(ForceExitTransition());
    }

    IEnumerator ForceSwimTransition()
    {
        motor.LockMovement(true);
        yield return null;
        motor.LockMovement(false);
    }

    IEnumerator ForceExitTransition()
    {
        motor.LockMovement(true);
        yield return null;
        motor.LockMovement(false);
    }

    public bool IsSwimming()
    {
        return isSwimming;
    }
}