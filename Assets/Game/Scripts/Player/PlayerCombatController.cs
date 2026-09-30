using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator spriteAnimator;
    [SerializeField] private GameObject meleeHitbox;
    [SerializeField] private Transform meleeHitboxTransform;
    [SerializeField] private PlayerMotor motor;
    [SerializeField] private PlayerJump jump;
    [Tooltip("Weapon sprite under SpriteBody. Cleared when not in combat so Run/CombatRun can share one clip.")]
    [SerializeField] private SpriteRenderer weaponBody;
    [Tooltip("Optional. Plays the weapon 'store into player' VFX when combat ends.")]
    [SerializeField] private WeaponStoreEffect weaponStoreEffect;
    [Tooltip("Soft targeting / melee magnetism. Auto-added if missing.")]
    [SerializeField] private MeleeSoftTargeting softTargeting;
    PlayerSwimming swim;
    PlayerHealth health;
    PlayerEquipment equipment;

    [Header("Combo")]
    [SerializeField] private int maxCombo = 3;
    [SerializeField] private float comboBufferTime = 0.25f;

    [Header("Combo Lockouts")]
    [SerializeField] private float comboEndCooldown = 0.25f;
    [SerializeField] private float missComboCooldown = 0.15f;

    [Header("Combat Mode")]
    [SerializeField] private float combatTimeout = 3.5f;

    [Header("Attack Lunge")]
    [SerializeField] private bool useAttackLunge = true;
    [SerializeField] private bool lungeStep1 = true;
    [SerializeField] private bool lungeStep2 = true;
    [SerializeField] private bool lungeStep3 = true;

    [System.Serializable]
    private struct LungeParams
    {
        public float speed;
        public float duration;
        public float delay;

        [Tooltip("If on, lunge speed starts at full and eases out to 0 over the duration.")]
        public bool useEaseOut;

        [Tooltip("Ease-out strength. 1 = linear slowdown, 2 = classic ease-out (fast then soft stop), higher = stays fast longer then stops softer.")]
        [Min(1f)]
        public float easeOutPower;
    }

    [Header("Lunge Params Per Step")]
    [SerializeField] private LungeParams step1 = new LungeParams
    {
        speed = 6.5f, duration = 0.08f, delay = 0f,
        useEaseOut = true, easeOutPower = 2f
    };
    [SerializeField] private LungeParams step2 = new LungeParams
    {
        speed = 6.5f, duration = 0.06f, delay = 0.02f,
        useEaseOut = true, easeOutPower = 2f
    };
    [SerializeField] private LungeParams step3 = new LungeParams
    {
        speed = 7.5f, duration = 0.08f, delay = 0.04f,
        useEaseOut = true, easeOutPower = 2f
    };

    [Header("Roll")]
    [SerializeField] private bool enableRoll = true;
    [SerializeField] private float rollSpeed = 8f;
    [SerializeField] private float rollDuration = 0.25f;
    [SerializeField] private float rollCooldown = 0.15f;

    [Tooltip("If no move input, roll uses last facing direction.")]
    [SerializeField] private bool rollUsesFacingIfNoInput = true;

    [Header("Hitbox Position (in front)")]
    [Tooltip("World-space distance in front of facing (not multiplied by player scale).")]
    [SerializeField] private float hitboxForwardDistance = 0.60f;
    [SerializeField] private Vector3 hitboxLocalOffset = Vector3.zero;

    [Tooltip("World-space uniform scale for the hitbox transform. Cancels the player root scale (e.g. 2.5).")]
    [SerializeField] private float hitboxWorldScale = 0.7f;

    [System.Serializable]
    private struct HitboxStepProfile
    {
        [Tooltip("If on, this step uses the values below instead of the defaults above.")]
        public bool overrideDefaults;

        public float forwardDistance;
        public Vector3 localOffset;

        [Tooltip("BoxCollider size for this step. Leave at 0 to use the prefab default size (not the previous hit).")]
        public Vector3 boxSize;
    }

    [Tooltip("Optional per-combo-step hitbox (index 0 = Attack1, 1 = Attack2, 2 = Attack3). " +
             "Anim events already pass the step into EnableHitboxInt.")]
    [SerializeField] private HitboxStepProfile[] hitboxPerStep = new HitboxStepProfile[3];

    [System.Serializable]
    private struct KnockbackStepProfile
    {
        [Tooltip("If off, this combo hit deals damage but does not push the enemy.")]
        public bool pushEnemy;

        [Tooltip("Push speed in world units/sec.")]
        public float force;

        [Tooltip("How long the push lasts (seconds).")]
        public float duration;
    }

    [Header("Enemy Knockback Per Combo Step")]
    [Tooltip("Index 0 = Attack1, 1 = Attack2, 2 = Attack3. Tune push feel here per character.")]
    [SerializeField] private KnockbackStepProfile[] knockbackPerStep = new KnockbackStepProfile[]
    {
        new KnockbackStepProfile { pushEnemy = false, force = 0f,   duration = 0f },
        new KnockbackStepProfile { pushEnemy = true,  force = 2.5f, duration = 0.12f },
        new KnockbackStepProfile { pushEnemy = true,  force = 10f,  duration = 0.28f },
    };

    [Header("Attack Swing SFX")]
    [Tooltip("One swing clip per combo step (index 0 = step 1, etc). " +
             "If a slot is empty, no sound is played for that step.")]
    [SerializeField] private AudioClip[] swingSounds = new AudioClip[3];

    [Range(0f, 1f)]
    [SerializeField] private float swingVolume = 1f;

    [Header("Roll SFX")]
    [SerializeField] private AudioClip rollSound;

    [Range(0f, 1f)]
    [SerializeField] private float rollVolume = 0.8f;

    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int ComboIndexHash  = Animator.StringToHash("ComboIndex");
    private static readonly int InCombatHash    = Animator.StringToHash("InCombat");
    private static readonly int AttackTrigger   = Animator.StringToHash("Attack");
    private static readonly int IsRollingHash   = Animator.StringToHash("IsRolling");
    private static readonly int IsMovingHash    = Animator.StringToHash("IsMoving");
    private static readonly int RollTrigger     = Animator.StringToHash("Roll");

    private int comboIndex = 0;
    private bool isAttacking = false;

    private float comboBufferUntil = 0f;
    private bool buffered = false;

    // True after a swing's link point when the next hit was not buffered.
    // Roll can cancel the rest of the combo until EndAttack.
    private bool comboLinkOpen;

    private float lockoutUntil = 0f;

    private float combatTimer = 0f;
    private bool inCombat = false;

    private bool isRolling = false;
    private float rollEndTime = 0f;
    private float rollCooldownUntil = 0f;

    // Play store VFX once on the combat-exit edge (animator still writes weapon each frame).
    private bool pendingWeaponStoreVfx;

    private BoxCollider meleeHitboxCollider;
    private Vector3 defaultHitboxSize = Vector3.one;
    private bool hasDefaultHitboxSize;
    private float defaultHitboxHeightWorld;

    void Awake()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!jump) jump = GetComponent<PlayerJump>();

        if (!spriteAnimator) Debug.LogError("Assign SpriteBody Animator", this);
        if (!meleeHitbox) Debug.LogError("Assign MeleeHitbox GameObject", this);

        if (!meleeHitboxTransform && meleeHitbox)
            meleeHitboxTransform = meleeHitbox.transform;

        if (meleeHitbox)
        {
            meleeHitboxCollider = meleeHitbox.GetComponent<BoxCollider>();
            if (meleeHitboxCollider)
            {
                defaultHitboxSize = meleeHitboxCollider.size;
                hasDefaultHitboxSize = true;
            }

            if (meleeHitboxTransform)
                defaultHitboxHeightWorld = meleeHitboxTransform.position.y - transform.position.y;

            // Start disabled so it can't ghost-hit before the first attack.
            meleeHitbox.SetActive(false);
        }

        if (!weaponBody && spriteAnimator)
        {
            var t = spriteAnimator.transform.Find("WeaponBody");
            if (t) weaponBody = t.GetComponent<SpriteRenderer>();
        }

        if (!weaponStoreEffect)
        {
            weaponStoreEffect = GetComponent<WeaponStoreEffect>();
            if (!weaponStoreEffect)
                weaponStoreEffect = gameObject.AddComponent<WeaponStoreEffect>();
        }

        swim = GetComponent<PlayerSwimming>();
        health = GetComponent<PlayerHealth>();
        equipment = GetComponent<PlayerEquipment>();

        if (!softTargeting)
            softTargeting = GetComponent<MeleeSoftTargeting>();
        if (!softTargeting)
            softTargeting = gameObject.AddComponent<MeleeSoftTargeting>();

        if (spriteAnimator)
        {
            spriteAnimator.SetBool(IsAttackingHash, false);
            spriteAnimator.SetInteger(ComboIndexHash, 0);
            spriteAnimator.SetBool(IsRollingHash, false);
        }

        EnsureKnockbackDefaults();

        DisableHitbox();
    }

    void EnsureKnockbackDefaults()
    {
        if (knockbackPerStep != null && knockbackPerStep.Length >= 3)
            return;

        knockbackPerStep = new KnockbackStepProfile[]
        {
            new KnockbackStepProfile { pushEnemy = false, force = 0f,   duration = 0f },
            new KnockbackStepProfile { pushEnemy = true,  force = 2.5f, duration = 0.12f },
            new KnockbackStepProfile { pushEnemy = true,  force = 10f,  duration = 0.28f },
        };
    }

    void OnValidate()
    {
        EnsureKnockbackDefaults();
    }

    void LateUpdate()
    {
        // Shared Run/CombatRun clips always write weapon sprites; hide them outside combat
        // without restarting the locomotion state.
        if (weaponBody == null || inCombat)
            return;

        // Play store VFX even if Idle already blanked WeaponBody — snapshot was taken in ExitCombat.
        if (pendingWeaponStoreVfx)
        {
            if (weaponStoreEffect != null)
                weaponStoreEffect.TryPlay();
            pendingWeaponStoreVfx = false;
        }

        if (weaponBody.sprite != null)
            weaponBody.sprite = null;
    }

    void Update()
    {
        if (health != null && health.isTakingDamage)
            return;

        if (buffered && Time.time > comboBufferUntil)
            buffered = false;

        if (isRolling && Time.time >= rollEndTime)
            EndRoll();

        if (inCombat)
        {
            combatTimer -= Time.deltaTime;
            if (combatTimer <= 0f && !isAttacking && !isRolling)
                ExitCombat();
        }
    }

    public void OnAttack(InputAction.CallbackContext ctx)
    {
        if (health != null && health.isTakingDamage)
            return;

        if (swim != null && swim.IsSwimming())
            return;

        if (!ctx.performed) return;

        if (jump != null && !jump.IsGrounded)
            return;

        if (isRolling) return;

        // No weapon equipped → can't attack.
        if (equipment != null && !equipment.HasWeapon)
            return;

        if (!isAttacking && Time.time < lockoutUntil)
            return;

        EnterCombat();

        if (!isAttacking)
        {
            StartAttack1();
            return;
        }

        buffered = true;
        comboBufferUntil = Time.time + comboBufferTime;
    }

    public void OnRoll(InputAction.CallbackContext ctx)
    {
        if (health != null && health.isTakingDamage)
            return;

        if (swim != null && swim.IsSwimming())
            return;

        if (!ctx.performed) return;
        if (!enableRoll) return;
        if (isAttacking && !comboLinkOpen) return;

        if (jump != null && !jump.IsGrounded)
            return;

        if (Time.time < rollCooldownUntil) return;

        if (isAttacking)
            EndCombo();

        StartRoll();
    }

    private void StartAttack1()
    {
        if (jump != null && !jump.IsGrounded)
            return;

        isAttacking = true;
        comboIndex = 1;
        buffered = false;

        Vector2 intent = motor != null ? motor.GetFacing2D() : Vector2.down;
        LungeParams p = step1;
        float easePower = p.useEaseOut ? Mathf.Max(1f, p.easeOutPower) : 0f;

        MeleeSoftTargeting.SoftAim aim = softTargeting != null
            ? softTargeting.BeginCombo(intent, p.speed, p.duration, easePower)
            : new MeleeSoftTargeting.SoftAim { facing2D = intent, speedScale = 1f, steered = false };

        motor?.LockFacing(aim.facing2D);
        motor?.LockMovement(true);
        DoStepLunge(1, aim);
        PlaySwingSfx(1);

        spriteAnimator?.SetBool(IsAttackingHash, true);
        spriteAnimator?.SetInteger(ComboIndexHash, comboIndex);

        spriteAnimator?.ResetTrigger(AttackTrigger);
        spriteAnimator?.SetTrigger(AttackTrigger);

        combatTimer = combatTimeout;
    }

    private void AdvanceCombo()
    {
        if (comboIndex >= maxCombo) return;

        comboIndex++;
        buffered = false;

        DoStepLunge(comboIndex);
        PlaySwingSfx(comboIndex);

        spriteAnimator?.SetInteger(ComboIndexHash, comboIndex);

        spriteAnimator?.ResetTrigger(AttackTrigger);
        spriteAnimator?.SetTrigger(AttackTrigger);

        combatTimer = combatTimeout;
    }

    private void DoStepLunge(int step)
    {
        if (!useAttackLunge || motor == null) return;

        bool enabled =
            step == 1 ? lungeStep1 :
            step == 2 ? lungeStep2 :
                        lungeStep3;

        if (!enabled) return;

        LungeParams p =
            step == 1 ? step1 :
            step == 2 ? step2 :
                        step3;

        float easePower = p.useEaseOut ? Mathf.Max(1f, p.easeOutPower) : 0f;
        Vector2 intent = motor.GetFacing2D();

        MeleeSoftTargeting.SoftAim aim = softTargeting != null
            ? softTargeting.ResolveStep(intent, step, p.speed, p.duration, easePower)
            : new MeleeSoftTargeting.SoftAim { facing2D = intent, speedScale = 1f, steered = false };

        DoStepLunge(step, aim);
    }

    private void DoStepLunge(int step, MeleeSoftTargeting.SoftAim aim)
    {
        if (!useAttackLunge || motor == null) return;

        bool enabled =
            step == 1 ? lungeStep1 :
            step == 2 ? lungeStep2 :
                        lungeStep3;

        if (!enabled) return;

        LungeParams p =
            step == 1 ? step1 :
            step == 2 ? step2 :
                        step3;

        float easePower = p.useEaseOut ? Mathf.Max(1f, p.easeOutPower) : 0f;

        // Soft targeting steers the SAME lunge — never a second magnet force.
        if (aim.steered)
            motor.LockFacing(aim.facing2D);

        // Already in attack range → keep facing/anim, skip positional lunge.
        if (aim.steered && aim.speedScale <= 0.001f)
            return;

        float speed = p.speed * Mathf.Max(0f, aim.speedScale);
        motor.BeginAttackLunge(aim.facing2D, speed, p.duration, p.delay, easePower);
    }

    /// <summary>
    /// World-space distance from player root to the far face of the melee hitbox
    /// along attack forward. Used by soft targeting for attack spacing.
    /// </summary>
    public float GetMeleeHitboxReach()
    {
        float reach = hitboxForwardDistance;

        Vector3 size = hasDefaultHitboxSize ? defaultHitboxSize : Vector3.zero;
        if (size.sqrMagnitude > 0.0001f)
        {
            float depth = size.z * 0.5f * Mathf.Max(0.01f, hitboxWorldScale);
            reach += depth;
        }

        return Mathf.Max(0.05f, reach);
    }

    public void TryAdvanceCombo()
    {
        if (!isAttacking) return;

        if (buffered && Time.time <= comboBufferUntil && comboIndex < maxCombo)
        {
            comboLinkOpen = false;
            AdvanceCombo();
            return;
        }

        lockoutUntil = Time.time + missComboCooldown;
        buffered = false;
        comboLinkOpen = true;
    }

    public void EndAttack()
    {
        if (!isAttacking) return;

        if (comboIndex >= maxCombo)
            lockoutUntil = Time.time + comboEndCooldown;

        EndCombo();
    }

    private void EndCombo()
    {
        isAttacking = false;
        comboIndex = 0;
        buffered = false;
        comboLinkOpen = false;

        spriteAnimator?.SetBool(IsAttackingHash, false);
        spriteAnimator?.SetInteger(ComboIndexHash, 0);

        softTargeting?.ClearComboTarget();

        motor?.CancelAttackLunge();
        motor?.LockMovement(false);
        motor?.UnlockFacing();

        DisableHitbox();

        combatTimer = combatTimeout;
    }

    public void EnableHitboxInt(int step)
    {
        if (!IsActivePartyMember())
        {
            DisableHitbox();
            return;
        }

        if (!meleeHitbox || !meleeHitboxTransform || motor == null)
            return;

        float dist = hitboxForwardDistance;
        Vector3 offset = hitboxLocalOffset;
        Vector3 boxSize = hasDefaultHitboxSize ? defaultHitboxSize : Vector3.zero;

        int idx = step - 1;
        if (hitboxPerStep != null && idx >= 0 && idx < hitboxPerStep.Length
            && hitboxPerStep[idx].overrideDefaults)
        {
            dist = hitboxPerStep[idx].forwardDistance;
            offset = hitboxPerStep[idx].localOffset;
            // Per-step size only if set; otherwise keep the prefab default (not the previous hit's size).
            if (hitboxPerStep[idx].boxSize.sqrMagnitude > 0.0001f)
                boxSize = hitboxPerStep[idx].boxSize;
        }

        PositionHitboxInFront(dist, offset);
        ApplyHitboxSize(boxSize);

        var hb = meleeHitbox.GetComponent<MeleeHitbox>();

        if (hb)
        {
            hb.SetOwner(transform);
            ApplyKnockbackProfileToHitbox(hb, step);
        }

        meleeHitbox.SetActive(true);

        // Fresh once-per-target list for this swing (works even if GO was already active).
        if (hb)
            hb.BeginHitWindow(step);
    }

    void ApplyKnockbackProfileToHitbox(MeleeHitbox hb, int step)
    {
        int idx = step - 1;
        if (knockbackPerStep != null && idx >= 0 && idx < knockbackPerStep.Length)
        {
            var profile = knockbackPerStep[idx];
            hb.SetKnockback(profile.pushEnemy, profile.force, profile.duration);
            return;
        }

        // Fallback if the array was resized shorter than the combo.
        hb.SetKnockback(step >= 2, step >= 3 ? 10f : 2.5f, step >= 3 ? 0.28f : 0.12f);
    }

    public void DisableHitbox()
    {
        if (meleeHitbox) meleeHitbox.SetActive(false);
        // Restore so the next Enable doesn't inherit a previous step's size by accident.
        if (hasDefaultHitboxSize)
            ApplyHitboxSize(defaultHitboxSize);
    }

    bool IsActivePartyMember()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();
        if (health == null)
            return true;
        return Party.Active == null || Party.Active == health;
    }

    void ApplyHitboxSize(Vector3 size)
    {
        if (size.sqrMagnitude < 0.0001f) return;
        if (!meleeHitboxCollider && meleeHitbox)
            meleeHitboxCollider = meleeHitbox.GetComponent<BoxCollider>();
        if (meleeHitboxCollider)
            meleeHitboxCollider.size = size;
    }

    private void PositionHitboxInFront()
    {
        PositionHitboxInFront(hitboxForwardDistance, hitboxLocalOffset);
    }

    private void PositionHitboxInFront(float forwardDistance, Vector3 localOffset)
    {
        Vector2 dir2 = motor.GetFacing2D();
        if (dir2.sqrMagnitude < 0.001f) dir2 = Vector2.down;
        dir2.Normalize();

        Vector3 forward = new Vector3(dir2.x, 0f, dir2.y);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        else right.Normalize();

        // Cancel player root scale (often 2.5) so BoxCollider.size matches the debug cube in world units.
        Vector3 parentLossy = transform.lossyScale;
        float s = Mathf.Max(0.0001f, hitboxWorldScale);
        meleeHitboxTransform.localScale = new Vector3(
            s / Mathf.Max(0.0001f, parentLossy.x),
            s / Mathf.Max(0.0001f, parentLossy.y),
            s / Mathf.Max(0.0001f, parentLossy.z)
        );

        float height = !Mathf.Approximately(localOffset.y, 0f)
            ? localOffset.y
            : defaultHitboxHeightWorld;

        // forwardDistance / offset XZ are WORLD units (not scaled by the 2.5 root).
        Vector3 worldPos = transform.position
                           + forward * (forwardDistance + localOffset.z)
                           + right * localOffset.x;
        worldPos.y = transform.position.y + height;

        meleeHitboxTransform.SetPositionAndRotation(
            worldPos,
            Quaternion.LookRotation(forward, Vector3.up)
        );

        Physics.SyncTransforms();
    }

    private void StartRoll()
    {
        if (jump != null && !jump.IsGrounded)
            return;

        isRolling = true;

        if (rollSound != null)
            AudioManager.PlaySfxOrFallback(rollSound, transform.position, rollVolume);

        rollEndTime = Time.time + rollDuration;
        rollCooldownUntil = rollEndTime + rollCooldown;

        EnterCombat();

        motor?.LockMovement(true);

        Vector2 rollDir2D = motor != null ? motor.GetMoveInput2D() : Vector2.zero;

        if (rollDir2D.sqrMagnitude < 0.01f && rollUsesFacingIfNoInput && motor != null)
            rollDir2D = motor.GetFacing2D();

        if (rollDir2D.sqrMagnitude < 0.01f)
            rollDir2D = Vector2.down;

        rollDir2D.Normalize();

        motor?.BeginRoll(rollDir2D, rollSpeed, rollDuration);

        spriteAnimator?.SetBool(IsRollingHash, true);
        spriteAnimator?.SetBool(IsMovingHash, false);
        spriteAnimator?.ResetTrigger(RollTrigger);
        spriteAnimator?.SetTrigger(RollTrigger);

        combatTimer = combatTimeout;
    }

    public void EndRoll()
    {
        isRolling = false;

        motor?.EndRoll();
        motor?.LockMovement(false);
        motor?.UnlockFacing();

        spriteAnimator?.SetBool(IsRollingHash, false);

        combatTimer = combatTimeout;
    }

    private void ExitCombat()
    {
        // Snapshot NOW — CombatIdle→Idle blanks WeaponBody before LateUpdate.
        if (inCombat)
        {
            if (weaponStoreEffect != null)
                weaponStoreEffect.CaptureNow();
            pendingWeaponStoreVfx = true;
        }

        inCombat = false;
        spriteAnimator?.SetBool(InCombatHash, false);
    }

    private void EnterCombat()
    {
        combatTimer = combatTimeout;
        if (inCombat) return;

        inCombat = true;
        pendingWeaponStoreVfx = false;
        if (weaponStoreEffect != null)
            weaponStoreEffect.ClearPendingCapture();
        spriteAnimator?.SetBool(InCombatHash, true);
    }

    public void CancelCombatImmediate()
    {
        InterruptCombatActions();
        motor?.LockMovement(false);
        motor?.UnlockFacing();
        ExitCombat();
    }

    /// <summary>
    /// Stop attack/roll/hitbox without leaving combat mode.
    /// Used when taking a hit so HurtCombat can keep the weapon visible.
    /// </summary>
    public void InterruptCombatActions()
    {
        isAttacking = false;
        isRolling = false;

        comboIndex = 0;
        buffered = false;
        comboLinkOpen = false;

        softTargeting?.ClearComboTarget();

        spriteAnimator?.SetBool(IsAttackingHash, false);
        spriteAnimator?.SetBool(IsRollingHash, false);
        spriteAnimator?.SetInteger(ComboIndexHash, 0);

        motor?.CancelAttackLunge();
        // Don't unlock facing/movement here — TakeDamage re-locks them for the hit reaction.
        DisableHitbox();

        // Stay in combat; refresh the combat timeout so HurtCombat keeps weapon sprites.
        if (inCombat)
            combatTimer = combatTimeout;
    }

    public bool IsInCombat()
    {
        return inCombat;
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }

    public bool IsRolling()
    {
        return isRolling;
    }

    private void PlaySwingSfx(int step)
    {
        if (swingSounds == null || swingSounds.Length == 0) return;

        int idx = Mathf.Clamp(step - 1, 0, swingSounds.Length - 1);
        var clip = swingSounds[idx];
        if (clip == null) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip, swingVolume);
    }
}