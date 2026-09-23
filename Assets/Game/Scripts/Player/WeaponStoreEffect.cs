using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Plays the "weapon storing" VFX: last WeaponBody frame turns into a light-blue
/// particle glare and dissipates into SpriteBody when combat ends.
/// </summary>
[DisallowMultipleComponent]
public class WeaponStoreEffect : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Weapon sprite to snapshot. Auto-finds child WeaponBody if empty.")]
    [SerializeField] SpriteRenderer weaponBody;

    [Tooltip("Where particles absorb into (SpriteBody). Auto-finds parent/child if empty.")]
    [SerializeField] Transform absorbTarget;

    [Header("Timing")]
    [SerializeField] float duration = 0.32f;

    [Header("Ghost (last frame — dissolve in place like enemy death)")]
    [SerializeField] Color ghostFlash = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField] float ghostFlashIntensity = 3.5f;
    [SerializeField] float ditherStart = 0.01f;
    [SerializeField] float ditherEnd = -1f;
    [SerializeField] string clipThresholdProperty = "_ClipThreshold";
    [SerializeField] string flashColorProperty = "_FlashColor";

    [Header("Particles")]
    [SerializeField] int particleCount = 56;
    [SerializeField] float particleLifetime = 0.28f;
    [SerializeField] Vector2 particleSize = new Vector2(0.012f, 0.032f);
    [SerializeField] Color particleStart = new Color(0.85f, 0.95f, 1f, 0.95f);
    [SerializeField] Color particleMid = new Color(0.35f, 0.75f, 1f, 0.7f);
    [SerializeField] Color particleEnd = new Color(0.55f, 0.9f, 1f, 0f);
    [SerializeField] float horizontalSpread = 0.025f;
    [SerializeField] float riseSpeed = 0.35f;
    [SerializeField] float pullStrength = 18f;
    [SerializeField] float suckRate = 10f;
    [SerializeField] float absorbHeightOffset = 0.05f;

    [Header("Sorting")]
    [SerializeField] int sortingOrderOffset = 2;

    Material particleMaterial;
    bool playing;
    bool hasPendingSnapshot;
    Snapshot pendingSnapshot;

    void Awake()
    {
        ResolveRefs();
    }

    void ResolveRefs()
    {
        if (!weaponBody)
        {
            var t = transform.Find("SpriteBody/WeaponBody")
                    ?? transform.Find("WeaponBody");
            if (t) weaponBody = t.GetComponent<SpriteRenderer>();
        }

        if (!absorbTarget && weaponBody)
            absorbTarget = weaponBody.transform.parent != null
                ? weaponBody.transform.parent
                : transform;

        if (!absorbTarget)
        {
            var sb = transform.Find("SpriteBody");
            if (sb) absorbTarget = sb;
        }
    }

    /// <summary>
    /// Grab WeaponBody now (while combat clips still own it). Call on the ExitCombat
    /// edge — Idle may blank the sprite before LateUpdate.
    /// </summary>
    public bool CaptureNow()
    {
        ResolveRefs();
        if (weaponBody == null || weaponBody.sprite == null)
        {
            hasPendingSnapshot = false;
            return false;
        }

        pendingSnapshot = CaptureSnapshot();
        hasPendingSnapshot = true;
        return true;
    }

    public void ClearPendingCapture()
    {
        hasPendingSnapshot = false;
    }

    /// <summary>
    /// Play using a CaptureNow() snapshot, or a live WeaponBody sprite if still present.
    /// </summary>
    public bool TryPlay()
    {
        ResolveRefs();
        if (playing)
            return false;

        Snapshot snap;
        if (hasPendingSnapshot && pendingSnapshot.sprite != null)
        {
            snap = pendingSnapshot;
            hasPendingSnapshot = false;
        }
        else if (weaponBody != null && weaponBody.sprite != null)
        {
            snap = CaptureSnapshot();
        }
        else
        {
            return false;
        }

        StartCoroutine(PlayRoutine(snap));
        return true;
    }

    struct Snapshot
    {
        public Sprite sprite;
        public Color color;
        public bool flipX;
        public bool flipY;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool hasLocalFollow;
        public int sortingLayerId;
        public int sortingOrder;
        public Material sharedMaterial;
    }

    Snapshot CaptureSnapshot()
    {
        var t = weaponBody.transform;
        var snap = new Snapshot
        {
            sprite = weaponBody.sprite,
            color = weaponBody.color,
            flipX = weaponBody.flipX,
            flipY = weaponBody.flipY,
            position = t.position,
            rotation = t.rotation,
            scale = t.lossyScale,
            sortingLayerId = weaponBody.sortingLayerID,
            sortingOrder = weaponBody.sortingOrder,
            sharedMaterial = weaponBody.sharedMaterial
        };

        // Keep last frame, but follow the player (SpriteBody) while dissolving.
        Transform follow = absorbTarget != null ? absorbTarget : t.parent;
        if (follow != null)
        {
            snap.hasLocalFollow = true;
            if (t.parent == follow)
            {
                snap.localPosition = t.localPosition;
                snap.localRotation = t.localRotation;
                snap.localScale = t.localScale;
            }
            else
            {
                snap.localPosition = follow.InverseTransformPoint(t.position);
                snap.localRotation = Quaternion.Inverse(follow.rotation) * t.rotation;
                Vector3 ps = follow.lossyScale;
                snap.localScale = new Vector3(
                    t.lossyScale.x / Mathf.Max(0.0001f, ps.x),
                    t.lossyScale.y / Mathf.Max(0.0001f, ps.y),
                    t.lossyScale.z / Mathf.Max(0.0001f, ps.z)
                );
            }
        }

        return snap;
    }

    IEnumerator PlayRoutine(Snapshot snap)
    {
        playing = true;

        var root = new GameObject("WeaponStoreFX");

        // Parent to SpriteBody so the frozen frame tracks player movement/billboard.
        Transform follow = absorbTarget != null ? absorbTarget : null;
        if (snap.hasLocalFollow && follow != null)
        {
            root.transform.SetParent(follow, false);
            root.transform.localPosition = snap.localPosition;
            root.transform.localRotation = snap.localRotation;
            root.transform.localScale = Vector3.one;
        }
        else
        {
            root.transform.SetPositionAndRotation(snap.position, snap.rotation);
            root.transform.localScale = Vector3.one;
        }

        // --- Ghost: last frame frozen (no sprite change), follows player ---
        var ghostGo = new GameObject("Ghost");
        ghostGo.transform.SetParent(root.transform, false);
        ghostGo.transform.localPosition = Vector3.zero;
        ghostGo.transform.localRotation = Quaternion.identity;
        ghostGo.transform.localScale = snap.hasLocalFollow ? snap.localScale : snap.scale;

        var ghost = ghostGo.AddComponent<SpriteRenderer>();
        ghost.sprite = snap.sprite;
        ghost.color = snap.color;
        ghost.flipX = snap.flipX;
        ghost.flipY = snap.flipY;
        ghost.sortingLayerID = snap.sortingLayerId;
        ghost.sortingOrder = snap.sortingOrder + sortingOrderOffset;
        if (snap.sharedMaterial != null)
            ghost.sharedMaterial = snap.sharedMaterial;

        // Instance so ClipThreshold / flash don't leak onto the live WeaponBody material.
        Material ghostMat = ghost.material;
        bool hasClip = !string.IsNullOrEmpty(clipThresholdProperty)
                       && ghostMat != null
                       && ghostMat.HasProperty(clipThresholdProperty);
        bool hasFlash = !string.IsNullOrEmpty(flashColorProperty)
                        && ghostMat != null
                        && ghostMat.HasProperty(flashColorProperty);
        int clipId = hasClip ? Shader.PropertyToID(clipThresholdProperty) : 0;
        int flashId = hasFlash ? Shader.PropertyToID(flashColorProperty) : 0;

        if (hasFlash)
            ghostMat.SetColor(flashId, ghostFlash * ghostFlashIntensity);
        if (hasClip)
            ghostMat.SetFloat(clipId, ditherStart);

        // --- Particles: small blue motes that rise, then absorb into SpriteBody ---
        var psGo = new GameObject("Particles");
        psGo.transform.SetParent(root.transform, false);
        psGo.transform.localPosition = Vector3.zero;

        var ps = psGo.AddComponent<ParticleSystem>();
        ConfigureParticles(ps, snap);

        var renderer = psGo.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = snap.sortingLayerId;
        renderer.sortingOrder = snap.sortingOrder + sortingOrderOffset + 1;
        renderer.alignment = ParticleSystemRenderSpace.View;

        ps.Play(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);

            // Frame stays the same; only dissolve. Root is parented so it moves with player.
            if (hasClip)
                ghostMat.SetFloat(clipId, Mathf.Lerp(ditherStart, ditherEnd, u));
            if (hasFlash)
                ghostMat.SetColor(flashId, ghostFlash * ghostFlashIntensity);

            if (!hasClip)
            {
                var c = snap.color;
                c.a = snap.color.a * (1f - u);
                ghost.color = c;
            }

            Vector3 absorbPos = absorbTarget != null
                ? absorbTarget.position + Vector3.up * absorbHeightOffset
                : root.transform.position + Vector3.up * absorbHeightOffset;

            PullParticles(ps, absorbPos, u);

            yield return null;
        }

        if (root != null)
            Destroy(root);

        playing = false;
    }

    void ConfigureParticles(ParticleSystem ps, Snapshot snap)
    {
        // AddComponent starts playOnAwake; stop fully before mutating main.duration etc.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = duration;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
        main.startColor = particleStart;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(particleCount * 2, 64);
        // Slight float upward before scripted pull takes over.
        main.gravityModifier = -0.02f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)particleCount)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        // Emit from a tight volume near the weapon, not a large box.
        shape.position = Vector3.zero;
        if (snap.sprite != null)
        {
            var b = snap.sprite.bounds;
            shape.scale = new Vector3(
                Mathf.Max(0.03f, b.size.x * Mathf.Abs(snap.scale.x) * 0.35f),
                Mathf.Max(0.03f, b.size.y * Mathf.Abs(snap.scale.y) * 0.35f),
                Mathf.Max(0.02f, 0.04f * Mathf.Abs(snap.scale.z))
            );
        }
        else
        {
            shape.scale = new Vector3(0.06f, 0.1f, 0.04f);
        }

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(particleStart, 0f),
                new GradientColorKey(particleMid, 0.4f),
                new GradientColorKey(particleEnd, 1f)
            },
            new[]
            {
                new GradientAlphaKey(particleStart.a, 0f),
                new GradientAlphaKey(particleMid.a, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

        // Tiny upward drift; PullParticles yanks them into the body almost immediately.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-horizontalSpread, horizontalSpread);
        vel.y = new ParticleSystem.MinMaxCurve(riseSpeed * 0.4f, riseSpeed);
        vel.z = new ParticleSystem.MinMaxCurve(-horizontalSpread, horizontalSpread);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    void PullParticles(ParticleSystem ps, Vector3 target, float effectU)
    {
        if (ps == null || !ps.IsAlive(true))
            return;

        int max = ps.particleCount;
        if (max <= 0) return;

        var particles = new ParticleSystem.Particle[max];
        int count = ps.GetParticles(particles);
        float dt = Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            float lifeT = 1f - (particles[i].remainingLifetime
                                / Mathf.Max(0.01f, particles[i].startLifetime));

            Vector3 to = target - particles[i].position;
            float dist = to.magnitude;

            if (dist < 0.03f)
            {
                particles[i].remainingLifetime = 0f;
                continue;
            }

            // Pull into the player almost immediately — only a brief hint of rise.
            float intoMix = Mathf.SmoothStep(0f, 1f, Mathf.Max(lifeT * 1.8f, effectU * 1.5f));
            intoMix = Mathf.Clamp01(intoMix);

            Vector3 rise = Vector3.up * riseSpeed;
            Vector3 intoBody = to / dist * pullStrength;
            Vector3 desired = Vector3.Lerp(rise, intoBody, intoMix);

            particles[i].velocity = Vector3.Lerp(
                particles[i].velocity,
                desired,
                dt * (4f + intoMix * 10f)
            );

            // Hard suck: move position toward the body so they stay close to the player.
            float suck = suckRate * (0.35f + intoMix * 1.65f);
            particles[i].position = Vector3.Lerp(
                particles[i].position,
                target,
                1f - Mathf.Exp(-suck * dt)
            );

            if (dist < 0.35f)
            {
                var c = particles[i].startColor;
                particles[i].startColor = Color.Lerp(c, particleMid, 1f - dist / 0.35f);
            }
        }

        ps.SetParticles(particles, count);
    }

    Material GetParticleMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Particles/Unlit")
            ?? Shader.Find("Sprites/Default");

        particleMaterial = new Material(shader);
        if (particleMaterial.HasProperty("_Surface"))
            particleMaterial.SetFloat("_Surface", 1f); // Transparent
        if (particleMaterial.HasProperty("_Blend"))
            particleMaterial.SetFloat("_Blend", 0f); // Alpha
        if (particleMaterial.HasProperty("_ColorMode"))
            particleMaterial.SetFloat("_ColorMode", 0f);
        if (particleMaterial.HasProperty("_BaseColor"))
            particleMaterial.SetColor("_BaseColor", Color.white);
        if (particleMaterial.HasProperty("_Color"))
            particleMaterial.SetColor("_Color", Color.white);

        // Prefer additive-ish look when possible.
        if (particleMaterial.HasProperty("_BlendOp"))
            particleMaterial.SetFloat("_BlendOp", 0f);

        particleMaterial.renderQueue = (int)RenderQueue.Transparent;
        return particleMaterial;
    }

    void OnDestroy()
    {
        if (particleMaterial != null)
            Destroy(particleMaterial);
    }
}
