using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerClimbing : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor motor;
    public PlayerJump jump;
    public Animator animator;
    PlayerCombatController combat;


    [Header("Climb Settings")]
    public float climbSpeed = 2f;
    bool ignoreLadder;
    float ignoreTimer;
    public float ignoreDuration = 0.5f;

    bool isClimbing;
    Ladder currentLadder;

    float originalGravity;

    void Awake()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!jump) jump = GetComponent<PlayerJump>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        combat = GetComponent<PlayerCombatController>();

        originalGravity = motor.gravity;
    }

    void Update()
    {
        // 🔥 actualizar timer SIEMPRE
        if (ignoreLadder)
        {
            ignoreTimer -= Time.deltaTime;
            if (ignoreTimer <= 0f)
                ignoreLadder = false;
        }

        if (!isClimbing) return;

        motor.SetVerticalVelocity(0f);

        HandleClimbMovement();

        CheckTopExit();
    }

    // =========================
    // ENTER
    // =========================
    public void EnterClimb(Ladder ladder)
    {
        if (isClimbing || ignoreLadder) return;

        if (isClimbing) return;

        isClimbing = true;
        currentLadder = ladder;

        motor.SetVerticalVelocity(0f);

        // 🔥 cancelar sistemas
        combat?.CancelCombatImmediate();
        jump?.ForceExitAirState();

        // 🔥 bloquear gravedad
        motor.gravity = 0f;
        motor.SetVerticalVelocity(0f);

        // 🔥 bloquear movimiento normal
        motor.LockMovement(true);

        // 🔥 snap a la escalera (alinear XZ)
        Vector3 pos = transform.position;
        pos.x = ladder.transform.position.x;
        pos.z = ladder.transform.position.z;
        transform.position = pos;

        // 🔥 animación
        // 🔥 limpiar animaciones anteriores
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("Land");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Roll");

        // 🔥 entrar DIRECTO a la animación de escalar (nombre EXACTO del estado)
        animator.Play("ClimbIdle", 0, 0f);

        // 🔥 activar modo climb
        animator.SetBool("isClimbing", true);
        animator.SetFloat("ClimbSpeed", 0f);

        // 🔥 forzar actualización inmediata
        animator.Update(0f);
    }

    // =========================
    // EXIT
    // =========================
    public void ExitClimb()
    {
        if (!isClimbing) return;

        isClimbing = false;
        currentLadder = null;

        animator.SetFloat("ClimbSpeed", 0f);

        // 🔥 restaurar motor
        motor.gravity = originalGravity;
        motor.LockMovement(false);

        // 🔥 animación
        animator.SetBool("isClimbing", false);
    }

    // =========================
    // MOVIMIENTO
    // =========================
    void HandleClimbMovement()
    {
        Vector2 input = motor.GetRawInput();

        float vertical = input.y;

        // 🔥 FIX REAL (anti drift)
        if (vertical > 0.15f)
            vertical = 1f;
        else if (vertical < -0.15f)
            vertical = -1f;
        else
            vertical = 0f;

        Vector3 move = Vector3.up * vertical * climbSpeed;

        motor.GetCharacterController().Move(move * Time.deltaTime);

        animator.SetFloat("ClimbSpeed", vertical);
    }

    public bool IsClimbing()
    {
        return isClimbing;
    }

    void CheckTopExit()
    {
        if (currentLadder == null || currentLadder.topPoint == null)
            return;

        // si ya llegó arriba
        float threshold = 0.15f;

        if (transform.position.y >= currentLadder.topPoint.position.y - threshold)
        {
            ExitAtTop();
        }
    }

    void ExitAtTop()
    {
        ignoreLadder = true;
        ignoreTimer = ignoreDuration;

        Ladder ladderRef = currentLadder;

        if (ladderRef != null && ladderRef.ladderTrigger != null)
        {
            ladderRef.ladderTrigger.enabled = false;
        }

        ExitClimb();

        CharacterController cc = motor.GetCharacterController();

        // 🔥 1. pequeño lift vertical (clave)
        cc.Move(Vector3.up * 0.3f);

        // 🔥 2. luego avanzar
        cc.Move(transform.forward * 0.3f);

        currentLadder = null;

        StartCoroutine(ReenableLadder(ladderRef));
    }

    IEnumerator ReenableLadder(Ladder ladderRef)
    {
        yield return new WaitForSeconds(0.5f);

        if (ladderRef != null && ladderRef.ladderTrigger != null)
        {
            ladderRef.ladderTrigger.enabled = true;
        }
    }
}