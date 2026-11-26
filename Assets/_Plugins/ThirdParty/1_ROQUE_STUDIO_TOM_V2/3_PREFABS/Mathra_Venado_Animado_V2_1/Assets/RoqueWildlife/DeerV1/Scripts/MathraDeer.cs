using UnityEngine;

namespace Roque.Wildlife
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Roque/Wildlife/Mathra Deer")]
    public sealed class MathraDeer : MonoBehaviour
    {
        public enum Behaviour { Drinking, Looking, Fleeing }
        [Header("References")]
        public Animator animator;
        public SpriteRenderer spriteRenderer;
        [Tooltip("Valid imported Sprite used if an external or broken animation assigns None.")]
        public Sprite fallbackSprite;
        public Transform visual;
        [Tooltip("Assign Ax and/or Jyn here. Only active objects are considered.")]
        public Transform[] threats = new Transform[0];
        [Tooltip("Optional. Otherwise uses Camera.main.")]
        public Camera viewCamera;

        [Header("Starting behaviour")]
        public Behaviour startBehaviour = Behaviour.Drinking;
        [Tooltip("When starting in Fleeing mode, first stand still for this many seconds, then react and run.")]
        [Min(0f)] public float standBeforeRunning = 1.2f;
        public bool alternateDrinkAndLook = true;
        [Min(0.1f)] public float drinkingSeconds = 6f;
        [Min(0.1f)] public float lookingSeconds = 4f;
        [Range(0.1f, 3f)] public float animationSpeed = 1f;

        [Header("Scare detection")]
        public bool detectNearbyPlayers = true;
        [Tooltip("Finds active objects with the built-in Player tag when Threats is empty.")]
        public bool autoFindPlayerTag = true;
        [Min(0f)] public float scareRadius = 3.5f;
        [Min(0f)] public float maximumHeightDifference = 2f;
        [Min(0f)] public float alertSeconds = 0.25f;
        [Min(0f)] public float scareCooldown = 4f;

        [Header("Escape")]
        [Tooltip("Animation only, useful for Timeline shots or an external movement script.")]
        public bool runInPlace;
        [Min(0.01f)] public float runSpeed = 4f;
        [Min(0.01f)] public float acceleration = 12f;
        [Min(0.1f)] public float fleeDistance = 9f;
        [Tooltip("Optional destination. Otherwise runs away from the threat.")]
        public Transform escapeTarget;
        [Tooltip("Keeps motion left/right relative to the camera, matching the profile sprites.")]
        public bool constrainToCameraRight = true;

        [Header("Ground and obstacles (3D physics)")]
        public bool followGround = true;
        public LayerMask groundLayers = -1;
        public LayerMask obstacleLayers = -1;
        public bool avoidObstacles = true;
        [Min(0.01f)] public float obstacleRadius = 0.2f;
        [Min(0.01f)] public float obstacleHeight = 0.6f;
        [Min(0.01f)] public float groundProbeHeight = 1.5f;
        [Min(0.01f)] public float maximumStepUp = 0.45f;
        [Min(0.01f)] public float maximumDrop = 0.7f;
        [Range(0f, 80f)] public float maximumSlope = 50f;
        [Tooltip("Distance from root/pivot to floor. Keep 0 for the supplied sprites.")]
        public float groundOffset;

        [Header("Billboard")]
        public bool faceCamera = true;
        [Tooltip("Off: faces the full camera plane. On: stays upright on world Y.")]
        public bool uprightBillboard;

        public Behaviour CurrentBehaviour { get; private set; }
        public bool IsAlerting { get { return alerting || poseTransition == PoseTransition.RaiseToScare || poseTransition == PoseTransition.Launch; } }
        private static readonly int DrinkHash = Animator.StringToHash("Base Layer.Drinking");
        private static readonly int LookHash = Animator.StringToHash("Base Layer.Looking");
        private static readonly int RunHash = Animator.StringToHash("Base Layer.Running");
        private static readonly int AlertHash = Animator.StringToHash("Base Layer.Alert");
        private static readonly int RaiseHash = Animator.StringToHash("Base Layer.RaiseHead");
        private static readonly int LowerHash = Animator.StringToHash("Base Layer.LowerHead");
        private static readonly int LaunchHash = Animator.StringToHash("Base Layer.StartRun");
        private enum PoseTransition { None, RaiseToLook, LowerToDrink, RaiseToScare, Launch }
        private PoseTransition poseTransition;
        private float poseTime;
        private bool autoRunPending;
        private float autoRunTimer;
        private Transform[] foundPlayers = new Transform[0];
        private Vector3 direction;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialVisualScale;
        private bool initialFlip;
        private bool alerting;
        private bool started;
        private Sprite lastValidSprite;
        private bool warnedEmptySprite;
        private float stateTime, movedDistance, currentSpeed, nextDetection, nextRefresh, nextAllowedScare;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!visual && spriteRenderer) visual = spriteRenderer.transform;
            initialPosition = transform.position;
            initialRotation = visual ? visual.localRotation : Quaternion.identity;
            initialVisualScale = visual ? visual.localScale : Vector3.one;
            initialFlip = spriteRenderer && spriteRenderer.flipX;
            lastValidSprite = spriteRenderer && spriteRenderer.sprite ? spriteRenderer.sprite : fallbackSprite;
            if (spriteRenderer && !spriteRenderer.sprite && lastValidSprite) spriteRenderer.sprite = lastValidSprite;
        }

        private void OnEnable()
        {
            if (started) InitializeBehaviour();
        }

        private void Start()
        {
            started = true;
            InitializeBehaviour();
        }

        private void InitializeBehaviour()
        {
            nextDetection = 0f;
            nextRefresh = 0f;
            nextAllowedScare = 0f;
            alerting = false;
            poseTransition = PoseTransition.None;
            autoRunPending = false;
            if (startBehaviour == Behaviour.Fleeing)
            {
                SetCalm(Behaviour.Looking);
                autoRunPending = true;
                autoRunTimer = standBeforeRunning;
            }
            else SetCalm(startBehaviour);
        }

        private Camera ActiveCamera()
        {
            if (!viewCamera || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            return viewCamera;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (animator) animator.speed = animationSpeed;
            if (autoRunPending)
            {
                autoRunTimer -= dt;
                if (autoRunTimer <= 0f)
                {
                    autoRunPending = false;
                    RunNow();
                }
            }
            if (detectNearbyPlayers && CurrentBehaviour != Behaviour.Fleeing &&
                !autoRunPending && !alerting && poseTransition == PoseTransition.None &&
                Time.time >= nextAllowedScare && Time.time >= nextDetection)
            {
                nextDetection = Time.time + 0.15f;
                CheckThreats();
            }
            stateTime += dt;
            if (poseTransition != PoseTransition.None)
            {
                poseTime += dt * animationSpeed;
                float duration = poseTransition == PoseTransition.Launch ? 0.24f : 0.48f;
                if (poseTime >= duration) CompletePoseTransition();
                return;
            }
            if (alerting)
            {
                if (stateTime >= alertSeconds)
                {
                    CurrentBehaviour = Behaviour.Fleeing;
                    BeginPoseTransition(PoseTransition.Launch, LaunchHash);
                }
                return;
            }
            if (CurrentBehaviour == Behaviour.Fleeing)
            {
                if (!runInPlace) MoveEscape(dt);
                return;
            }
            if (alternateDrinkAndLook && !autoRunPending)
            {
                float duration = CurrentBehaviour == Behaviour.Drinking ? drinkingSeconds : lookingSeconds;
                if (stateTime >= duration)
                    RequestCalm(CurrentBehaviour == Behaviour.Drinking ? Behaviour.Looking : Behaviour.Drinking);
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer)
            {
                if (spriteRenderer.sprite) lastValidSprite = spriteRenderer.sprite;
                else
                {
                    if (lastValidSprite || fallbackSprite) spriteRenderer.sprite = lastValidSprite ? lastValidSprite : fallbackSprite;
                    if (!warnedEmptySprite)
                    {
                        warnedEmptySprite = true;
                        Debug.LogWarning("Mathra Deer: an animation assigned Sprite None. Holding a valid frame. Exit Play and use Roque > Wildlife > Deer > Repair NONE keyframes and prefabs.", this);
                    }
                }
            }
            Camera cam = ActiveCamera();
            if (!faceCamera || !cam || !visual) return;
            Vector3 forward = cam.transform.forward;
            if (uprightBillboard) forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
                visual.rotation = Quaternion.LookRotation(forward, uprightBillboard ? Vector3.up : cam.transform.up);
        }

        private void CheckThreats()
        {
            Transform[] candidates = threats;
            if ((candidates == null || candidates.Length == 0) && autoFindPlayerTag)
            {
                if (Time.time >= nextRefresh)
                {
                    nextRefresh = Time.time + 1f;
                    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                    foundPlayers = new Transform[players.Length];
                    for (int i = 0; i < players.Length; i++) foundPlayers[i] = players[i].transform;
                }
                candidates = foundPlayers;
            }
            if (candidates == null) return;
            Transform nearest = null;
            float nearestSqr = scareRadius * scareRadius;
            foreach (Transform candidate in candidates)
            {
                if (!candidate || !candidate.gameObject.activeInHierarchy || candidate == transform) continue;
                Vector3 delta = candidate.position - transform.position;
                if (Mathf.Abs(delta.y) > maximumHeightDifference) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude < nearestSqr)
                {
                    nearest = candidate;
                    nearestSqr = delta.sqrMagnitude;
                }
            }
            if (nearest) Scare(nearest);
        }

        public void Scare(Transform source)
        {
            if (source) ScareFromPosition(source.position);
            else ScareNow();
        }

        public void ScareFromPosition(Vector3 sourcePosition)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsAlerting || CurrentBehaviour == Behaviour.Fleeing) return;
            SetEscapeDirection(transform.position - sourcePosition);
            RequestScare();
        }

        [ContextMenu("Preview/Scare now (Play Mode)")]
        public void ScareNow()
        {
            if (!Application.isPlaying) return;
            Camera cam = ActiveCamera();
            Vector3 right = cam ? cam.transform.right : Vector3.right;
            ScareFromPosition(transform.position + right * 2f);
        }

        [ContextMenu("Preview/Drink (Play Mode)")]
        public void DrinkNow() { if (Application.isPlaying) RequestCalm(Behaviour.Drinking); }

        [ContextMenu("Preview/Look around (Play Mode)")]
        public void LookNow() { if (Application.isPlaying) RequestCalm(Behaviour.Looking); }

        [ContextMenu("Preview/Run (Play Mode)")]
        public void RunNow()
        {
            if (!Application.isPlaying) return;
            if (IsAlerting || CurrentBehaviour == Behaviour.Fleeing) return;
            Camera cam = ActiveCamera();
            SetEscapeDirection(cam ? -cam.transform.right : Vector3.left);
            RequestScare();
        }

        [ContextMenu("Preview/Reset position (Play Mode)")]
        public void ResetDeer()
        {
            if (!Application.isPlaying) return;
            transform.position = initialPosition;
            if (visual) visual.localRotation = initialRotation;
            if (visual) visual.localScale = initialVisualScale;
            if (spriteRenderer) spriteRenderer.flipX = initialFlip;
            InitializeBehaviour();
            nextAllowedScare = Time.time + scareCooldown;
        }

        private void SetEscapeDirection(Vector3 away)
        {
            if (escapeTarget) away = escapeTarget.position - transform.position;
            away.y = 0f;
            Camera cam = ActiveCamera();
            Vector3 screenRight = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            if (screenRight.sqrMagnitude < 0.01f) screenRight = Vector3.right;
            if (away.sqrMagnitude < 0.001f) away = -screenRight;
            if (constrainToCameraRight && !escapeTarget)
                away = screenRight * (Vector3.Dot(away, screenRight) > 0f ? 1f : -1f);
            direction = away.normalized;
            // Source art faces left; flip it for motion toward screen-right.
            // Transform mirroring works with custom URP sprite shaders as well.
            if (spriteRenderer) spriteRenderer.flipX = false;
            if (visual)
            {
                Vector3 scale = visual.localScale;
                scale.x = Mathf.Abs(initialVisualScale.x) * (Vector3.Dot(direction, screenRight) > 0f ? -1f : 1f);
                visual.localScale = scale;
            }
        }

        private void BeginRun()
        {
            poseTransition = PoseTransition.None;
            alerting = false;
            CurrentBehaviour = Behaviour.Fleeing;
            stateTime = 0f;
            currentSpeed = 0f;
            movedDistance = 0f;
            Play(RunHash);
        }

        private void SetCalm(Behaviour behaviour)
        {
            poseTransition = PoseTransition.None;
            autoRunPending = false;
            alerting = false;
            CurrentBehaviour = behaviour;
            stateTime = 0f;
            currentSpeed = 0f;
            Play(behaviour == Behaviour.Drinking ? DrinkHash : LookHash);
        }

        private void RequestCalm(Behaviour target)
        {
            autoRunPending = false;
            if (target == Behaviour.Looking && CurrentBehaviour == Behaviour.Drinking)
                BeginPoseTransition(PoseTransition.RaiseToLook, RaiseHash);
            else if (target == Behaviour.Drinking && CurrentBehaviour != Behaviour.Drinking)
                BeginPoseTransition(PoseTransition.LowerToDrink, LowerHash);
            else SetCalm(target);
        }

        private void RequestScare()
        {
            autoRunPending = false;
            if (CurrentBehaviour == Behaviour.Drinking)
                BeginPoseTransition(PoseTransition.RaiseToScare, RaiseHash);
            else BeginAlert();
        }

        private void BeginAlert()
        {
            poseTransition = PoseTransition.None;
            CurrentBehaviour = Behaviour.Looking;
            alerting = true;
            stateTime = 0f;
            Play(AlertHash);
        }

        private void BeginPoseTransition(PoseTransition next, int clipHash)
        {
            autoRunPending = false;
            alerting = false;
            poseTransition = next;
            poseTime = 0f;
            stateTime = 0f;
            Play(clipHash);
        }

        private void CompletePoseTransition()
        {
            PoseTransition completed = poseTransition;
            poseTransition = PoseTransition.None;
            switch (completed)
            {
                case PoseTransition.RaiseToLook: SetCalm(Behaviour.Looking); break;
                case PoseTransition.LowerToDrink: SetCalm(Behaviour.Drinking); break;
                case PoseTransition.RaiseToScare: BeginAlert(); break;
                case PoseTransition.Launch: BeginRun(); break;
            }
        }

        private void Play(int stateHash)
        {
            if (animator && animator.runtimeAnimatorController) animator.Play(stateHash, 0, 0f);
        }

        private void MoveEscape(float dt)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, runSpeed, acceleration * dt);
            float step = Mathf.Min(currentSpeed * dt, Mathf.Max(0f, fleeDistance - movedDistance));
            if (escapeTarget)
            {
                Vector3 toTarget = Vector3.ProjectOnPlane(escapeTarget.position - transform.position, Vector3.up);
                if (toTarget.magnitude <= 0.1f) { EndEscape(); return; }
                SetEscapeDirection(toTarget);
                step = Mathf.Min(step, toTarget.magnitude);
            }
            Vector3 candidate = transform.position + direction * step;
            if (avoidObstacles && step > 0f)
            {
                Vector3 origin = transform.position + Vector3.up * obstacleHeight;
                if (Physics.SphereCast(origin, obstacleRadius, direction, out RaycastHit obstacle,
                    step + 0.05f, obstacleLayers, QueryTriggerInteraction.Ignore))
                { EndEscape(); return; }
            }
            if (followGround)
            {
                Vector3 origin = candidate + Vector3.up * groundProbeHeight;
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floor,
                    groundProbeHeight + maximumDrop + Mathf.Abs(groundOffset) + 0.1f,
                    groundLayers, QueryTriggerInteraction.Ignore))
                { EndEscape(); return; }
                float newY = floor.point.y + groundOffset;
                float delta = newY - transform.position.y;
                if (delta > maximumStepUp || delta < -maximumDrop ||
                    Vector3.Angle(floor.normal, Vector3.up) > maximumSlope)
                { EndEscape(); return; }
                candidate.y = newY;
            }
            transform.position = candidate;
            movedDistance += step;
            if (movedDistance >= fleeDistance - 0.001f) EndEscape();
        }

        private void EndEscape()
        {
            nextAllowedScare = Time.time + scareCooldown;
            SetCalm(Behaviour.Looking);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, scareRadius);
            if (escapeTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, escapeTarget.position);
            }
        }
    }
}
