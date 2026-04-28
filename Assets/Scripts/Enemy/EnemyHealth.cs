using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    EnemyAI ai;
    EnemyCombatController combat;
    EnemyMotor motor;

    [Header("Health")]
    public int maxHealth = 30;
    public int currentHealth;

    [Header("Hit Stun")]
    public float hitStunTime = 0.08f;

    [Header("Default Knockback")]
    public float defaultKnockbackForce = 6f;
    public float defaultKnockbackTime = 0.25f;

    [Header("Per Hit Knockback")]
    [Tooltip("Index 0 = step 1, index 1 = step 2, index 2 = step 3")]
    public bool[] knockbackEnabledPerStep = new bool[] { false, false, true };

    [Tooltip("Index 0 = step 1, index 1 = step 2, index 2 = step 3")]
    public float[] knockbackForcePerStep = new float[] { 2f, 4f, 7f };

    [Tooltip("Index 0 = step 1, index 1 = step 2, index 2 = step 3")]
    public float[] knockbackTimePerStep = new float[] { 0.08f, 0.12f, 0.22f };

    SpriteRenderer sr;
    Color originalColor;
    Coroutine flashRoutine;

    void Awake()
    {
        currentHealth = maxHealth;

        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombatController>();
        motor = GetComponent<EnemyMotor>();

        sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
            originalColor = sr.color;
    }

    public void TakeDamage(int amount, Vector3 hitDir, int step)
    {
        hitDir.y = 0f;

        if (hitDir.sqrMagnitude < 0.0001f)
        {
            if (ai != null && ai.player != null)
            {
                hitDir = (transform.position - ai.player.position).normalized;
                hitDir.y = 0f;
            }
            else
            {
                hitDir = -transform.forward;
            }
        }

        if (ai != null && ai.isDefending)
        {
            Vector3 dirToPlayer = (ai.player.position - transform.position).normalized;
            dirToPlayer.y = 0f;

            float dot = Vector3.Dot(transform.forward, dirToPlayer);

            if (dot > 0.5f)
            {
                Debug.Log("BLOCK FRONT!");

                if (sr != null)
                {
                    if (flashRoutine != null)
                        StopCoroutine(flashRoutine);

                    flashRoutine = StartCoroutine(BlockFlash());
                }

                return;
            }
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (combat != null)
            combat.OnTakeDamage(hitDir);

        if (sr != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(FlashWhite());
        }

        if (motor != null)
        {
            // face the source of damage
            motor.FaceDirection(-hitDir);

            // small initial full stop
            motor.ApplyHitStun(hitStunTime);
        }

        ApplyStepKnockback(hitDir, step);

        if (currentHealth <= 0)
            Die();
    }

    void ApplyStepKnockback(Vector3 hitDir, int step)
    {
        if (motor == null)
            return;

        int index = step - 1;

        if (index < 0)
            return;

        bool enabled = GetStepBool(knockbackEnabledPerStep, index, false);
        if (!enabled)
            return;

        float force = GetStepFloat(knockbackForcePerStep, index, defaultKnockbackForce);
        float time = GetStepFloat(knockbackTimePerStep, index, defaultKnockbackTime);

        motor.DoKnockback(hitDir, force, time);
    }

    bool GetStepBool(bool[] arr, int index, bool fallback)
    {
        if (arr == null)
            return fallback;

        if (index < 0 || index >= arr.Length)
            return fallback;

        return arr[index];
    }

    float GetStepFloat(float[] arr, int index, float fallback)
    {
        if (arr == null)
            return fallback;

        if (index < 0 || index >= arr.Length)
            return fallback;

        return arr[index];
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

    IEnumerator FlashWhite()
    {
        if (sr == null) yield break;

        sr.color = new Color(2f, 2f, 2f, 1f);
        yield return new WaitForSeconds(0.12f);
        sr.color = originalColor;
        flashRoutine = null;
    }

    IEnumerator BlockFlash()
    {
        if (sr == null) yield break;

        sr.color = Color.gray;
        yield return new WaitForSeconds(0.05f);
        sr.color = originalColor;
        flashRoutine = null;
    }
}