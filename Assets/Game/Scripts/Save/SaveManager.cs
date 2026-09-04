using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that orchestrates save / load:
///   - Capture: walks the live scene (PlayerHealth, PlayerWallet, PlayerInventory,
///     PlayerEquipment, GameState) and builds a SaveData.
///   - Apply: takes a SaveData and pushes its values back into those components.
///
/// Stays alive across scene loads (DontDestroyOnLoad) so it can stage a Load:
/// 1) keep the SaveData in memory
/// 2) load the gameplay scene
/// 3) once the scene is ready, apply the data
///
/// Drop one GameObject with this component into your bootstrap scene.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Scenes")]
    [Tooltip("Scene loaded when starting a new game or loading a save (if save has no scene).")]
    public string defaultGameplayScene = "Game";

    [Header("Player Spawning (optional)")]
    [Tooltip("If assigned, SaveManager instantiates this prefab when no Player is " +
             "found in the destination scene after a New Game / Load Game. Leave " +
             "empty if your gameplay scenes already have a Player placed in them.")]
    public GameObject playerPrefab;

    [Tooltip("Default spawn position used when no SpawnPoint with newGameSpawnPointId " +
             "is found in the scene.")]
    public Vector3 newGameSpawnPosition = Vector3.zero;

    [Tooltip("Optional. If a SpawnPoint with this id exists in the destination scene, " +
             "the player is positioned there on New Game.")]
    public string newGameSpawnPointId = "new_game";

    [Header("Play Time")]
    [SerializeField] float currentSessionPlayTime;
    public float CurrentSessionPlayTime => currentSessionPlayTime;

    SaveData pendingLoad;
    bool pendingNewGameSpawn;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (!GamePause.IsPaused)
            currentSessionPlayTime += Time.unscaledDeltaTime;
    }

    // ============================================================
    // NEW GAME
    // ============================================================
    public void StartNewGame()
    {
        currentSessionPlayTime = 0f;
        pendingLoad = null;
        pendingNewGameSpawn = true;

        // Fresh start: no pickup is consumed yet.
        PickupRegistry.Clear();

        // Wipe shared party state so New Game doesn't carry stuff over.
        PlayerInventory.ClearAll();
        PlayerWallet.ClearAll();

        // Configure the initial party from PartyRoot's Inspector defaults (who
        // starts present, who starts active).
        if (PartyRoot.Instance != null)
            PartyRoot.Instance.ApplyNewGameConfig();

        FreezePlayerForTransition();
        LoadingScreen.Ensure().BeginTransition();

        if (!string.IsNullOrEmpty(defaultGameplayScene))
            SceneManager.LoadScene(defaultGameplayScene);
        else
            Debug.LogWarning("[SaveManager] defaultGameplayScene is empty.");
    }

    /// <summary>
    /// Hide and zero out the player just before a scene load. Prevents the
    /// player from free-falling during the brief moment between scene unload
    /// and re-positioning. The player is re-enabled once positioned.
    /// Works on the currently active party member.
    /// </summary>
    void FreezePlayerForTransition()
    {
        // Prefer the active party member; fall back to the legacy
        // PersistentPlayer reference for setups that haven't migrated yet.
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

        // Zero out vertical velocity so gravity doesn't carry over
        var motor = player.GetComponent<PlayerMotor>();
        if (motor != null) motor.SetVerticalVelocity(0f);

        // Hide the player entirely until positioned in the new scene
        player.gameObject.SetActive(false);
    }

    // ============================================================
    // SAVE
    // ============================================================
    public void SaveToSlot(int slot)
    {
        var data = CaptureCurrentState();
        if (data == null)
        {
            Debug.LogWarning("[SaveManager] No state to save (no Player in scene?).");
            return;
        }

        SaveSystem.Save(slot, data);
    }

    public SaveData CaptureCurrentState()
    {
        var data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            playTimeSeconds = currentSessionPlayTime
        };

        // Player position + character + stats
        var players = PlayerHealth.All;
        if (players.Count > 0)
        {
            var ph = players[0];
            data.playerPosition = ph.transform.position;
            data.playerEulerAngles = ph.transform.eulerAngles;

            if (ph.character != null)
                data.characterId = ph.character.id;

            data.level = ph.level;
            data.currentHealth = ph.currentHealth;
            data.maxHealth     = ph.maxHealth;
            data.currentMana   = ph.currentMana;
            data.maxMana       = ph.maxMana;
            data.currentAP     = ph.currentAP;
            data.maxAP         = ph.maxAP;
        }

        // Wallet
        // Wallet is shared (static); just read the total directly.
        data.varium = PlayerWallet.Total;

        // Inventory
        if (players.Count > 0)
        {
            var inv = players[0].GetComponent<PlayerInventory>();
            if (inv != null)
            {
                foreach (var entry in inv.Entries)
                {
                    if (entry == null || entry.item == null) continue;
                    if (string.IsNullOrEmpty(entry.item.id)) continue;

                    data.items.Add(new SavedItem
                    {
                        itemId = entry.item.id,
                        quantity = entry.quantity
                    });
                }
            }
        }

        // Equipment — one entry per party member.
        data.partyEquipment.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            var eq = p.GetComponent<PlayerEquipment>();
            if (eq == null) continue;

            string charId = p.character != null ? p.character.id : "";

            var s1 = eq.GetSlotPart(0);
            var s2 = eq.GetSlotPart(1);
            var s3 = eq.GetSlotPart(2);

            data.partyEquipment.Add(new SavedCharacterEquipment
            {
                characterId  = charId,
                weaponId     = eq.CurrentWeapon != null ? eq.CurrentWeapon.id : "",
                slot1PartId  = s1 != null ? s1.id : "",
                slot2PartId  = s2 != null ? s2.id : "",
                slot3PartId  = s3 != null ? s3.id : "",
            });
        }

        // Legacy field — first character's weapon, kept for old saves' compat.
        if (data.partyEquipment.Count > 0)
            data.equippedWeaponId = data.partyEquipment[0].weaponId;

        // Stats — one entry per party member (HP/MP, independent per character).
        data.partyStats.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            string charId = p.character != null ? p.character.id : "";

            data.partyStats.Add(new SavedCharacterStats
            {
                characterId   = charId,
                currentHealth = p.currentHealth,
                maxHealth     = p.maxHealth,
                currentMana   = p.currentMana,
                maxMana       = p.maxMana,
            });
        }

        // Magic — one entry per party member (which spell is bound to each of
        // their 3 cast-wheel slots). The party-wide pool of KNOWN spells is
        // already covered above via `items` (SpellItems in the shared inventory).
        data.partyMagic.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            var magic = p.GetComponent<PlayerMagic>();
            if (magic == null) continue;

            string charId = p.character != null ? p.character.id : "";

            var s1 = magic.GetEquipped(0);
            var s2 = magic.GetEquipped(1);
            var s3 = magic.GetEquipped(2);

            data.partyMagic.Add(new SavedCharacterMagic
            {
                characterId  = charId,
                slot1SpellId = s1 != null ? s1.id : "",
                slot2SpellId = s2 != null ? s2.id : "",
                slot3SpellId = s3 != null ? s3.id : "",
            });
        }

        // World
        if (GameState.Instance != null)
            data.currentLocation = GameState.Instance.currentLocation;

        // Pickups already consumed in this play.
        data.consumedPickupIds = PickupRegistry.Snapshot();

        // Who is the player currently controlling.
        if (Party.Active != null && Party.Active.character != null)
            data.activeCharacterId = Party.Active.character.id;

        return data;
    }

    // ============================================================
    // LOAD
    // ============================================================
    public void LoadFromSlot(int slot)
    {
        var data = SaveSystem.Load(slot);
        if (data == null)
        {
            Debug.LogWarning($"[SaveManager] No save data in slot {slot}.");
            return;
        }

        FreezePlayerForTransition();
        BeginLoad(data);
    }

    public void BeginLoad(SaveData data)
    {
        if (data == null) return;

        pendingLoad = data;

        // Restore the consumed pickup set BEFORE the scene loads so each
        // ItemPickup.Awake sees the correct state and hides itself if needed.
        PickupRegistry.RestoreFrom(data.consumedPickupIds);

        string sceneToLoad = !string.IsNullOrEmpty(data.sceneName) ? data.sceneName : defaultGameplayScene;

        if (SceneManager.GetActiveScene().name == sceneToLoad)
            // Already in the scene → apply immediately on next frame
            ApplyPendingLoad();
        else
        {
            LoadingScreen.Ensure().BeginTransition();
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Wait one frame so all the scene's Awakes / Starts (and PlayerHealth
        // registrations) finish before we try to find / spawn the player.
        StartCoroutine(HandleSceneLoadedNextFrame());
    }

    System.Collections.IEnumerator HandleSceneLoadedNextFrame()
    {
        yield return null;

        SpawnPlayerIfNeeded();

        var player = PersistentPlayer.Instance;
        bool wasDisabled = player != null && !player.gameObject.activeSelf;

        // A SceneTeleporter set this. If so, SpawnPoint owns the spawn — we
        // must NOT re-enable the player here at the old position, or HazardRespawn
        // triggers may fire before SpawnPoint can reposition it.
        bool teleporterOwnsSpawn = !string.IsNullOrEmpty(SceneTeleporter.PendingSpawnPointId);

        // Position the player WHILE STILL DISABLED so it doesn't fall before
        // we get there. Transform updates work on inactive GameObjects.
        if (pendingNewGameSpawn)
        {
            ApplyNewGameSpawnPoint();
            pendingNewGameSpawn = false;
        }

        if (pendingLoad != null)
        {
            // Position-only first (no inventory yet) so we can re-enable cleanly.
            PositionPlayerFromSave(pendingLoad);
        }

        // Reset vertical velocity right before activating, so accumulated gravity
        // from before the scene change doesn't snap the player downward.
        if (player != null)
        {
            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null) motor.SetVerticalVelocity(0f);

            if (wasDisabled && !teleporterOwnsSpawn)
                player.gameObject.SetActive(true);
        }

        // Wait one frame so OnEnable callbacks (PlayerHealth.All etc.) finish
        // before we apply inventory / stats.
        yield return null;

        if (pendingLoad != null)
        {
            ApplyState(pendingLoad);
            pendingLoad = null;
        }
    }

    void PositionPlayerFromSave(SaveData data)
    {
        var player = PersistentPlayer.Instance;
        if (player == null)
        {
            // No persistent player — fall back to PlayerHealth.All
            var players = PlayerHealth.All;
            if (players.Count == 0) return;
            player = players[0].GetComponent<PersistentPlayer>();
            if (player == null)
            {
                // Just position the PlayerHealth's transform
                var ph = players[0];
                var cc2 = ph.GetComponent<CharacterController>();
                bool wasEnabled = cc2 != null && cc2.enabled;
                if (cc2 != null) cc2.enabled = false;
                ph.transform.SetPositionAndRotation(data.playerPosition,
                                                    Quaternion.Euler(data.playerEulerAngles));
                if (cc2 != null && wasEnabled) cc2.enabled = true;
                return;
            }
        }

        var cc = player.GetComponent<CharacterController>();
        bool wasEnabledCc = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        player.transform.SetPositionAndRotation(data.playerPosition,
                                                Quaternion.Euler(data.playerEulerAngles));

        if (cc != null && wasEnabledCc) cc.enabled = true;

        HazardRespawn.SuspendForSeconds(1.5f);
    }

    void SpawnPlayerIfNeeded()
    {
        // PartyRoot owns the characters now. If it exists, it's the source of
        // truth and SaveManager must NOT spawn anything from the legacy
        // playerPrefab — that would create a duplicate "(Clone)" character.
        if (PartyRoot.Instance != null) return;

        // Legacy fallback path: no PartyRoot in the scene.
        // Already has a persistent player (active OR disabled) → nothing to do.
        if (PersistentPlayer.Instance != null) return;

        // Or a registered PlayerHealth → also nothing to do.
        if (PlayerHealth.All.Count > 0) return;

        if (playerPrefab == null)
        {
            Debug.LogWarning("[SaveManager] No Player in scene and no Player Prefab " +
                             "assigned. Either place a Player in the scene or assign a " +
                             "Player Prefab to the SaveManager.");
            return;
        }

        Vector3 spawnPos = newGameSpawnPosition;
        Quaternion spawnRot = Quaternion.identity;

        // Try to find a SpawnPoint with the new-game id
        if (!string.IsNullOrEmpty(newGameSpawnPointId))
        {
            var allSpawnPoints = FindObjectsOfType<SpawnPoint>();
            for (int i = 0; i < allSpawnPoints.Length; i++)
            {
                if (allSpawnPoints[i].spawnId == newGameSpawnPointId)
                {
                    spawnPos = allSpawnPoints[i].transform.position;
                    spawnRot = allSpawnPoints[i].transform.rotation;
                    break;
                }
            }
        }

        var player = Instantiate(playerPrefab, spawnPos, spawnRot);
        player.name = playerPrefab.name; // drop the "(Clone)" suffix

        Debug.Log($"[SaveManager] Spawned Player at {spawnPos}.");
    }

    void ApplyNewGameSpawnPoint()
    {
        if (string.IsNullOrEmpty(newGameSpawnPointId)) return;

        // Find the player even if it's currently disabled (in transition).
        Transform playerT = null;

        if (PersistentPlayer.Instance != null)
            playerT = PersistentPlayer.Instance.transform;
        else
        {
            var players = PlayerHealth.All;
            if (players.Count > 0) playerT = players[0].transform;
        }

        if (playerT == null)
        {
            Debug.LogWarning("[SaveManager] ApplyNewGameSpawnPoint: no player found.");
            return;
        }

        var allSpawnPoints = FindObjectsOfType<SpawnPoint>();
        SpawnPoint target = null;

        for (int i = 0; i < allSpawnPoints.Length; i++)
        {
            if (allSpawnPoints[i].spawnId == newGameSpawnPointId)
            {
                target = allSpawnPoints[i];
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[SaveManager] No SpawnPoint with id '{newGameSpawnPointId}' " +
                             $"found in the current scene.");
            return;
        }

        var cc = playerT.GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        playerT.SetPositionAndRotation(target.transform.position, target.transform.rotation);

        if (cc != null && wasEnabled) cc.enabled = true;

        HazardRespawn.SuspendForSeconds(1.5f);

        // Pull every hidden party member to the active's new spot so the FIRST
        // swap doesn't drop them back wherever they were originally.
        if (PartyRoot.Instance != null)
            PartyRoot.Instance.SyncHiddenToControlled();

        Debug.Log($"[SaveManager] Positioned player at SpawnPoint '{newGameSpawnPointId}' " +
                  $"({target.transform.position}).");
    }

    System.Collections.IEnumerator ApplyPendingLoadNextFrame()
    {
        yield return null;
        ApplyPendingLoad();
    }

    public void ApplyPendingLoad()
    {
        if (pendingLoad == null) return;

        var data = pendingLoad;
        pendingLoad = null;

        ApplyState(data);
    }

    public void ApplyState(SaveData data)
    {
        if (data == null) return;

        currentSessionPlayTime = data.playTimeSeconds;

        // Player
        var players = PlayerHealth.All;
        if (players.Count == 0)
        {
            Debug.LogWarning($"[SaveManager] ApplyState: no PlayerHealth found in scene " +
                             $"'{SceneManager.GetActiveScene().name}'. State NOT applied. " +
                             $"Make sure the gameplay scene has a Player with PlayerHealth.");
            return;
        }

        if (players.Count > 0)
        {
            var ph = players[0];

            // Position is handled separately in PositionPlayerFromSave so the
            // player is already in place when we get here.

            // HP/MP are restored per-character below (partyStats). Level/AP
            // stay single-character for now — only the first party member.
            ph.level  = data.level;
            ph.maxAP  = Mathf.Max(0, data.maxAP);
            ph.currentAP = Mathf.Clamp(data.currentAP, 0, ph.maxAP);
            ph.NotifyStatsChanged();

            Debug.Log($"[SaveManager] Player teleported to {data.playerPosition}.");
        }

        // Stats — per character via partyStats list, with fallback to the
        // legacy single-character HP/MP fields for old saves (which only ever
        // tracked the first party member).
        if (data.partyStats != null && data.partyStats.Count > 0)
        {
            for (int i = 0; i < data.partyStats.Count; i++)
                ApplyStatsEntry(players, data.partyStats[i]);
        }
        else if (players.Count > 0)
        {
            var ph = players[0];
            ph.maxHealth     = Mathf.Max(1, data.maxHealth);
            ph.maxMana       = Mathf.Max(0, data.maxMana);
            ph.currentHealth = Mathf.Clamp(data.currentHealth, 0, ph.maxHealth);
            ph.currentMana   = Mathf.Clamp(data.currentMana,   0, ph.maxMana);
            ph.NotifyStatsChanged();
        }

        // Wallet — shared across the party (static state).
        PlayerWallet.SetTotal(data.varium);

        // Inventory — shared across the party. Clear once and repopulate via
        // any character's PlayerInventory (they all back the same data).
        if (players.Count > 0)
        {
            var inv = players[0].GetComponent<PlayerInventory>();
            var db  = ItemDatabase.Instance;

            if (inv != null && db != null)
            {
                PlayerInventory.ClearAll();
                foreach (var saved in data.items)
                {
                    var item = db.GetById(saved.itemId);
                    if (item != null && saved.quantity > 0)
                        inv.Add(item, saved.quantity);
                }
            }
        }

        // Equipment — per character via partyEquipment list, with fallback to
        // the legacy single-character field.
        var itemDb = ItemDatabase.Instance;

        if (itemDb != null && data.partyEquipment != null && data.partyEquipment.Count > 0)
        {
            for (int i = 0; i < data.partyEquipment.Count; i++)
                ApplyEquipmentEntry(players, data.partyEquipment[i], itemDb);
        }
        else if (players.Count > 0 && itemDb != null && !string.IsNullOrEmpty(data.equippedWeaponId))
        {
            // Legacy path: old save with just one weapon id for the first player.
            var eq = players[0].GetComponent<PlayerEquipment>();
            if (eq != null)
            {
                var weapon = itemDb.GetById(data.equippedWeaponId) as WeaponItem;
                if (weapon != null) eq.Equip(weapon);
            }
        }

        // Magic — per character via partyMagic list. Must run AFTER inventory
        // is restored above, since PlayerMagic.Equip() requires the SpellItem
        // to already be present in the shared inventory.
        if (itemDb != null && data.partyMagic != null && data.partyMagic.Count > 0)
        {
            for (int i = 0; i < data.partyMagic.Count; i++)
                ApplyMagicEntry(players, data.partyMagic[i], itemDb);
        }

        // World
        if (GameState.Instance != null && !string.IsNullOrEmpty(data.currentLocation))
            GameState.Instance.SetLocation(data.currentLocation);

        // Restore the active party member if saved.
        if (!string.IsNullOrEmpty(data.activeCharacterId))
            Party.SetActiveByCharacterId(data.activeCharacterId);

        Debug.Log($"[SaveManager] Applied save (slot {data.slot}, scene {data.sceneName}).");
    }

    /// <summary>
    /// Apply one party member's equipment from a save entry. Matches the
    /// PlayerHealth by character.id; falls back to index 0 when the id is empty
    /// or unmatched.
    /// </summary>
    static void ApplyEquipmentEntry(
        System.Collections.Generic.IReadOnlyList<PlayerHealth> players,
        SavedCharacterEquipment entry,
        ItemDatabase db)
    {
        var target = FindByCharacterId(players, entry.characterId);
        if (target == null) return;

        var eq = target.GetComponent<PlayerEquipment>();
        if (eq == null) return;

        if (!string.IsNullOrEmpty(entry.weaponId))
        {
            var weapon = db.GetById(entry.weaponId) as WeaponItem;
            if (weapon != null) eq.Equip(weapon);
        }

        // New-format slots (preferred).
        ApplyPart(eq, db, entry.slot1PartId);
        ApplyPart(eq, db, entry.slot2PartId);
        ApplyPart(eq, db, entry.slot3PartId);

        // Legacy fallback for old saves that used handle/guard/blade fields.
        if (string.IsNullOrEmpty(entry.slot1PartId) &&
            string.IsNullOrEmpty(entry.slot2PartId) &&
            string.IsNullOrEmpty(entry.slot3PartId))
        {
            ApplyPart(eq, db, entry.handlePartId);
            ApplyPart(eq, db, entry.guardPartId);
            ApplyPart(eq, db, entry.bladePartId);
        }
    }

    static void ApplyPart(PlayerEquipment eq, ItemDatabase db, string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var part = db.GetById(id) as WeaponPart;
        if (part != null) eq.EquipPart(part);
    }

    /// <summary>Finds the PlayerHealth whose character.id matches, falling
    /// back to index 0 when the id is empty or unmatched. Shared by the
    /// per-character save-entry appliers (equipment/stats/magic).</summary>
    static PlayerHealth FindByCharacterId(
        System.Collections.Generic.IReadOnlyList<PlayerHealth> players, string characterId)
    {
        if (players == null || players.Count == 0) return null;

        if (!string.IsNullOrEmpty(characterId))
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null || players[i].character == null) continue;
                if (players[i].character.id == characterId) return players[i];
            }
        }

        return players[0];
    }

    /// <summary>Apply one party member's saved HP/MP. Matches by characterId,
    /// falls back to index 0 when unmatched (see FindByCharacterId).</summary>
    static void ApplyStatsEntry(
        System.Collections.Generic.IReadOnlyList<PlayerHealth> players,
        SavedCharacterStats entry)
    {
        var target = FindByCharacterId(players, entry.characterId);
        if (target == null) return;

        target.maxHealth     = Mathf.Max(1, entry.maxHealth);
        target.maxMana       = Mathf.Max(0, entry.maxMana);
        target.currentHealth = Mathf.Clamp(entry.currentHealth, 0, target.maxHealth);
        target.currentMana   = Mathf.Clamp(entry.currentMana,   0, target.maxMana);
        target.NotifyStatsChanged();
    }

    /// <summary>Apply one party member's saved magic loadout (which SpellItem
    /// is bound to each of their 3 cast-wheel slots). Matches by characterId,
    /// falls back to index 0 when unmatched (see FindByCharacterId). Requires
    /// the spell to already be in the shared inventory — PlayerMagic.Equip()
    /// silently no-ops otherwise (also self-guards against an incompatible
    /// character/spell pairing, so a bad fallback match is harmless).</summary>
    static void ApplyMagicEntry(
        System.Collections.Generic.IReadOnlyList<PlayerHealth> players,
        SavedCharacterMagic entry,
        ItemDatabase db)
    {
        var target = FindByCharacterId(players, entry.characterId);
        if (target == null) return;

        var magic = target.GetComponent<PlayerMagic>();
        if (magic == null) return;

        ApplySpellSlot(magic, db, 0, entry.slot1SpellId);
        ApplySpellSlot(magic, db, 1, entry.slot2SpellId);
        ApplySpellSlot(magic, db, 2, entry.slot3SpellId);
    }

    static void ApplySpellSlot(PlayerMagic magic, ItemDatabase db, int slotIndex, string spellId)
    {
        if (string.IsNullOrEmpty(spellId))
        {
            magic.Equip(null, slotIndex);
            return;
        }

        var spell = db.GetById(spellId) as SpellItem;
        if (spell != null) magic.Equip(spell, slotIndex);
    }
}
