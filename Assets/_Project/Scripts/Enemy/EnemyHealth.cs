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

    [Header("Damage Reaction")]
    [Tooltip("If OFF, the enemy IGNORES hit-flinch: no Hurt animation, no " +
             "knockback, no hit-stun, and the current attack is NOT cancelled. " +
             "Damage still applies. Use this for hyperarmor bosses or enemies " +
             "whose attacks can't be interrupted by hitting them.")]
    public bool canBeInterrupted = true;

    [Header("Hit Stun")]
    public float hitStunTime = 0.08f;

    [Header("Default Knockback")]
    public float defaultKnockbackForce = 6f;
    public float defaultKnockbackTime = 0.25f;

    [Header("Per Hit Knockback")]
    public bool[] knockbackEnabledPerStep = new bool[] { false, false, true };
    public float[] knockbackForcePerStep = new float[] { 2f, 4f, 7f };
    public float[] knockbackTimePerStep = new float[] { 0.08f, 0.12f, 0.22f };

    [Header("Flash")]
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private float flashIntensity = 4f;

    [Header("Varium Reward")]
    [Tooltip("Minimum varium awarded to the player on death.")]
    [SerializeField] private int variumMin = 5;

    [Tooltip("Maximum varium awarded (inclusive). Set equal to min for a fixed amount.")]
    [SerializeField] private int variumMax = 15;

    [Header("Item Drops")]
    [Tooltip("Items the enemy may drop on death. Each entry is rolled independently.")]
    [SerializeField] private System.Collections.Generic.List<ItemDropEntry> itemDrops = new System.Collections.Generic.List<ItemDropEntry>();

    [System.Serializable]
    public class ItemDropEntry
    {
        public ItemDefinition item;

        [Range(0f, 1f)]
        [Tooltip("0 = never, 1 = always. Rolled per entry.")]
        public float chance = 0.1f;

        [Min(1)]
        public int quantity = 1;
    }

    [Header("Hurt SFX")]
    [SerializeField] private AudioClip[] hurtSounds;
    [SerializeField] private AudioClip deathSound;

    [Range(0f, 1f)]
    [SerializeField] private float hurtVolume = 1f;

    [Range(0f, 0.5f)]
    [SerializeField] private float hurtPitchVariation = 0.1f;

    [Header("Death")]
    [SerializeField] private float deathHurtPlayTime = 0.15f;
    [SerializeField] private float deathFadeTime = 0.8f;
    [SerializeField] private string deathHurtStateName = "Hurt";

    [Range(0f, 1f)]
    [SerializeField] private float deathFreezeNormalizedTime = 0.8f;

    [Header("Death White + Clip Threshold")]
    [SerializeField] private float deathWhiteIntensity = 5f;
    [SerializeField] private float deathDitherStart = 0.01f;
    [SerializeField] private float deathDitherEnd = -1f;

    EnemyVisuals visuals;
    Coroutine flashRoutine;

    bool isDead;
    public bool IsDead => isDead;

    void Awake()
    {
        currentHealth = maxHealth;

        ai = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombatController>();
        motor = GetComponent<EnemyMotor>();

        // Resolve the visual driver. If none is present, auto-add the sprite
        // variant so legacy 2D enemy prefabs keep working with no changes.
        visuals = GetComponent<EnemyVisuals>();
        if (visuals == null)
            visuals = gameObject.AddComponent<EnemySpriteVisuals>();
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

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // Flash + hurt sfx always happen (visual/audio feedback of the hit).
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashWhite());
        PlayHurtSfx();

        // If the enemy is uninterruptible, skip ALL flinch behaviour:
        // no knockback, no hitstun, no Hurt animation, current attack keeps
        // going. The damage still counted (currentHealth already reduced).
        if (!canBeInterrupted) return;

        // IMPORTANT:
        // Face / knockback must happen BEFORE Hurt animation is triggered.
        bool didKnockback = ApplyStepKnockback(hitDir, step);

        if (motor != null)
        {
            if (!didKnockback)
                motor.FaceDirection(-hitDir);

            motor.ApplyHitStun(hitStunTime);
        }

        if (combat != null)
            combat.OnTakeDamage(hitDir);

        var ranged = GetComponent<EnemyRangedCombatController>();
        if (ranged != null)
            ranged.OnTakeDamage(hitDir);
    }

    bool ApplyStepKnockback(Vector3 hitDir, int step)
    {
        if (motor == null) return false;

        int index = step - 1;
        if (index < 0) return false;

        bool enabled = GetStepBool(knockbackEnabledPerStep, index, false);
        if (!enabled) return false;

        float force = GetStepFloat(knockbackForcePerStep, index, defaultKnockbackForce);
        float time = GetStepFloat(knockbackTimePerStep, index, defaultKnockbackTime);

        motor.DoKnockback(hitDir, force, time);
        return true;
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

        StopAllMovementImmediately();

        AwardVarium();
        AwardItemDrops();

        PlayDeathSfx();
        StartCoroutine(DieRoutine());
    }

    void StopAllMovementImmediately()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (ai != null) ai.enabled = false;

        if (combat != null)
        {
            EnemyAttackScheduler.ReleaseIfExists(combat);
            combat.DisableHitbox();
            combat.enabled = false;
        }

        var ranged = GetComponent<EnemyRangedCombatController>();
        if (ranged != null)
        {
            EnemyAttackScheduler.ReleaseIfExists(ranged);
            ranged.enabled = false;
        }

        if (motor != null)
        {
            motor.Stop();
            motor.enabled = false;
        }

        var push = GetComponent<CharacterPushApart>();
        if (push != null) push.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        var rb2d = GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        foreach (var c in GetComponentsInChildren<Collider>())
            if (c != null) c.enabled = false;

        foreach (var c in GetComponentsInChildren<Collider2D>())
            if (c != null) c.enabled = false;
    }

    IEnumerator DieRoutine()
    {
        Vector3 frozenPosition = transform.position;

        var animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.ResetTrigger("Attack");
            animator.SetBool("IsAttacking", false);
            animator.SetBool("isDefending", false);
            animator.ResetTrigger("Hurt");

            animator.speed = 1f;
            animator.Play(deathHurtStateName, 0, 0f);
            animator.Update(0f);
        }

        SetDeathVisual(1f, deathDitherStart, deathWhiteIntensity);

        float hurtTimer = 0f;

        while (hurtTimer < deathHurtPlayTime)
        {
            transform.position = frozenPosition;
            hurtTimer += Time.deltaTime;
            yield return null;
        }

        if (animator != null)
        {
            animator.Play(deathHurtStateName, 0, deathFreezeNormalizedTime);
            animator.Update(0f);
            animator.speed = 0f;
        }

        float dur = Mathf.Max(0.01f, deathFadeTime);
        float t = 0f;

        while (t < dur)
        {
            transform.position = frozenPosition;

            if (animator != null)
            {
                animator.Play(deathHurtStateName, 0, deathFreezeNormalizedTime);
                animator.Update(0f);
            }

            t += Time.deltaTime;

            float normalized = Mathf.Clamp01(t / dur);

            float alpha = 1f;
            float dither = Mathf.Lerp(deathDitherStart, deathDitherEnd, normalized);
            float white = deathWhiteIntensity;

            SetDeathVisual(alpha, dither, white);

            yield return null;
        }

        SetDeathVisual(1f, deathDitherEnd, deathWhiteIntensity);

        Destroy(gameObject);
    }

    void SetDeathVisual(float alpha, float dither, float white)
    {
        if (visuals != null) visuals.SetDeathVisual(alpha, dither, white);
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

    void AwardVarium()
    {
        if (variumMin <= 0 && variumMax <= 0) return;

        int amount = Random.Range(variumMin, variumMax + 1);
        if (amount <= 0) return;

        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddVarium(amount);
        else
            Debug.LogWarning("[EnemyHealth] No PlayerWallet in scene; varium reward lost. " +
                             "Add a PlayerWallet component to the Player GameObject.", this);
    }

    void AwardItemDrops()
    {
        if (itemDrops == null || itemDrops.Count == 0) return;

        var inv = ResolvePlayerInventory();
        if (inv == null)
        {
            Debug.LogWarning("[EnemyHealth] No PlayerInventory in scene; item drops lost.", this);
            return;
        }

        for (int i = 0; i < itemDrops.Count; i++)
        {
            var drop = itemDrops[i];
            if (drop == null || drop.item == null) continue;
            if (drop.chance <= 0f) continue;

            if (Random.value <= drop.chance)
            {
                inv.Add(drop.item, drop.quantity);
                ItemPickupNotifier.Show(
                    string.IsNullOrEmpty(drop.item.displayName) ? drop.item.name : drop.item.displayName,
                    drop.item.icon);
            }
        }
    }

    static PlayerInventory ResolvePlayerInventory()
    {
        var players = PlayerHealth.All;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            var inv = players[i].GetComponent<PlayerInventory>();
            if (inv != null) return inv;
        }

        var pgo = GameObject.FindWithTag("Player");
        return pgo != null ? pgo.GetComponent<PlayerInventory>() : null;
    }

    IEnumerator FlashWhite()
    {
        if (visuals == null) yield break;
        visuals.SetFlashColor(Color.white * flashIntensity);
        yield return new WaitForSeconds(flashDuration);
        visuals.SetFlashColor(Color.black);
        flashRoutine = null;
    }

    IEnumerator BlockFlash()
    {
        if (visuals == null) yield break;
        visuals.SetFlashColor(Color.gray * 2f);
        yield return new WaitForSeconds(0.05f);
        visuals.SetFlashColor(Color.black);
        flashRoutine = null;
    }
}