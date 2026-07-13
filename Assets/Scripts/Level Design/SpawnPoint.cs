using UnityEngine;

/// <summary>
/// Marker in a scene where the player can spawn. When a SceneTeleporter sets
/// SceneTeleporter.PendingSpawnPointId before a scene load, the SpawnPoint
/// with the matching id repositions the player on Start.
///
/// Drop one or more in each scene at the entry locations from neighboring areas.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Unique id within this scene (matches SceneTeleporter.spawnPointId).")]
    public string spawnId = "default";

    [Tooltip("If true, fades a CanvasGroup from black to clear on arrival. Useful " +
             "to pair with a SceneTeleporter that faded out.")]
    public bool fadeInOnArrival = false;

    [Tooltip("CanvasGroup that fades from 1 → 0 on arrival.")]
    public CanvasGroup fadeCanvasGroup;

    [Range(0.05f, 5f)]
    public float fadeInDuration = 0.4f;

    [Header("Gizmos")]
    public Color gizmoColor = new Color(1f, 0.8f, 0.3f, 0.8f);

    void Start()
    {
        // Only spawn at this point if we're the pending target.
        if (SceneTeleporter.PendingSpawnPointId != spawnId) return;

        SceneTeleporter.PendingSpawnPointId = null;

        // Wait one frame so PersistentPlayer.Awake has run and the destination
        // scene's colliders/physics are fully initialized.
        StartCoroutine(TeleportNextFrame());
    }

    System.Collections.IEnumerator TeleportNextFrame()
    {
        yield return null;

        TeleportPlayerHere();

        if (fadeInOnArrival && fadeCanvasGroup != null)
            StartCoroutine(FadeIn());
    }

    void TeleportPlayerHere()
    {
        // Use PersistentPlayer.Instance — works even if the player GameObject is
        // currently disabled (SceneTeleporter freezes it during the transition).
        GameObject playerGO = null;

        if (PersistentPlayer.Instance != null)
        {
            playerGO = PersistentPlayer.Instance.gameObject;
        }
        else
        {
            var players = PlayerHealth.All;
            if (players.Count > 0 && players[0] != null)
                playerGO = players[0].gameObject;
        }

        if (playerGO == null)
        {
            Debug.LogWarning($"[SpawnPoint] No player found for spawn '{spawnId}'.", this);
            return;
        }

        bool wasDisabled = !playerGO.activeSelf;

        // 1. Suspend HazardRespawn BEFORE we touch the player. This way the
        //    very first OnTriggerEnter (when the player re-activates inside any
        //    trigger) is ignored.
        HazardRespawn.SuspendForSeconds(2f);

        // 2. CharacterController must be disabled before manually setting
        //    transform.position, otherwise it ignores the change.
        var cc = playerGO.GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        // 3. Position the player WHILE STILL DISABLED so no physics / triggers
        //    can react to the old position.
        playerGO.transform.SetPositionAndRotation(transform.position, transform.rotation);

        // 4. Zero out the accumulated falling velocity so the player doesn't
        //    keep plummeting from before the scene change.
        var motor = playerGO.GetComponent<PlayerMotor>();
        if (motor != null) motor.SetVerticalVelocity(0f);

        // 5. Re-enable CharacterController.
        if (cc != null && ccWasEnabled) cc.enabled = true;

        // 6. NOW activate the player. Its first OnTriggerEnter (if any) will
        //    see the suspension and skip.
        if (wasDisabled) playerGO.SetActive(true);

        Debug.Log($"[SpawnPoint] Positioned player at '{spawnId}' ({transform.position}).", this);
    }

    System.Collections.IEnumerator FadeIn()
    {
        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeInDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.gameObject.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Arrow showing facing direction
        var forward = transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + forward * 1.5f);
        Gizmos.DrawLine(transform.position + forward * 1.5f,
                        transform.position + forward * 1f + transform.right * 0.3f);
        Gizmos.DrawLine(transform.position + forward * 1.5f,
                        transform.position + forward * 1f - transform.right * 0.3f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, $"⮕ {spawnId}");
#endif
    }
}
