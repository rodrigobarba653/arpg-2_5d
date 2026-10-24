using UnityEngine;
using System.Collections;

public class PlayerSwimming : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor motor;
    public PlayerJump jump;
    public Animator animator;
    PlayerCombatController combat;

    [Header("Swim Settings")]
    public float swimSpeed = 2.5f;
    public float swimGravity = -2f;

    [Header("Jump Splash")]
    [Tooltip("Played on the water surface when the player jumps or falls in. Running into the water does not play it.")]
    public GameObject jumpSplashVfx;

    [Tooltip("Size of the whole splash. 1 is the prefab's own size.")]
    public float jumpSplashScale = 0.5f;

    [Tooltip("World-space shift from the splash point on the water. Y moves it up or down.")]
    public Vector3 jumpSplashOffset = Vector3.zero;

    public float jumpSplashLifetime = 1.5f;

    [Header("Water Dip")]
    [Tooltip("How far the sprite sinks on a short fall, in local units.")]
    public float dipMin = 0.1f;

    [Tooltip("Deepest the sprite is allowed to sink. Taller falls approach this and then stop.")]
    public float dipMax = 0.4f;

    [Tooltip("Fall height, from the highest point to the water, that reaches the deepest sink.")]
    public float dipHeightForMax = 4f;

    [Tooltip("Seconds for the sprite to sink and come back.")]
    public float dipDuration = 0.5f;

    [Tooltip("How sharp the sink is. 2 is gentle. Higher waits longer at the start and the bottom, and moves faster in between.")]
    public float dipEase = 4f;

    [Tooltip("How gently the sprite comes back. Lower arrives with less acceleration. 2 settles in softly.")]
    public float dipReturnEase = 2f;

    [Tooltip("How far the sprite pops above its rest pose before settling. Local units.")]
    public float dipOvershoot = 0.05f;

    Transform sprite;
    Vector3 spriteRest;
    bool spriteRestCached;
    Coroutine dipRoutine;

    bool isSwimming;
    bool hasIsSwimmingParam;

    float originalSpeed;
    float originalGravity;

    static readonly int IsSwimmingHash = Animator.StringToHash("isSwimming");

    PlayerEquipment equipment;

    void Start()
    {
        if (!motor) motor = GetComponent<PlayerMotor>();
        if (!jump) jump = GetComponent<PlayerJump>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        combat = GetComponent<PlayerCombatController>();
        equipment = GetComponent<PlayerEquipment>();

        originalSpeed = motor.moveSpeed;
        originalGravity = motor.gravity;

        CacheAnimatorParameter();

        // PlayerEquipment swaps the runtime animator controller when a weapon
        // is equipped — re-check the swim parameter when that happens.
        if (equipment != null)
            equipment.OnWeaponChanged += _ => CacheAnimatorParameter();

        if (animator != null)
            sprite = animator.transform;
    }

    void CacheAnimatorParameter()
    {
        hasIsSwimmingParam = false;
        if (animator == null)
        {
            Debug.LogWarning("[PlayerSwimming] No Animator found — swim animation will not play.", this);
            return;
        }

        var parms = animator.parameters;
        for (int i = 0; i < parms.Length; i++)
        {
            if (parms[i].nameHash == IsSwimmingHash)
            {
                hasIsSwimmingParam = true;
                break;
            }
        }

        if (!hasIsSwimmingParam)
        {
            string ctrlName = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "NULL";
            Debug.LogWarning($"[PlayerSwimming] Animator on '{animator.name}' is missing " +
                             "'isSwimming' (bool). Add it in the Animator Controller's " +
                             "Parameters tab so the swim state can activate. " +
                             $"(Currently equipped controller: {ctrlName})", this);
        }
    }

    public void EnterWater(Collider water = null)
    {
        if (isSwimming) return;

        Debug.Log("[PlayerSwimming] EnterWater()", this);

        if (jump != null && jump.WaterSplashPending)
        {
            float surfaceY = water != null ? water.bounds.max.y : transform.position.y;
            float drop = jump.AirborneDrop(surfaceY);
            jump.ConsumeWaterSplash();
            SpawnJumpSplash(water);
            BeginSpriteDip(drop);
        }

        // 🔥 1. CAMBIAR ESTADO PRIMERO
        isSwimming = true;

        // 🔥 2. cancelar combate
        if (combat != null)
            combat.CancelCombatImmediate();

        // 🔥 3. cancelar salto completamente
        if (jump != null)
            jump.ForceExitAirState();

        // 🔥 4. reset físico
        motor.SetVerticalVelocity(0f);

        // 🔥 5. aplicar movimiento
        motor.moveSpeed = swimSpeed;
        motor.gravity = swimGravity;

        // 🔥 6. animación inmediata (solo si el parámetro existe)
        if (animator && hasIsSwimmingParam)
        {
            animator.SetBool(IsSwimmingHash, true);
            animator.Update(0f);
        }

        // 🔥 7. fix transición
        StartCoroutine(ForceSwimTransition());
    }

    public void ExitWater()
    {
        if (!isSwimming) return;

        Debug.Log("[PlayerSwimming] ExitWater()", this);

        // 🔥 1. CAMBIAR ESTADO PRIMERO
        isSwimming = false;

        // 🔺 restaurar movimiento
        motor.moveSpeed = originalSpeed;
        motor.gravity = originalGravity;

        // 🔺 reset físico
        motor.SetVerticalVelocity(0f);

        // 🔺 animación (solo si el parámetro existe)
        if (animator && hasIsSwimmingParam)
        {
            animator.SetBool(IsSwimmingHash, false);
            animator.Update(0f);
        }

        // 🔺 fix transición
        StartCoroutine(ForceExitTransition());
        RestoreSprite();
    }

    IEnumerator ForceSwimTransition()
    {
        motor.LockMovement(true);
        yield return null;
        motor.LockMovement(false);
    }

    IEnumerator ForceExitTransition()
    {
        motor.LockMovement(true);
        yield return null;
        motor.LockMovement(false);
    }

    public bool IsSwimming()
    {
        return isSwimming;
    }

    void BeginSpriteDip(float drop)
    {
        if (sprite == null && animator != null)
            sprite = animator.transform;
        if (sprite == null)
            return;

        if (!spriteRestCached)
        {
            spriteRest = sprite.localPosition;
            spriteRestCached = true;
        }

        float heightSpan = Mathf.Max(0.01f, dipHeightForMax);
        float t = Mathf.Clamp01(drop / heightSpan);
        float depth = Mathf.Lerp(Mathf.Min(dipMin, dipMax), Mathf.Max(dipMin, dipMax), t);

        if (dipRoutine != null)
            StopCoroutine(dipRoutine);
        dipRoutine = StartCoroutine(DipSprite(depth));
    }

    IEnumerator DipSprite(float depth)
    {
        float duration = Mathf.Max(0.05f, dipDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (sprite == null)
                yield break;

            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = spriteRest;
            pos.y -= DipOffset(u, depth);
            sprite.localPosition = pos;
            yield return null;
        }

        RestoreSprite();
        dipRoutine = null;
    }

    float DipOffset(float u, float depth)
    {
        const float sinkEnd = 0.46f;
        const float riseEnd = 0.78f;
        float overshoot = Mathf.Max(0f, dipOvershoot);

        if (u <= sinkEnd)
            return depth * EaseInOut(u / sinkEnd, Mathf.Max(1f, dipEase));

        if (u <= riseEnd)
        {
            float leg = (u - sinkEnd) / (riseEnd - sinkEnd);
            float eased = EaseOut(leg, Mathf.Max(1f, dipReturnEase));
            return Mathf.Lerp(depth, -overshoot, eased);
        }

        float settle = (u - riseEnd) / (1f - riseEnd);
        return Mathf.Lerp(-overshoot, 0f, EaseOut(settle, 2f));
    }

    static float EaseInOut(float t, float power)
    {
        if (t < 0.5f)
            return 0.5f * Mathf.Pow(t * 2f, power);

        return 1f - 0.5f * Mathf.Pow((1f - t) * 2f, power);
    }

    static float EaseOut(float t, float power)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), power);
    }

    void RestoreSprite()
    {
        if (dipRoutine != null)
        {
            StopCoroutine(dipRoutine);
            dipRoutine = null;
        }

        if (sprite != null && spriteRestCached)
            sprite.localPosition = spriteRest;
    }

    void SpawnJumpSplash(Collider water)
    {
        if (jumpSplashVfx == null)
            return;

        Vector3 pos = transform.position;
        if (water != null)
            pos.y = water.bounds.max.y;
        pos += jumpSplashOffset;

        float scale = Mathf.Max(0.05f, jumpSplashScale);

        // Scale the holder before the effect wakes up. The splash emits on
        // enable, so changing its scale afterwards does not resize the burst.
        var holder = new GameObject("JumpSplash");
        holder.SetActive(false);
        holder.transform.SetPositionAndRotation(pos, Quaternion.identity);
        holder.transform.localScale = Vector3.one * scale;

        GameObject splash = Instantiate(jumpSplashVfx, holder.transform);
        splash.transform.localPosition = Vector3.zero;
        splash.transform.localRotation = jumpSplashVfx.transform.localRotation;

        ParticleSystem[] systems = splash.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        holder.SetActive(true);
        if (jumpSplashLifetime > 0f)
            Destroy(holder, jumpSplashLifetime);
    }
}