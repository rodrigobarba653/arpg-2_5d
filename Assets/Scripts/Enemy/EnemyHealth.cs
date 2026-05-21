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

    [Header("Flash")]
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private float flashIntensity = 4f;

    SpriteRenderer[] srs;
    Coroutine flashRoutine;

    void Awake()
    {
        currentHealth = maxHealth;

        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombatController>();
        motor = GetComponent<EnemyMotor>();

        // 🔥 get ALL renderers (important)
        srs = GetComponentsInChildren<SpriteRenderer>();
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

        // 🛡️ BLOCK
        if (ai != null && ai.isDefending)
        {
            Vector3 dirToPlayer = (ai.player.position - transform.position).normalized;
            dirToPlayer.y = 0f;

            float dot = Vector3.Dot(transform.forward, dirToPlayer);

            if (dot > 0.5f)
            {
                if (flashRoutine != null)
                    StopCoroutine(flashRoutine);

                flashRoutine = StartCoroutine(BlockFlash());
                return;
            }
        }

        // 💔 DAMAGE
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (combat != null)
            combat.OnTakeDamage(hitDir);

        // 💥 FLASH (NEW SYSTEM)
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashWhite());

        // 🎯 HIT REACTION
        if (motor != null)
        {
            motor.FaceDirection(-hitDir);
            motor.ApplyHitStun(hitStunTime);
        }

        ApplyStepKnockback(hitDir, step);

        if (currentHealth <= 0)
            Die();
    }

    void ApplyStepKnockback(Vector3 hitDir, int step)
    {
        if (motor == null) return;

        int index = step - 1;
        if (index < 0) return;

        bool enabled = GetStepBool(knockbackEnabledPerStep, index, false);
        if (!enabled) return;

        float force = GetStepFloat(knockbackForcePerStep, index, defaultKnockbackForce);
        float time = GetStepFloat(knockbackTimePerStep, index, defaultKnockbackTime);

        motor.DoKnockback(hitDir, force, time);
    }

    bool GetStepBool(bool[] arr, int index, bool fallback)
    {
        if (arr == null) return fallback;
        if (index < 0 || index >= arr.Length) return fallback;
        return arr[index];
    }

    float GetStepFloat(float[] arr, int index, float fallback)
    {
        if (arr == null) return fallback;
        if (index < 0 || index >= arr.Length) return fallback;
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

    // 🔥 WHITE FLASH (uses Shader Graph _FlashColor)
    IEnumerator FlashWhite()
    {
        if (srs == null || srs.Length == 0) yield break;

        foreach (var r in srs)
        {
            if (r == null) continue;

            var mat = r.material;

            if (mat.HasProperty("_FlashColor"))
                mat.SetColor("_FlashColor", Color.white * flashIntensity);
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (var r in srs)
        {
            if (r == null) continue;

            var mat = r.material;

            if (mat.HasProperty("_FlashColor"))
                mat.SetColor("_FlashColor", Color.black);
        }

        flashRoutine = null;
    }

    // 🛡️ BLOCK FLASH
    IEnumerator BlockFlash()
    {
        if (srs == null || srs.Length == 0) yield break;

        foreach (var r in srs)
        {
            if (r == null) continue;

            var mat = r.material;

            if (mat.HasProperty("_FlashColor"))
                mat.SetColor("_FlashColor", Color.gray * 2f);
        }

        yield return new WaitForSeconds(0.05f);

        foreach (var r in srs)
        {
            if (r == null) continue;

            var mat = r.material;

            if (mat.HasProperty("_FlashColor"))
                mat.SetColor("_FlashColor", Color.black);
        }

        flashRoutine = null;
    }
}