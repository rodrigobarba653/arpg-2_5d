using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Valcyr.Environment
{
    [DisallowMultipleComponent]
    public sealed class ValcyrCurtainPassage : MonoBehaviour
    {
        [SerializeField] MeshFilter curtain;
        [SerializeField, HideInInspector] MeshFilter shadowCurtain;
        [Header("Passage")]
        [Tooltip("CharacterControllers, moving rigidbodies, and Player-tagged collider hierarchies on these layers can open the door.")]
        [SerializeField] LayerMask actorLayers = ~0;
        [Tooltip("Optional layers for characters moved directly by Transform, with an untagged 3D collider.")]
        [SerializeField] LayerMask extraActorLayers;
        [Tooltip("Half-width of the character in door-local metres.")]
        [SerializeField, Range(.15f, 1.1f)] float bodyRadius = .43f;
        [SerializeField, Range(.12f, 1.1f)] float pushDepth = .68f;
        [SerializeField, Range(0f, .85f)] float sideParting = .44f;
        [Header("PVC response")]
        [SerializeField, Range(.65f, 2.5f)] float swingFrequency = 1.6f;
        [SerializeField, Range(.28f, 1f)] float damping = .55f;

        [Header("Movement sound")]
        [Tooltip("One short rustle per brush through the PVC. Clear the clip for a silent door.")]
        [SerializeField] AudioClip movementClip;
        [SerializeField, Range(0f, 1f)] float soundVolume = .65f;
        [SerializeField, Range(.06f, .8f)] float soundCooldown = .22f;
        [SerializeField, Min(1f)] float soundDistance = 24f;
        AudioSource emitter;
        float nextSoundTime;

        // Matches the baked mesh: strip-major, top to bottom, left/centre/right.
        const int Strips = 16, Rows = 11, Columns = 3, PerStrip = Rows * Columns;
        struct Contact
        {
            public Collider collider;
            public float direction;
            public float previousZ;
            public bool brushed;
            public Vector2 lastBrush;
            public float brushDirection;
        }
        struct Strip
        {
            public Vector2 upper, tip, upperSpeed, tipSpeed;
            public float centre, length;
        }

        readonly List<Contact> contacts = new List<Contact>(4);
        readonly Strip[] state = new Strip[Strips];
        readonly Vector2[] targets = new Vector2[Strips];
        Mesh shared, moving;
        Vector3[] rest, positions, normals;
        Coroutine animationLoop;

        bool Accepts(Collider other)
        {
            int layer = 1 << other.gameObject.layer;
            if ((actorLayers.value & layer) == 0 || other.isTrigger) return false;
            if ((extraActorLayers.value & layer) != 0 || other.attachedRigidbody ||
                other.GetComponentInParent<CharacterController>()) return true;
            for (Transform p = other.transform; p; p = p.parent)
                if (p.CompareTag("Player")) return true;
            return false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || !curtain || !Accepts(other)) return;
            for (int i = 0; i < contacts.Count; i++)
                if (contacts[i].collider == other) return;
            float z = transform.InverseTransformPoint(other.bounds.center).z;
            contacts.Add(new Contact { collider = other, previousZ = z, direction = z < 0f ? 1f : -1f });
            if (animationLoop == null)
            {
                PrepareMesh();
                animationLoop = StartCoroutine(Animate());
            }
        }

        void OnTriggerExit(Collider other)
        {
            for (int i = contacts.Count - 1; i >= 0; i--)
                if (contacts[i].collider == other) contacts.RemoveAt(i);
        }

        void PrepareMesh()
        {
            if (moving) return;
            shared = curtain.sharedMesh;
            moving = Instantiate(shared);
            moving.name = "PVC strips (moving)";
            moving.MarkDynamic();
            rest = shared.vertices;
            positions = (Vector3[])rest.Clone();
            normals = shared.normals;
            for (int s = 0; s < Strips; s++)
            {
                int top = s * PerStrip + 1;
                state[s].centre = rest[top].x;
                state[s].length = rest[top].y - rest[top + PerStrip - Columns].y;
            }
        }

        void UpdateTargets()
        {
            System.Array.Clear(targets, 0, Strips);
            for (int i = contacts.Count - 1; i >= 0; i--)
            {
                Contact c = contacts[i];
                if (!c.collider || !c.collider.enabled || !c.collider.gameObject.activeInHierarchy)
                {
                    contacts.RemoveAt(i);
                    continue;
                }
                Vector3 p = transform.InverseTransformPoint(c.collider.bounds.center);
                float dz = p.z - c.previousZ;
                if (Mathf.Abs(dz) > .002f) c.direction = Mathf.Sign(dz);
                c.previousZ = p.z;

                float distance = Mathf.Clamp01(1f - Mathf.Abs(p.z) / .84f);
                float engagement = distance * distance * (3f - 2f * distance);
                float strongest = 0f;
                for (int s = 0; s < Strips; s++)
                {
                    float dx = state[s].centre - p.x;
                    float w = Mathf.Clamp01(1f - Mathf.Abs(dx) / (bodyRadius + .24f));
                    w = w * w * (3f - 2f * w) * engagement;
                    strongest = Mathf.Max(strongest, w);
                    Vector2 push = new Vector2((dx < 0f ? -1f : 1f) * sideParting, c.direction * pushDepth) * w;
                    if (push.sqrMagnitude > targets[s].sqrMagnitude) targets[s] = push;
                }
                if (movementClip && soundVolume > 0f)
                    CheckBrush(ref c, p, strongest);
                contacts[i] = c;
            }
        }

        void CheckBrush(ref Contact contact, Vector3 localPosition, float engagement)
        {
            if (engagement < .08f)
                contact.brushed = false;
            if (engagement < .24f) return;
            Vector2 point = new Vector2(localPosition.x, localPosition.z);
            bool reversed = contact.direction != contact.brushDirection &&
                            Mathf.Abs(point.y - contact.lastBrush.y) > .15f;
            if (contact.brushed && Mathf.Abs(point.x - contact.lastBrush.x) < .55f && !reversed) return;

            contact.brushed = true;
            contact.lastBrush = point;
            contact.brushDirection = contact.direction;
            if (Time.time < nextSoundTime) return;

            if (!emitter)
            {
                var sourceObject = new GameObject("PVC movement audio");
                sourceObject.transform.SetParent(transform, false);
                emitter = sourceObject.AddComponent<AudioSource>();
                emitter.playOnAwake = false;
                emitter.loop = false;
                emitter.spatialBlend = 1f;
                emitter.dopplerLevel = 0f;
                emitter.rolloffMode = AudioRolloffMode.Linear;
                emitter.minDistance = 1f;
            }
            emitter.transform.localPosition = new Vector3(localPosition.x, Mathf.Clamp(localPosition.y, .25f, 2.3f), 0f);
            emitter.maxDistance = Mathf.Max(1.01f, soundDistance);
            emitter.PlayOneShot(movementClip, soundVolume);
            nextSoundTime = Time.time + soundCooldown;
        }

        void ShowMesh(Mesh mesh)
        {
            curtain.sharedMesh = mesh;
            if (shadowCurtain) shadowCurtain.sharedMesh = mesh;
        }

        static void Spring(ref Vector2 value, ref Vector2 speed, Vector2 goal, float omega, float drag, float dt)
        {
            speed += ((goal - value) * (omega * omega) - speed * (2f * drag * omega)) * dt;
            value += speed * dt;
        }

        IEnumerator Animate()
        {
            ShowMesh(moving);
            while (true)
            {
                UpdateTargets();
                float dt = Mathf.Min(Time.deltaTime, .05f);
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 100f));
                bool swinging = false;
                for (int s = 0; s < Strips; s++)
                {
                    Strip strip = state[s];
                    float hz = 2f * Mathf.PI * swingFrequency * (1f + ((s * 7) % 9 - 4) * .019f);
                    for (int step = 0; step < steps; step++)
                    {
                        Spring(ref strip.upper, ref strip.upperSpeed, targets[s], hz * 1.48f, damping, dt / steps);
                        Spring(ref strip.tip, ref strip.tipSpeed, strip.upper, hz, damping, dt / steps);
                    }
                    state[s] = strip;
                    swinging |= strip.upper.sqrMagnitude + strip.tip.sqrMagnitude > .000003f ||
                                strip.upperSpeed.sqrMagnitude + strip.tipSpeed.sqrMagnitude > .0001f;
                }
                if (contacts.Count == 0 && !swinging) break;
                Bend();
                yield return null;
            }
            ResetPose();
            animationLoop = null;
        }

        void Bend()
        {
            for (int s = 0; s < Strips; s++)
            {
                Strip strip = state[s];
                Vector2 prevOffset = Vector2.zero;
                float y = 0f;
                float segment = strip.length / (Rows - 1);
                for (int r = 0; r < Rows; r++)
                {
                    float t = r / (float)(Rows - 1);
                    float shoulder = Mathf.Clamp01(t / .68f);
                    float bend = shoulder * shoulder * (3f - 2f * shoulder) * (.86f + .14f * t);
                    Vector2 offset = bend * Vector2.Lerp(strip.upper, strip.tip, t);
                    Vector2 delta = Vector2.ClampMagnitude(offset - prevOffset, segment * .90f);
                    if (r > 0) y -= Mathf.Sqrt(Mathf.Max(0f, segment * segment - delta.sqrMagnitude));
                    prevOffset += delta;
                    int first = s * PerStrip + r * Columns;
                    for (int col = 0; col < Columns; col++)
                    {
                        int k = first + col;
                        Vector3 p = rest[k];
                        p.x += prevOffset.x;
                        p.y += y + segment * r;
                        p.z += prevOffset.y;
                        positions[k] = p;
                    }
                }
                for (int r = 0; r < Rows; r++)
                {
                    int first = s * PerStrip + r * Columns;
                    int above = r == 0 ? first : first - Columns;
                    int below = r == Rows - 1 ? first : first + Columns;
                    for (int col = 0; col < Columns; col++)
                    {
                        Vector3 across = positions[first + Mathf.Min(col + 1, 2)] - positions[first + Mathf.Max(col - 1, 0)];
                        normals[first + col] = Vector3.Cross(across, positions[below + col] - positions[above + col]).normalized;
                    }
                }
            }
            // Opposite normals on the reverse face let either side catch a spotlight.
            int frontCount = Strips * PerStrip;
            if (positions.Length == 2 * frontCount)
            {
                System.Array.Copy(positions, 0, positions, frontCount, frontCount);
                for (int i = 0; i < frontCount; i++) normals[i + frontCount] = -normals[i];
            }
            moving.SetVertices(positions, 0, positions.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            moving.SetNormals(normals, 0, normals.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
        }

        void ResetPose()
        {
            if (curtain && shared) ShowMesh(shared);
            for (int s = 0; s < Strips; s++)
            {
                state[s].upper = state[s].tip = Vector2.zero;
                state[s].upperSpeed = state[s].tipSpeed = Vector2.zero;
            }
        }

        void OnDisable()
        {
            if (animationLoop != null) StopCoroutine(animationLoop);
            animationLoop = null;
            contacts.Clear();
            if (emitter) emitter.Stop();
            nextSoundTime = 0f;
            ResetPose();
        }

        void OnDestroy()
        {
            if (moving) Destroy(moving);
        }
    }
}
