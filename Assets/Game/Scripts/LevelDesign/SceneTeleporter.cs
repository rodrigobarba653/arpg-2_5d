using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger that transitions the player to another scene. Optionally remembers
/// a spawn point id so the SpawnPoint with that id in the destination scene
/// can position the player on arrival.
///
/// Make sure the target scene is added to File ▸ Build Settings ▸ Scenes In Build.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTeleporter : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
    public string targetSceneName;

    [Tooltip("Optional. If set, the player is moved to the SpawnPoint with this " +
             "id in the target scene. Empty = player keeps its default spawn position.")]
    public string spawnPointId = "";

    [Header("Trigger Mode")]
    [Tooltip("If true, player must press the interact button while inside the " +
             "trigger. If false, teleports immediately on entry.")]
    public bool requireInteract = false;

    [Tooltip("Optional: child GameObject (usually an icon ✕/E) shown while the " +
             "player is inside and requireInteract is true.")]
    public GameObject interactPrompt;

    [Header("Fade Out (optional)")]
    [Tooltip("CanvasGroup that fades to alpha=1 before the new scene loads. " +
             "Usually a full-screen black Image with a CanvasGroup. Leave empty " +
             "to skip the fade.")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("How long the fade-to-black lasts before loading.")]
    public float fadeDuration = 0.4f;

    [Header("Anti Re-trigger")]
    [Tooltip("Seconds after a scene loads during which NO SceneTeleporter can " +
             "fire. Prevents the player spawning inside a teleporter and " +
             "ricocheting back to the previous scene.")]
    public float postLoadGrace = 1f;

    [Header("Gizmos")]
    public Color gizmoColor = new Color(0.4f, 1f, 0.6f, 0.5f);

    // Static state survives the scene unload so the destination SpawnPoint can read it.
    public static string PendingSpawnPointId;

    bool playerInside;
    bool teleportInProgress;

    // True if the player was already inside the trigger when the scene loaded
    // (grace blocked the entry). They must walk OUT first before any future
    // entry counts as a real intent to use the teleporter.
    bool playerSpawnedInside;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[SceneTeleporter] '{name}' collider should be 'Is Trigger'.", this);

        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (teleportInProgress) return;
        if (!requireInteract) return;
        if (!playerInside) return;

        if (InteractInput.WasPressedThisFrame(null))
            BeginTeleport();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = true;

        // If the scene just loaded, the player is spawning INSIDE this trigger.
        // Mark it and don't fire — they must walk OUT before any new entry counts.
        if (Time.timeSinceLevelLoad < postLoadGrace)
        {
            playerSpawnedInside = true;
            return;
        }

        // If the player spawned inside us and never left, ignore this re-enter.
        if (playerSpawnedInside) return;

        if (requireInteract)
        {
            if (interactPrompt != null) interactPrompt.SetActive(true);
            return;
        }

        BeginTeleport();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = false;
        // NOTE: we do NOT reset playerSpawnedInside here. If the player
        // spawned inside this teleporter, we keep it disabled for the rest of
        // this scene visit so they can't accidentally bounce back by walking
        // out and back in. They'll re-arm naturally when the next scene loads.

        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    static bool IsPlayer(Collider c)
    {
        return c.GetComponentInParent<PlayerHealth>() != null
            || c.CompareTag("Player");
    }

    void BeginTeleport()
    {
        if (teleportInProgress) return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[SceneTeleporter] No targetSceneName set.", this);
            return;
        }

        teleportInProgress = true;
        PendingSpawnPointId = spawnPointId;

        if (interactPrompt != null) interactPrompt.SetActive(false);

        // Freeze the player so it doesn't free-fall during the scene transition.
        // The SpawnPoint will re-enable it after positioning.
        FreezePlayerForTransition();

        // Show the loading screen (black overlay + corner label) and pause the
        // game. It auto-hides after the destination scene has loaded.
        LoadingScreen.Ensure().BeginTransition();

        if (fadeCanvasGroup != null && fadeDuration > 0f)
            StartCoroutine(FadeAndLoad());
        else
            SceneManager.LoadScene(targetSceneName);
    }

    static void FreezePlayerForTransition()
    {
        // Prefer the active party member; fall back to PersistentPlayer.Instance
        // for legacy setups.
        var active = Party.Active;
        if (active != null)
        {
            var m = active.GetComponent<PlayerMotor>();
            if (m != null) m.SetVerticalVelocity(0f);
            active.gameObject.SetActive(false);
            return;
        }

        var player = PersistentPlayer.Instance;
        if (player == null) return;

        var motor = player.GetComponent<PlayerMotor>();
        if (motor != null) motor.SetVerticalVelocity(0f);

        player.gameObject.SetActive(false);
    }

    IEnumerator FadeAndLoad()
    {
        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;

        float t = 0f;
        float start = fadeCanvasGroup.alpha;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(start, 1f, t / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
        SceneManager.LoadScene(targetSceneName);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        var col = GetComponent<Collider>();
        if (col is BoxCollider bc)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);
        }
        else if (col is SphereCollider sc)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sc.center, sc.radius);
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }
}
