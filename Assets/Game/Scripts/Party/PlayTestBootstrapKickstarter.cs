using System.Collections;
using UnityEngine;

/// <summary>
/// Lives on the PlayTestBootstrap prefab (see PlayTestBootstrapPrefabCreator
/// in Editor/). Runs once at Play start and does exactly what pressing "New
/// Game" on the Title Screen does — clears shared state and calls
/// PartyRoot.ApplyNewGameConfig() to actually show/activate the starting
/// party — but WITHOUT changing scenes, so you stay in whatever level you
/// dropped this prefab into. Then positions the active character on a
/// SpawnPoint in the current scene if one with a matching id exists.
///
/// Without this, the party member GameObjects stay however PartyRoot left
/// them after HideAll() (i.e. both off) — nothing ever tells it who should
/// actually be active, since that normally only happens via SaveManager's
/// real New Game / Load Game flow.
///
/// Testing convenience only — remove the prefab from the scene before
/// saving/shipping a real level.
/// </summary>
public class PlayTestBootstrapKickstarter : MonoBehaviour
{
    [Tooltip("SpawnPoint id to look for in the CURRENT scene. Match " +
             "SaveManager's 'New Game Spawn Point Id' so the same SpawnPoints " +
             "work for both a real New Game and Play-testing a level directly.")]
    public string spawnPointId = "new_game";

    IEnumerator Start()
    {
        // Let every other Awake in the scene (PartyRoot, SaveManager, etc.)
        // finish first.
        yield return null;

        if (PartyRoot.Instance == null)
        {
            Debug.LogWarning("[PlayTestBootstrapKickstarter] No PartyRoot found — " +
                              "is this prefab set up correctly? Re-run Tools/ARPG/Level " +
                              "Design/Create Play Test Bootstrap Prefab.");
            yield break;
        }

        PickupRegistry.Clear();
        PlayerInventory.ClearAll();
        PlayerWallet.ClearAll();

        PartyRoot.Instance.ApplyNewGameConfig();

        // One more frame so the newly-activated party member's Awake/OnEnable
        // (PlayerHealth.All registration etc.) finishes before we position it.
        yield return null;

        PositionAtSpawnPoint();
    }

    void PositionAtSpawnPoint()
    {
        if (string.IsNullOrEmpty(spawnPointId)) return;

        var active = Party.Active;
        if (active == null) return;

        var spawnPoints = FindObjectsOfType<SpawnPoint>();
        foreach (var sp in spawnPoints)
        {
            if (sp.spawnId != spawnPointId) continue;

            var cc = active.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            active.transform.SetPositionAndRotation(sp.transform.position, sp.transform.rotation);

            if (cc != null && wasEnabled) cc.enabled = true;

            PartyRoot.Instance.SyncHiddenToControlled();
            return;
        }

        Debug.Log($"[PlayTestBootstrapKickstarter] No SpawnPoint with id '{spawnPointId}' " +
                  "in this scene — party starts wherever PartyRoot placed it.");
    }
}
