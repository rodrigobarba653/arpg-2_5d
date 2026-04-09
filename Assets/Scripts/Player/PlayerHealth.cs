using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("State")]
    public bool isTakingDamage;

    [Header("Damage Lock")]
    [SerializeField] private float damageLockTime = 0.3f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("Flicker")]
    [SerializeField] private float flickerDuration = 0.3f;
    [SerializeField] private float flickerInterval = 0.05f;

    [Header("Camera Shake")]
    [SerializeField] private CameraShakeCinemachine cameraShake;
    [SerializeField] private float hitShakeDuration = 0.10f;
    [SerializeField] private float hitShakeMagnitude = 0.06f;

    Renderer rend;
    Material mat;
    Color originalColor;

    SpriteRenderer[] spriteRenderers;

    Animator animator;
    PlayerMotor motor;

    Coroutine damageRoutine;
    Coroutine flickerRoutine;

    static readonly int TintColorID = Shader.PropertyToID("_TintColor");

    void Awake()
    {
        currentHealth = maxHealth;

        rend = GetComponentInChildren<Renderer>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        animator = GetComponentInChildren<Animator>();
        motor = GetComponent<PlayerMotor>();

        if (cameraShake == null)
            cameraShake = FindFirstObjectByType<CameraShakeCinemachine>();

        if (rend != null)
        {
            mat = rend.material;

            if (mat.HasProperty(TintColorID))
                originalColor = mat.GetColor(TintColorID);
        }
    }

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
        // 📸 CAMERA SHAKE
        // =========================
        if (cameraShake != null)
            cameraShake.Shake(hitShakeDuration, hitShakeMagnitude);

        // =========================
        // 🎯 HIT ANIMATION (FACE ATTACKER)
        // =========================
        if (animator != null && motor != null)
        {
            Vector3 flatHitDir = hitDirection;
            flatHitDir.y = 0f;

            if (flatHitDir.sqrMagnitude < 0.001f)
                flatHitDir = transform.forward;

            flatHitDir.Normalize();

            Vector2 hitDir2D = new Vector2(flatHitDir.x, flatHitDir.z);

            motor.LockFacing(hitDir2D);

            animator.SetFloat("HitX", hitDir2D.x);
            animator.SetFloat("HitY", hitDir2D.y);

            animator.ResetTrigger("Hit");
            animator.SetTrigger("Hit");
        }

        // =========================
        // 💥 KNOCKBACK (OPPOSITE DIRECTION)
        // =========================
        if (motor != null)
        {
            Vector3 knockbackDir = hitDirection;
            knockbackDir.y = 0f;

            if (knockbackDir.sqrMagnitude < 0.001f)
                knockbackDir = -transform.forward;

            motor.BeginKnockback(knockbackDir, knockbackForce, knockbackDuration);
        }

        // =========================
        // 🧍 DAMAGE LOCK
        // =========================
        if (motor != null)
        {
            if (damageRoutine != null)
                StopCoroutine(damageRoutine);

            damageRoutine = StartCoroutine(DamageLockRoutine(damageLockTime));
        }

        // =========================
        // ✨ FLICKER
        // =========================
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            if (flickerRoutine != null)
                StopCoroutine(flickerRoutine);

            SetRenderersVisible(true);
            flickerRoutine = StartCoroutine(FlickerRoutine());
        }

        // =========================
        // 💀 DEATH
        // =========================
        if (currentHealth <= 0)
        {
            if (damageRoutine != null)
                StopCoroutine(damageRoutine);

            if (flickerRoutine != null)
                StopCoroutine(flickerRoutine);

            SetRenderersVisible(true);

            if (motor != null)
            {
                motor.CancelKnockback();
                motor.LockMovement(true);
            }

            Die();
        }
    }

    IEnumerator FlickerRoutine()
    {
        float timer = 0f;
        bool visible = true;

        while (timer < flickerDuration)
        {
            visible = !visible;
            SetRenderersVisible(visible);

            yield return new WaitForSeconds(flickerInterval);
            timer += flickerInterval;
        }

        SetRenderersVisible(true);
        flickerRoutine = null;
    }

    void SetRenderersVisible(bool visible)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            spriteRenderers[i].enabled = visible;
        }
    }

    IEnumerator DamageLockRoutine(float time)
    {
        isTakingDamage = true;

        if (motor != null)
            motor.LockMovement(true);

        yield return new WaitForSeconds(time);

        if (motor != null)
        {
            motor.LockMovement(false);
            motor.UnlockFacing();
        }

        isTakingDamage = false;
    }

    IEnumerator FlashRed()
    {
        if (mat != null)
        {
            mat.SetColor(TintColorID, Color.red);

            yield return new WaitForSeconds(0.08f);

            mat.SetColor(TintColorID, originalColor);
        }
    }

    void Die()
    {
        Debug.Log("Player Dead");
    }
}