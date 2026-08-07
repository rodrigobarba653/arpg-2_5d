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
    bool hasIsSwimmingParam;

    float originalSpeed;
    float originalGravity;

    static readonly int IsSwimmingHash = Animator.StringToHash("isSwimming");

    PlayerEquipment equipment;

    void Start()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!jump) jump = GetComponent<PlayerJump>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        combat = GetComponent<PlayerCombatController>();
        equipment = GetComponent<PlayerEquipment>();

        originalSpeed = motor.moveSpeed;
        originalGravity = motor.gravity;

        CacheAnimatorParameter();

        // PlayerEquipment swaps the runtime animator controller when a weapon
        // is equipped — re-check the swim parameter when that happens.
        if (equipment != null)
            equipment.OnWeaponChanged += _ => CacheAnimatorParameter();
    }

    void CacheAnimatorParameter()
    {
        hasIsSwimmingParam = false;
        if (animator == null)
        {
            Debug.LogWarning("[PlayerSwimming] No Animator found — swim animation will not play.", this);
            return;
        }

        var parms = animator.parameters;
        for (int i = 0; i < parms.Length; i++)
        {
            if (parms[i].nameHash == IsSwimmingHash)
            {
                hasIsSwimmingParam = true;
                break;
            }
        }

        if (!hasIsSwimmingParam)
        {
            string ctrlName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "NULL";
            Debug.LogWarning($"[PlayerSwimming] Animator on '{animator.name}' is missing " +
                             "'isSwimming' (bool). Add it in the Animator Controller's " +
                             "Parameters tab so the swim state can activate. " +
                             $"(Currently equipped controller: {ctrlName})", this);
        }
    }

    public void EnterWater()
    {
        if (isSwimming) return;

        Debug.Log("[PlayerSwimming] EnterWater()", this);

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

        // 🔥 6. animación inmediata (solo si el parámetro existe)
        if (animator && hasIsSwimmingParam)
        {
            animator.SetBool(IsSwimmingHash, true);
            animator.Update(0f);
        }

        // 🔥 7. fix transición
        StartCoroutine(ForceSwimTransition());
    }

    public void ExitWater()
    {
        if (!isSwimming) return;

        Debug.Log("[PlayerSwimming] ExitWater()", this);

        // 🔥 1. CAMBIAR ESTADO PRIMERO
        isSwimming = false;

        // 🔺 restaurar movimiento
        motor.moveSpeed = originalSpeed;
        motor.gravity = originalGravity;

        // 🔺 reset físico
        motor.SetVerticalVelocity(0f);

        // 🔺 animación (solo si el parámetro existe)
        if (animator && hasIsSwimmingParam)
        {
            animator.SetBool(IsSwimmingHash, false);
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