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

    [Header("Hurt SFX")]
    [Tooltip("Grunt / pain clips. One picked at random per damage taken. If empty, no sound is played.")]
    [SerializeField] private AudioClip[] hurtSounds;

    [Tooltip("Optional: clip played when the enemy dies. Falls back to hurtSounds if empty.")]
    [SerializeField] private AudioClip deathSound;

    [Range(0f, 1f)]
    [SerializeField] private float hurtVolume = 1f;

    [Tooltip("Random pitch variation range (e.g. 0.1 → pitch * 0.9..1.1). Set 0 to disable.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float hurtPitchVariation = 0.1f;

    [Header("Death")]
    [Tooltip("How long the hit animation plays before freezing on the current frame.")]
    [SerializeField] private float deathHurtPlayTime = 0.4f;

    [Tooltip("Duration of the alpha fade-out before the GameObject is destroyed.")]
    [SerializeField] private float deathFadeTime = 0.8f;

    SpriteRenderer[] srs;
    Coroutine flashRoutine;

    bool isDead;
    public bool IsDead => isDead;

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
        if (isDead) return;

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

        var ranged = GetComponent<EnemyRangedCombatController>();
        if (ranged != null)
            ranged.OnTakeDamage(hitDir);

        // 💥 FLASH (NEW SYSTEM)
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashWhite());

        // 🔊 GRUNT
        PlayHurtSfx();

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
        if (isDead) return;
        isDead = true;

        PlayDeathSfx();
        StartCoroutine(DieRoutine());
    }

    void PlayHurtSfx()
    {
        if (AudioManager.Instance == null) return;
        if (hurtSounds == null || hurtSounds.Length == 0) return;

        var clip = hurtSounds[Random.Range(0, hurtSounds.Length)];
        if (clip == null) return;

        var src = AudioManager.Instance.PlaySFXAt(clip, transform.position, hurtVolume);

        if (src != null && hurtPitchVariation > 0f)
            src.pitch = 1f + Random.Range(-hurtPitchVariation, hurtPitchVariation);
    }

    void PlayDeathSfx()
    {
        if (AudioManager.Instance == null) return;

        AudioClip clip = deathSound;
        if (clip == null && hurtSounds != null && hurtSounds.Length > 0)
            clip = hurtSounds[Random.Range(0, hurtSounds.Length)];

        if (clip == null) return;

        var src = AudioManager.Instance.PlaySFXAt(clip, transform.position, hurtVolume);

        if (src != null && hurtPitchVariation > 0f)
            src.pitch = 1f + Random.Range(-hurtPitchVariation, hurtPitchVariation);
    }

    IEnumerator DieRoutine()
    {
        var animator = GetComponentInChildren<Animator>();

        // Force hurt animation and clear conflicting params so nothing transitions out.
        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetBool("IsAttacking", false);
            animator.SetBool("isDefending", false);
            animator.ResetTrigger("Hurt");
            animator.SetTrigger("Hurt");
        }

        // Stop AI + combat so the enemy can't take any further action.
        if (ai != null) ai.enabled = false;

        if (combat != null)
        {
            EnemyAttackScheduler.ReleaseIfExists(combat);
            combat.enabled = false;
        }

        var ranged = GetComponent<EnemyRangedCombatController>();
        if (ranged != null)
        {
            EnemyAttackScheduler.ReleaseIfExists(ranged);
            ranged.enabled = false;
        }

        // Stop motor entirely.
        if (motor != null)
        {
            motor.Stop();
            motor.enabled = false;
        }

        // Remove from push-apart system so the corpse doesn't shove anything.
        var push = GetComponent<CharacterPushApart>();
        if (push != null) push.enabled = false;

        // Disable colliders so hitboxes don't keep hitting the corpse and so
        // other characters can walk through.
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
            if (c != null) c.enabled = false;

        // Stop any active flash so it doesn't fight the fade.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        // Let the hit animation play a moment, then freeze on its current frame.
        if (deathHurtPlayTime > 0f)
            yield return new WaitForSeconds(deathHurtPlayTime);

        if (animator != null) animator.speed = 0f;

        // Fade alpha to 0.
        float dur = Mathf.Max(0.01f, deathFadeTime);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - t / dur);

            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] == null) continue;
                var col = srs[i].color;
                col.a = alpha;
                srs[i].color = col;
            }

            yield return null;
        }

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