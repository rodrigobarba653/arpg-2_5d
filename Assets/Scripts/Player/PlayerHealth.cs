using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("State")]
    public bool isTakingDamage;

    Renderer rend;
    Material mat;
    Color originalColor;

    Animator animator;
    PlayerMotor motor;

    Coroutine damageRoutine;

    static readonly int TintColorID = Shader.PropertyToID("_TintColor");

    void Awake()
    {
        currentHealth = maxHealth;

        rend = GetComponentInChildren<Renderer>();
        animator = GetComponentInChildren<Animator>();
        motor = GetComponent<PlayerMotor>();

        if (rend != null)
        {
            mat = rend.material;

            if (mat.HasProperty(TintColorID))
                originalColor = mat.GetColor(TintColorID);
        }
    }

    // 🔥 DAÑO CON DIRECCIÓN
    public void TakeDamage(int amount, Vector3 hitDirection)
    {

        PlayerCombatController combat = GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.CancelCombatImmediate();

        if (currentHealth <= 0)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"[PlayerHealth] {name} took {amount} damage. Current Health = {currentHealth}", this);

        // =========================
        // 🎯 HIT BASED ON FACING (FIX REAL)
        // =========================
        if (animator != null && motor != null)
        {
            Vector2 facing = motor.GetFacing2D();

            if (facing.sqrMagnitude < 0.001f)
                facing = Vector2.down; // fallback

            facing.Normalize();

            float x = facing.x;
            float y = facing.y;

            // 🔥 decidir dirección dominante
            if (Mathf.Abs(y) > Mathf.Abs(x))
            {
                if (y > 0f)
                    animator.SetTrigger("HitUp");
                else
                    animator.SetTrigger("HitDown");
            }
            else
            {
                if (x > 0f)
                    animator.SetTrigger("HitRight");
                else
                    animator.SetTrigger("HitLeft");
            }
        }

        // =========================
        // 🧍 DAMAGE LOCK (FIX REAL)
        // =========================
        if (motor != null)
        {
            if (damageRoutine != null)
                StopCoroutine(damageRoutine);

            damageRoutine = StartCoroutine(DamageLockRoutine(0.3f));
        }

        // =========================
        // 🔴 FLASH DAMAGE
        // =========================
        if (mat != null)
            StartCoroutine(FlashRed());

        // =========================
        // 💀 DEATH
        // =========================
        if (currentHealth <= 0)
        {
            if (damageRoutine != null)
                StopCoroutine(damageRoutine);

            if (motor != null)
                motor.LockMovement(true);

            Die();
        }
    }

    IEnumerator DamageLockRoutine(float time)
    {
        isTakingDamage = true;

        motor.LockMovement(true);

        yield return new WaitForSeconds(time);

        motor.LockMovement(false);

        isTakingDamage = false;
    }

    IEnumerator FlashRed()
    {
        mat.SetColor(TintColorID, Color.red);

        yield return new WaitForSeconds(0.08f);

        mat.SetColor(TintColorID, originalColor);
    }

    void Die()
    {
        Debug.Log("Player Dead");
    }
}