using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-assigns the player as the tracking target of the Cinemachine camera AND
/// of the CameraObstructionFade script the moment the player is available (after
/// the scene loads, even if the Player was spawned by SaveManager or persists
/// from a previous scene).
///
/// Place on the GameObject that has the CinemachineCamera component.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CameraAutoTarget : MonoBehaviour
{
    [Header("Refs (auto-resolved on Awake if empty)")]
    public CinemachineCamera vcam;

    [Tooltip("Optional. The CameraObstructionFade on this same camera GameObject.")]
    public CameraObstructionFade obstructionFade;

    [Header("Player Detection")]
    [Tooltip("Used as fallback if PersistentPlayer.Instance and PlayerHealth.All are empty.")]
    public string playerTag = "Player";

    [Header("Debug")]
    public bool debugLog = true;

    Transform currentTarget;

    void Awake()
    {
        if (vcam == null) vcam = GetComponent<CinemachineCamera>();
        if (obstructionFade == null) obstructionFade = GetComponent<CameraObstructionFade>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Party.OnActiveChanged += OnPartyActiveChanged;
        StartCoroutine(DelayedFindAndAssign());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Party.OnActiveChanged -= OnPartyActiveChanged;
    }

    void OnPartyActiveChanged(PlayerHealth _)
    {
        // Force re-target on next frame so we follow the new active character.
        currentTarget = null;
        StartCoroutine(DelayedFindAndAssign());
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        StartCoroutine(DelayedFindAndAssign());
    }

    void Update()
    {
        // If the current target was destroyed (player respawn, scene unload),
        // keep trying every frame until we find a fresh one.
        if (currentTarget == null) TryFindAndAssign();
    }

    IEnumerator DelayedFindAndAssign()
    {
        // Wait one frame so PersistentPlayer.Awake and SaveManager spawn have run.
        yield return null;
        TryFindAndAssign();
    }

    void TryFindAndAssign()
    {
        Transform t = FindPlayer();
        if (t == null) return;
        if (currentTarget == t) return; // already assigned

        currentTarget = t;

        if (vcam != null)
        {
            vcam.Follow = t;
            vcam.LookAt = t;
        }

        if (obstructionFade != null)
            obstructionFade.target = t;

        if (debugLog)
            Debug.Log($"[CameraAutoTarget] Tracking player '{t.name}'.", this);
    }

    Transform FindPlayer()
    {
        // Primary: the currently-controlled party member.
        var t = Party.ActiveTransform;
        if (t != null) return t;

        // Fallback: legacy PersistentPlayer.
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.transform;

        // Fallback: any PlayerHealth registered to the global list.
        var players = PlayerHealth.All;
        if (players.Count > 0 && players[0] != null)
            return players[0].transform;

        // Last resort: search by tag.
        var pgo = GameObject.FindWithTag(playerTag);
        return pgo != null ? pgo.transform : null;
    }
}
