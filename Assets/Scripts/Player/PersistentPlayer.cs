using UnityEngine;

/// <summary>
/// Marker for a Player GameObject so older scripts can still reach "the
/// player" through PersistentPlayer.Instance.
///
/// Two modes depending on the scene setup:
///
///  (A) Under a PartyRoot: PartyRoot owns persistence (DontDestroyOnLoad on
///      the parent) and tracks the active member via the Party class. This
///      component is a passive marker — it does NOT singleton-destroy duplicates
///      so multiple party members can coexist. PersistentPlayer.Instance
///      returns the one on the currently-active party member.
///
///  (B) Standalone (no PartyRoot in parents): legacy single-character
///      behaviour. The component is a singleton; duplicates are destroyed; the
///      GameObject is marked DontDestroyOnLoad on its own.
///
/// Place on the root of each Player GameObject in either setup.
/// </summary>
public class PersistentPlayer : MonoBehaviour
{
    [Tooltip("Only used in standalone mode (no PartyRoot). " +
             "If true, sets DontDestroyOnLoad so the Player survives scene loads.")]
    public bool persistAcrossScenes = true;

    [Tooltip("If true, log when the singleton kicks in or a duplicate is destroyed.")]
    public bool debugLog = true;

    /// <summary>
    /// Active player. With a PartyRoot, this is the PersistentPlayer of the
    /// currently controlled party member. Without one, it's the legacy single
    /// instance.
    /// </summary>
    public static PersistentPlayer Instance
    {
        get
        {
            if (Party.Active != null)
            {
                var p = Party.Active.GetComponent<PersistentPlayer>();
                if (p != null) return p;
            }
            return legacyInstance;
        }
    }

    static PersistentPlayer legacyInstance;

    bool isUnderPartyRoot;

    void Awake()
    {
        // Mode A — live under a PartyRoot: don't singleton, don't DontDestroyOnLoad.
        isUnderPartyRoot = GetComponentInParent<PartyRoot>(includeInactive: true) != null;

        if (isUnderPartyRoot)
        {
            if (debugLog)
                Debug.Log($"[PersistentPlayer] '{name}' lives under PartyRoot — " +
                          "PartyRoot manages persistence. (no singleton)", this);
            return;
        }

        // Mode B — standalone: legacy singleton behaviour.
        if (legacyInstance != null && legacyInstance != this)
        {
            if (debugLog)
                Debug.Log($"[PersistentPlayer] Duplicate Player detected in scene " +
                          $"'{gameObject.scene.name}' — destroying this one.", this);

            Destroy(gameObject);
            return;
        }

        legacyInstance = this;

        if (persistAcrossScenes && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (debugLog)
            Debug.Log($"[PersistentPlayer] Registered Player '{name}' (standalone mode).", this);
    }

    void OnDestroy()
    {
        if (legacyInstance == this) legacyInstance = null;
    }
}
