using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    EnemyAI ai;
    EnemyCombatController combat;

    [Header("Health")]
    public int maxHealth = 30;
    public int currentHealth;

    [Header("Knockback")]
    public float knockbackForce = 6f;
    public float knockbackUp = 1.5f;


    SpriteRenderer sr;
    Color originalColor;
    Rigidbody rb;

    void Awake()
    {
        currentHealth = maxHealth;

        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombatController>();

        sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
            originalColor = sr.color;

        rb = GetComponent<Rigidbody>();
    }

    public void TakeDamage(int amount, Vector3 hitDir, int step)
    {
        if (ai != null && ai.isDefending)
        {
            Vector3 dirToPlayer = (ai.player.position - transform.position).normalized;
            dirToPlayer.y = 0f;

            float dot = Vector3.Dot(transform.forward, dirToPlayer);

            if (dot > 0.5f) // 👈 solo bloquea si viene de frente
            {
                Debug.Log("🛡️ BLOCK FRONT!");

                if (sr != null)
                    StartCoroutine(BlockFlash());

                return;
            }
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (combat != null)
            combat.OnTakeDamage();

        if (sr != null)
            StartCoroutine(FlashRed());

        if (step == 3)
        {
            DoKnockback(hitDir);
        }

        if (currentHealth <= 0)
            Die();
    }

    void DoKnockback(Vector3 dir)
    {
        EnemyMotor motor = GetComponent<EnemyMotor>();

        if (motor != null)
        {
            motor.DoKnockback(dir, knockbackForce, 0.25f);
        }
    }

    void Die()
    {
        StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }

    IEnumerator FlashRed()
    {
        sr.color = Color.red;
        yield return new WaitForSeconds(0.08f);
        sr.color = originalColor;
    }

    IEnumerator BlockFlash()
    {
        sr.color = Color.gray; // o azul
        yield return new WaitForSeconds(0.05f);
        sr.color = originalColor;
    }
}