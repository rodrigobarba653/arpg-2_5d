using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TearsOfMathra
{
    [DisallowMultipleComponent]
    public sealed class MathraSteamVent : MonoBehaviour
    {
        public Material vaporMaterial, waterMaterial;
        public AudioClip warningHiss, pressureRoar;
        [Header("Steam appearance")]
        [Tooltip("Brightness offset for every layer in stops. Each step down halves the emitted RGB.")]
        [Range(-6, 0)] public float exposureStops = -1.5f;
        [Range(0, 1)] public float masterOpacity = .85f;

        [Range(0, 3)] public float particleDensity = 1f;

        [System.Serializable]
        public class Plume
        {
            [ColorUsage(false)] public Color tint;
            [Range(0,1)] public float brightness = 1;
            [Range(0,1)] public float opacity;
            [Min(.01f)] public float size;
            [Tooltip("Travel distance as a fraction of vent Reach.")]
            [Min(0)] public float travel;
            [Tooltip("Sideways travel as a multiple of vent Radius.")]
            [Min(0)] public float spread;
            [Min(.02f)] public float lifetime;
            [Tooltip("Particle diameter as a multiple of vent Radius. Size scales the whole plume.")]
            [Min(.01f)] public float diameter;
            [Tooltip("Particles per warning or shutdown burst, or particles per second for continuous layers.")]
            [Range(0,200)] public int amount;

            public Plume(Color color, float alpha, float size, float travel, float spread,
                         float lifetime, float diameter, int amount)
            {
                tint = color;
                opacity = alpha;
                this.size = size;
                this.travel = travel;
                this.spread = spread;
                this.lifetime = lifetime;
                this.diameter = diameter;
                this.amount = amount;
            }
        }

        public Plume warningOne = new Plume(new Color(0.76f, 0.84f, 0.88f), 0.5f, 0.8f, 0.2f, 0.45f, 0.53f, 0.7f, 14);
        public Plume warningTwo = new Plume(new Color(0.78f, 0.85f, 0.89f), 0.55f, 1.2f, 0.2f, 0.45f, 0.53f, 0.7f, 18);
        public Plume pressureCore = new Plume(new Color(0.95f, 0.86f, 0.7f), 0.73f, 1.0f, 0.75f, 0.33f, 0.22f, 0.39f, 70);
        public Plume rollingVapor = new Plume(new Color(0.78f, 0.85f, 0.91f), 0.49f, 1.0f, 1.0f, 0.95f, 0.53f, 1.1f, 84);
        public Plume waterSpray = new Plume(new Color(0.63f, 0.82f, 0.9f), 0.88f, 1.0f, 1.0f, 0.95f, 0.34f, 0.13f, 52);
        public Plume floatingSteam = new Plume(new Color(0.73f, 0.8f, 0.87f), 0.25f, 1.0f, 0.15f, 0.8f, 1.85f, 1.3f, 9);
        public Plume shutdownWisps = new Plume(new Color(0.73f, 0.8f, 0.87f), 0.28f, 1.0f, 0.12f, 0.9f, 2.4f, 0.85f, 14);
        public Plume shutdownDroplets = new Plume(new Color(0.69f, 0.84f, 0.91f), 0.65f, 1.0f, 0.12f, 0.8f, 0.5f, 0.09f, 8);

        [Header("Vent dimensions and timing")]
        [Min(.5f)] public float reach = 4f;
        [Min(.05f)] public float radius = .48f;
        [Min(0)] public float startDelay;
        public bool repeat = true;
        [Min(0)] public float cooldown = 3f;
        [Header("Player damage")]
        [Min(0)] public int damage = 10;
        public string playerTag = "Player";
        public string damageMethod = "TakeDamage";
        public LayerMask targetLayers = ~0;
        [System.Serializable] public class HitEvent : UnityEvent<GameObject,int> { }
        public HitEvent onPlayerHit = new HitEvent();

        ParticleSystem mist, core, drops, drift;
        AudioSource sound;
        readonly HashSet<GameObject> hit = new HashSet<GameObject>();
        Collider[] candidates = new Collider[16];
        float clock, vaporBudget, coreBudget, waterBudget, driftBudget;
        bool secondWarning, started, released, finished, firstWarning;

        void Awake()
        {
            mist = Layer("Rolling vapor", vaporMaterial, false, 220);
            core = Layer("Pressure core", vaporMaterial, true, 120);
            drops = Layer("Condensate", waterMaterial, true, 160);
            drift = Layer("Drifting wisps", vaporMaterial, false, 70);
            var force = drift.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.y = .42f;
            var main = drops.main;
            main.gravityModifier = .55f;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 1;
            sound.minDistance = 2;
            sound.maxDistance = 18;
        }

        ParticleSystem Layer(string label, Material mat, bool stretch, int limit)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var ps = child.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = limit;
            main.playOnAwake = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = stretch ? .035f : 0;
            renderer.lengthScale = 1.2f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .62f, 1, 1.5f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0), new GradientAlphaKey(1,.08f),new GradientAlphaKey(.8f,.42f), new GradientAlphaKey(0,1)});
            fade.color = gradient;
            return ps;
        }

        void OnEnable()
        {
            clock = -startDelay;
            secondWarning = started = released = finished = firstWarning = false;
            vaporBudget = coreBudget = waterBudget = driftBudget = 0;
            hit.Clear();
            foreach (var ps in new[]{mist,core,drops,drift}) ps.Play(false);
        }

        void OnDisable()
        {
            sound.Stop();
            foreach (var ps in new[]{mist,core,drops,drift})
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Spray(ParticleSystem ps, int count, Plume plume, float offset = 0)
        {
            float scale = plume.size;
            float distance = reach * plume.travel * scale;
            float spread = radius * plume.spread * scale;
            float width = radius * plume.diameter * scale;
            Color tint = plume.tint * (Mathf.Pow(2f, exposureStops) * plume.brightness);
            tint.a = plume.opacity * masterOpacity;
            for (int i=0; i<count; i++)
            {
                Vector2 lateral = Random.insideUnitCircle;
                Vector3 sideways = transform.right*lateral.x + transform.up*lateral.y;
                float lifetime = plume.lifetime*Random.Range(.85f,1.15f);
                var p = new ParticleSystem.EmitParams {
                    position = transform.position + transform.forward*offset + sideways*radius*.34f*scale,
                    velocity = (transform.forward*distance + sideways*spread)/lifetime,
                    startLifetime = lifetime, startSize = width*Random.Range(.70f,1.25f),
                    rotation = Random.Range(0f, 360f), startColor = tint
                };
                ps.Emit(p,1);
            }
        }
        void Warn(Plume plume, float volume)
        {
            Spray(mist, Mathf.RoundToInt(plume.amount * particleDensity), plume);
            if(warningHiss) sound.PlayOneShot(warningHiss, volume);
        }
        int Count(ref float budget, float rate, float dt)
        {
            budget += rate * particleDensity * dt;
            int n = Mathf.FloorToInt(budget);
            budget -= n;
            return n;
        }
        void Update()
        {
            if (finished) return;
            float prev = clock;
            clock += Time.deltaTime;
            if (clock < 0) return;
            if (!firstWarning) { Warn(warningOne, .46f); firstWarning = true; }
            if (!secondWarning && clock >= 1) { Warn(warningTwo, .70f); secondWarning = true; }
            if (!started && clock >= 2)
            {
                started = true;
                if(pressureRoar) sound.PlayOneShot(pressureRoar,.85f);
            }
            float dt = Mathf.Clamp(clock,2,5)-Mathf.Clamp(prev,2,5);
            if(dt>0)
            {
                float pulse = 1+.16f*Mathf.Sin(clock*29);
                Spray(core, Count(ref coreBudget, pressureCore.amount * pulse, dt), pressureCore);
                Spray(mist, Count(ref vaporBudget, rollingVapor.amount * pulse, dt), rollingVapor);
                Spray(drops, Count(ref waterBudget, waterSpray.amount, dt), waterSpray);
                Spray(drift, Count(ref driftBudget, floatingSteam.amount, dt), floatingSteam,
                    Random.Range(.2f, .85f) * reach);
            }
            if (!released && clock >= 5)
            {
                released = true;
                Spray(drift, Mathf.RoundToInt(shutdownWisps.amount * particleDensity), shutdownWisps);
                Spray(drops, Mathf.RoundToInt(shutdownDroplets.amount * particleDensity), shutdownDroplets);
            }
            if(clock >= 5+cooldown)
            {
                if(!repeat) { finished = true; return; }
                clock = 0;
                firstWarning = secondWarning = started = released = false;
                hit.Clear();
            }
        }

        void FixedUpdate()
        {
            if(clock < 2 || clock >= 5 || finished) return;
            float length = reach*Mathf.Clamp01((clock-2)/.25f);
            if(length <= 0) return;
            Vector3 center = transform.position+transform.forward*(length*.5f);
            Vector3 extents = new Vector3(radius,radius,length*.5f);
            int n;
            while(true)
            {
                n = Physics.OverlapBoxNonAlloc(center,extents,candidates,transform.rotation,targetLayers,QueryTriggerInteraction.Collide);
                if(n<candidates.Length) break;
                System.Array.Resize(ref candidates,candidates.Length*2);
            }
            for(int i=0; i<n; i++)
            {
                Transform target = candidates[i].transform;
                while(target && !target.CompareTag(playerTag)) target = target.parent;
                if(!target || !hit.Add(target.gameObject)) continue;
                if(!string.IsNullOrEmpty(damageMethod))
                    ApplyDamage(target);
                onPlayerHit.Invoke(target.gameObject,damage);
            }
        }

        // Match the receiver signature before calling it. Unity messages cannot convert
        // a boxed integer into a float or fill omitted optional arguments.
        bool warnedAboutReceiver;
        void ApplyDamage(Transform target)
        {
            bool delivered = false;
            foreach (var receiver in target.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (!receiver) continue;
                System.Reflection.MethodInfo best = null;
                int bestRank = int.MaxValue;
                foreach (var method in receiver.GetType().GetMethods(
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic))
                {
                    if (method.Name != damageMethod || method.ContainsGenericParameters) continue;
                    var args = method.GetParameters();
                    if (args.Length == 0) continue;
                    var type = args[0].ParameterType;
                    if (type != typeof(int) && type != typeof(float)) continue;
                    bool supported = true;
                    for (int j = 1; j < args.Length; j++) supported &= args[j].IsOptional;
                    int rank = args.Length * 2 + (type == typeof(int) ? 0 : 1);
                    if (supported && rank < bestRank) { best = method; bestRank = rank; }
                }
                if (best == null) continue;
                var parameters = best.GetParameters();
                var values = new object[parameters.Length];
                values[0] = parameters[0].ParameterType == typeof(int) ? (object)damage : (float)damage;
                for (int j = 1; j < values.Length; j++) values[j] = parameters[j].DefaultValue;
                best.Invoke(receiver, values);
                delivered = true;
            }
            if (!delivered && !warnedAboutReceiver)
            {
                warnedAboutReceiver = true;
                Debug.LogWarning($"Steam vent: no compatible {damageMethod}(int or float) on {target.name} or its parents. Check the player health method or connect On Player Hit.", this);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position,transform.rotation,Vector3.one);
            Gizmos.color = new Color(1,.68f,.28f);
            Gizmos.DrawWireCube(Vector3.forward*reach*.5f,new Vector3(radius*2,radius*2,reach));
            Gizmos.DrawLine(Vector3.zero,Vector3.forward*reach);
        }
    }
}

#if UNITY_EDITOR
namespace TearsOfMathra
{
    [UnityEditor.CustomEditor(typeof(MathraSteamVent)), UnityEditor.CanEditMultipleObjects]
    public sealed class MathraSteamVentInspector : UnityEditor.Editor
    {
        bool advanced;

        void Field(string path, string label)
        {
            var property = serializedObject.FindProperty(path);
            UnityEditor.EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UnityEditor.EditorGUILayout.LabelField("Tears of Mathra — Steam Art Director", UnityEditor.EditorStyles.boldLabel);
            Field("warningOne.size", "Warning Burst 1 Size");
            Field("warningTwo.size", "Warning Burst 2 Size");
            Field("warningOne.tint", "Warning Burst 1 Color");
            Field("warningTwo.tint", "Warning Burst 2 Color");
            Field("pressureCore.tint", "Steam Core Color");
            Field("rollingVapor.tint", "Rolling Vapor Color");
            Field("waterSpray.tint", "Condensate Color");
            Field("floatingSteam.tint", "Drifting Wisps Color");
            Field("shutdownWisps.tint", "Shutdown Wisps Color");
            Field("shutdownDroplets.tint", "Shutdown Droplets Color");
            Field("exposureStops", "Steam Brightness (EV)");
            Field("masterOpacity", "Steam Opacity");
            Field("pressureCore.diameter", "Pressure Core Size");
            Field("particleDensity", "Particle Density");

            advanced = UnityEditor.EditorGUILayout.Foldout(advanced, "Advanced particle settings", true);
            if (advanced)
            {
                foreach (var name in new[] { "warningOne", "warningTwo", "pressureCore", "rollingVapor",
                         "waterSpray", "floatingSteam", "shutdownWisps", "shutdownDroplets" })
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
            }
            DrawPropertiesExcluding(serializedObject, "m_Script", "warningOne", "warningTwo", "pressureCore",
                "rollingVapor", "waterSpray", "floatingSteam", "shutdownWisps", "shutdownDroplets",
                "exposureStops", "masterOpacity", "particleDensity");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
