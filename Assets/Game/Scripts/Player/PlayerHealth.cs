using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    // ============================================================
    // STATIC REGISTRY — every PlayerHealth in the scene registers here so
    // the menu / UI can enumerate all party members without a separate manager.
    // ============================================================
    static readonly List<PlayerHealth> all = new List<PlayerHealth>();
    public static IReadOnlyList<PlayerHealth> All => all;

    [Header("Identity")]
    [Tooltip("Character template (portrait, name, class, element). Identity only — stats live below.")]
    public CharacterDefinition character;

    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Header("Mana")]
    public int maxMana = 100;
    public int currentMana = 100;

    [Header("AP")]
    public int maxAP = 8;
    public int currentAP = 2;

    [Header("Progression")]
    [Tooltip("Current level. Use Heal()/Damage()/SpendMP() etc. and LevelUp() to grow.")]
    public int level = 1;

    /// <summary>Fired whenever any stat changes. UI binds to this for live updates.</summary>
    public event Action OnStatsChanged;

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

    // Polled in Update — when reached, isTakingDamage is forced off. This is a
    // safety net so the player never gets stuck unable to attack / roll if the
    // DamageLockRoutine dies (scene transition, exception, etc.).
    float damageLockEndTime = -1f;

    /// <summary>Time.time at which the current damage lock ends. -1 if not
    /// currently locked. Read by PartySwapInput to enforce a grace period
    /// where swap is denied around the moment of a hit.</summary>
    public float DamageLockEndTime => damageLockEndTime;

    static readonly int TintColorID = Shader.PropertyToID("_TintColor");

    void Awake()
    {
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

    void OnEnable()
    {
        if (!all.Contains(this))
        {
            all.Add(this);
            Party.RaisePartyChanged();
        }

        // If we were disabled mid-damage (e.g. SceneTeleporter froze us during
        // a knockback), reset state so the player can attack / roll again.
        isTakingDamage = false;
        damageLockEndTime = -1f;
        damageRoutine = null;

        if (motor != null)
        {
            motor.LockMovement(false);
            motor.UnlockFacing();
        }
    }

    void OnDisable()
    {
        if (all.Remove(this))
            Party.RaisePartyChanged();

        // Stop any in-flight routines explicitly so they don't leak state.
        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }
        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }
    }

    void Update()
    {
        // Failsafe-style damage lock release. Polling Time.time is more robust
        // than a coroutine: if anything kills the coroutine mid-flight (scene
        // transition, exception, second hit timing edge case), the player would
        // be stuck unable to attack or roll. Polling guarantees release.
        if (isTakingDamage && Time.time >= damageLockEndTime)
        {
            isTakingDamage = false;
            damageLockEndTime = -1f;

            if (motor != null)
            {
                motor.LockMovement(false);
                motor.UnlockFacing();
            }
        }
    }

    public void NotifyStatsChanged() => OnStatsChanged?.Invoke();

    public void TakeDamage(int amount, Vector3 hitDirection)
    {
        PlayerCombatController combat = GetComponent<PlayerCombatController>();
        if (combat != null) combat.CancelCombatImmediate();

        if (currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnStatsChanged?.Invoke();

        // Stamp the party-wide last-damage timestamp BEFORE isTakingDamage is
        // set below. PartySwapInput uses this to block reflex swap presses that
        // arrive on the same frame as the hit but before isTakingDamage flips.
        if (Party.Active == this)
            Party.NotifyActiveTookDamage();

        Debug.Log($"[PlayerHealth] {name} took {amount} damage. HP = {currentHealth}/{maxHealth}", this);

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
        isTakingDamage = true;
        damageLockEndTime = Time.time + damageLockTime;

        if (motor != null)
            motor.LockMovement(true);

        // =========================
        // ✨ FLICKER
        // =========================
        // Only run the visual flicker on the ACTIVE party member. On a dormant
        // party member (hidden by PartyMemberControl), toggling the renderers
        // to visible here would briefly reveal them. Defensive — MeleeHitbox
        // already filters out dormant targets, but any other code path that
        // ends up calling TakeDamage on a dormant player would still trip this.
        bool isActiveMember = Party.Active == this;

        if (isActiveMember && spriteRenderers != null && spriteRenderers.Length > 0)
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

            if (isActiveMember)
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


    IEnumerator FlashRed()
    {
        if (mat != null)
        {
            mat.SetColor(TintColorID, Color.red);

            yield return new WaitForSeconds(0.08f);

            mat.SetColor(TintColorID, originalColor);
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnStatsChanged?.Invoke();
    }

    public void SpendMP(int amount)
    {
        if (amount <= 0) return;
        currentMana = Mathf.Max(0, currentMana - amount);
        OnStatsChanged?.Invoke();
    }

    public void RestoreMP(int amount)
    {
        if (amount <= 0) return;
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        OnStatsChanged?.Invoke();
    }

    public void SpendAP(int amount)
    {
        if (amount <= 0) return;
        currentAP = Mathf.Max(0, currentAP - amount);
        OnStatsChanged?.Invoke();
    }

    public void RestoreAP(int amount)
    {
        if (amount <= 0) return;
        currentAP = Mathf.Min(maxAP, currentAP + amount);
        OnStatsChanged?.Invoke();
    }

    void Die()
    {
        Debug.Log("Player Dead");
    }
}