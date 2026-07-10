using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent container that owns the party-eligible character GameObjects.
/// Place ONE in the title scene at the root of the hierarchy.
///
/// SWAP DESIGN — both characters stay SetActive(true) at all times when in
/// the party. A PartyMemberControl on each one toggles "controlled" state:
/// the controlled character is visible + has motor/CC/combat enabled, the
/// other is invisible + dormant.
///
/// Every LateUpdate, the dormant members' transforms are forced to match the
/// controlled one's. When the user swaps, the new character is ALREADY at
/// the right world position — we just toggle who is controlled.
/// </summary>
public class PartyRoot : MonoBehaviour
{
    public static PartyRoot Instance { get; private set; }

    [Serializable]
    public class MemberSlot
    {
        [Tooltip("ScriptableObject identity for this character.")]
        public CharacterDefinition character;

        [Tooltip("The character's GameObject (child of this PartyRoot). " +
                 "Should have PlayerHealth + PartyMemberControl + the usual " +
                 "player components.")]
        public GameObject root;

        [Tooltip("If true, this character is part of the party from New Game.")]
        public bool isInParty = true;

        [Tooltip("If true, this character is the one the player controls at " +
                 "the start of New Game. Only ONE slot should have this " +
                 "checked — if multiple have it, the first one wins.")]
        public bool isActiveCharacter;
    }

    [Header("Members")]
    public MemberSlot[] members;

    [Header("Swap VFX (optional)")]
    public GameObject swapVfxPrefab;
    public AudioClip swapSound;

    [Header("Swap Pause")]
    [Tooltip("If true, freezes the world (Time.timeScale = 0) for swapPauseDuration " +
             "seconds during the swap — like a hit-stop. The swap VFX prefab should " +
             "use 'Unscaled Time' in its ParticleSystem's Main module so it animates " +
             "during the pause.")]
    public bool pauseWorldDuringSwap = true;

    [Tooltip("How long to freeze the world during the swap, in unscaled seconds. " +
             "Use 0 to disable.")]
    [Range(0f, 1f)]
    public float swapPauseDuration = 0.3f;

    [Header("Behaviour")]
    public bool debugLog = true;

    PlayerHealth currentControlled;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (debugLog)
                Debug.Log($"[PartyRoot] Duplicate in scene '{gameObject.scene.name}' — destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("[PartyRoot] Not at the root of the scene — " +
                             "DontDestroyOnLoad will fail. Move it to the root.", this);
        }

        ValidateMemberSlots();

        // Detach members so PartyRoot's transform doesn't affect their world
        // positions / rotations. Each becomes its own root with its own
        // DontDestroyOnLoad. PartyRoot keeps the slot list and singleton role.
        DetachMembers();

        HideAll();

        Party.OnActiveChanged += HandleActiveChanged;

        if (debugLog)
            Debug.Log($"[PartyRoot] Registered with {members?.Length ?? 0} member slots.", this);
    }

    void DetachMembers()
    {
        if (members == null) return;
        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null || m.root == null) continue;
            if (m.root.transform.parent == transform)
            {
                m.root.transform.SetParent(null, worldPositionStays: true);
                DontDestroyOnLoad(m.root);
                if (debugLog)
                    Debug.Log($"[PartyRoot] Detached '{m.root.name}' to root + DontDestroyOnLoad.", this);
            }
        }
    }

    void OnDestroy()
    {
        Party.OnActiveChanged -= HandleActiveChanged;
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Enforce "only one active character" in the editor.
        if (members == null) return;
        bool found = false;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] == null) continue;
            if (!members[i].isActiveCharacter) continue;
            if (found) members[i].isActiveCharacter = false;
            else found = true;
        }
    }
#endif

    void ValidateMemberSlots()
    {
        if (members == null) return;
        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null) continue;
            if (m.character == null)
            {
                Debug.LogError($"[PartyRoot] Member slot {i} has no CharacterDefinition assigned.", this);
                continue;
            }
            if (m.root == null)
            {
                Debug.LogError($"[PartyRoot] Member '{m.character.displayName}' has no Root GameObject assigned.", this);
                continue;
            }
            if (!m.root.scene.IsValid())
            {
                Debug.LogError($"[PartyRoot] Member '{m.character.displayName}' Root is a " +
                               "PREFAB ASSET (from the Project window), not a scene instance. " +
                               "Drag the GameObject from the HIERARCHY (must be a child of this " +
                               "PartyRoot), not from the Project. The character will not appear.", this);
            }
            if (m.root != null && m.root.GetComponent<PartyMemberControl>() == null)
            {
                Debug.LogError($"[PartyRoot] Member '{m.character.displayName}' Root is missing " +
                               "the PartyMemberControl component. Add it so PartyRoot can toggle " +
                               "the controlled state.", m.root);
            }
        }
    }

    // ============================================================
    // SYNC — keep dormant members at the controlled one's transform.
    // ============================================================

    void LateUpdate()
    {
        SyncHiddenToControlled();
    }

    /// <summary>
    /// Copy the controlled member's transform to every other active member so
    /// a future swap drops the new character at the right spot.
    /// Public so external systems (SaveManager after positioning the player at
    /// a SpawnPoint, etc.) can force-sync mid-frame.
    /// </summary>
    public void SyncHiddenToControlled()
    {
        if (currentControlled == null || members == null) return;

        Vector3 pos = currentControlled.transform.position;
        Quaternion rot = currentControlled.transform.rotation;

        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null || m.root == null) continue;
            if (m.root == currentControlled.gameObject) continue;
            if (!m.root.activeSelf) continue;

            m.root.transform.SetPositionAndRotation(pos, rot);
        }
    }

    // ============================================================
    // SWAP
    // ============================================================

    void HandleActiveChanged(PlayerHealth newActive)
    {
        if (newActive == null) return;

        // First activation — nobody was controlled yet.
        if (currentControlled == null)
        {
            ApplyControlState(newActive);
            currentControlled = newActive;
            // Immediately sync hidden members to newActive's position so the
            // FIRST swap drops the new character at the right spot — instead
            // of wherever it was sitting in the title scene / PartyRoot.
            SyncHiddenToControlled();
            return;
        }

        if (currentControlled == newActive)
        {
            ApplyControlState(newActive);
            return;
        }

        // Real swap. Forcefully sync the new character's transform to the old
        // one RIGHT NOW (don't rely on the previous LateUpdate having run).
        Vector3 swapPos = currentControlled.transform.position;
        Quaternion swapRot = currentControlled.transform.rotation;

        // Copy the motor's 2D facing direction so the new character keeps
        // looking the same way (sprite doesn't flip on swap).
        var oldMotor = currentControlled.GetComponent<PlayerMotor>();
        var newMotorPre = newActive.GetComponent<PlayerMotor>();
        if (oldMotor != null && newMotorPre != null)
            newMotorPre.SetFacing(oldMotor.GetFacing2D());

        Vector3 jynBeforeSync = newActive.transform.position;

        // STEP 1 — Position the new character at the old's spot, BEFORE making
        // it visible. CC stays disabled during the write.
        var newCC = newActive.GetComponent<CharacterController>();
        if (newCC != null) newCC.enabled = false;

        newActive.transform.SetPositionAndRotation(swapPos, swapRot);
        Physics.SyncTransforms();

        // STEP 2 — Snap onto the floor BEFORE enabling the renderer. This way
        // the first frame the new character is visible, they're already in
        // the right spot — no "appear in air, then fall" flicker.
        if (newCC != null)
        {
            // Temporarily enable CC just to do the raycast & snap (the snap
            // function toggles CC internally).
            newCC.enabled = true;
            SnapNewActiveToGround(newActive, newCC);
            newCC.enabled = false;
            Physics.SyncTransforms();
        }

        // VFX/SFX at the (now final) ground position.
        Vector3 finalPos = newActive.transform.position;
        if (swapVfxPrefab != null)
        {
            var vfx = Instantiate(swapVfxPrefab, finalPos, Quaternion.identity);
            // Force every ParticleSystem inside to ignore Time.timeScale so the
            // VFX still animates while the world is paused during the swap.
            ForceUnscaledTimeOnParticleSystems(vfx);
        }
        if (swapSound != null)
            AudioManager.PlaySfxOrFallback(swapSound, finalPos, 1f);

        HazardRespawn.SuspendForSeconds(1f);

        // STEP 3 — Now reveal the new character (renderer, motor, CC, input).
        ApplyControlState(newActive);

        // STEP 4 — Ensure CC is on and refresh isGrounded with a tiny move.
        // Use a very small value so we don't slide on slopes (which was making
        // each swap drift the player downhill).
        if (newCC != null)
        {
            if (!newCC.enabled) newCC.enabled = true;
            newCC.Move(Vector3.down * 0.001f);
        }

        var newMotor = newActive.GetComponent<PlayerMotor>();
        if (newMotor != null) newMotor.SetVerticalVelocity(0f);

        // STEP 5 — Force-clear any lingering "in air" / landing state so the
        // animator doesn't try to play Land/Fall.
        var newJump = newActive.GetComponent<PlayerJump>();
        if (newJump != null) newJump.ForceExitAirState();

        // STEP 6 — Nuke any leftover animator state machine state (Fall/Land/etc.)
        // by rebinding. This is the equivalent of resetting the animator from
        // scratch — guarantees the new character starts from the default state.
        var newAnim = newActive.GetComponentInChildren<Animator>();
        if (newAnim != null && newAnim.runtimeAnimatorController != null)
        {
            newAnim.Rebind();
            newAnim.Update(0f);
        }

        currentControlled = newActive;

        // Freeze the world for a brief moment so the swap VFX + sprite swap
        // read clearly. Uses GamePause (Time.timeScale = 0).
        if (pauseWorldDuringSwap && swapPauseDuration > 0f)
            StartCoroutine(RunSwapPause());

        if (debugLog)
        {
            string groundedInfo = newCC != null ? newCC.isGrounded.ToString() : "no CC";
            Debug.Log($"[PartyRoot] Swapped → '{newActive.name}'.\n" +
                      $"  Old controlled pos = {swapPos}\n" +
                      $"  New char pos BEFORE sync = {jynBeforeSync}\n" +
                      $"  New char pos AFTER sync = {newActive.transform.position}\n" +
                      $"  CC.isGrounded after Move = {groundedInfo}", this);
        }
    }

    /// <summary>
    /// Raycast straight down from above the new character to find solid
    /// ground; teleport its transform onto that ground so the CC reports
    /// isGrounded immediately. Skips hits that are the player's own colliders.
    /// </summary>
    static readonly RaycastHit[] s_groundHits = new RaycastHit[16];

    /// <summary>True if the given transform is part of ANY registered party
    /// member's hierarchy. Used by the ground-snap raycast so it doesn't
    /// mistake another character's CC for "the floor".</summary>
    static bool IsPartyMemberCollider(Transform t)
    {
        if (t == null || Instance == null || Instance.members == null) return false;
        for (int i = 0; i < Instance.members.Length; i++)
        {
            var m = Instance.members[i];
            if (m == null || m.root == null) continue;
            if (t.IsChildOf(m.root.transform)) return true;
        }
        return false;
    }

    static void SnapNewActiveToGround(PlayerHealth newActive, CharacterController cc)
    {
        if (newActive == null || cc == null) return;

        // Start the ray clearly ABOVE the CC's top so we never query from
        // inside the player's own capsule.
        Vector3 feetWorld = newActive.transform.position;
        Vector3 origin = feetWorld + Vector3.up * (cc.height + 1f);

        const float maxDistance = 30f;

        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, s_groundHits,
            maxDistance, ~0, QueryTriggerInteraction.Ignore);

        if (hitCount == 0) return;

        // Find the closest hit that ISN'T part of ANY party member (their
        // colliders / CCs would falsely register as "ground").
        float bestDist = float.MaxValue;
        RaycastHit best = default;
        bool foundAny = false;

        for (int i = 0; i < hitCount; i++)
        {
            var h = s_groundHits[i];
            if (h.collider == null) continue;
            if (IsPartyMemberCollider(h.collider.transform)) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                foundAny = true;
            }
        }

        if (!foundAny) return;

        // Place transform so the CC's bottom hemisphere lands at best.point.
        // feetWorld = transform.position + cc.center - height/2
        // → transform = hit.point - cc.center + height/2.
        Vector3 desired = best.point - cc.center + new Vector3(0f, cc.height * 0.5f, 0f);

        cc.enabled = false;
        newActive.transform.position = desired;
        cc.enabled = true;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Sets every ParticleSystem (including children) inside the instantiated
    /// VFX to use unscaled time, so they animate even with Time.timeScale = 0
    /// during the swap pause.
    /// </summary>
    static void ForceUnscaledTimeOnParticleSystems(GameObject vfx)
    {
        if (vfx == null) return;
        var systems = vfx.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.useUnscaledTime = true;
        }
    }

    System.Collections.IEnumerator RunSwapPause()
    {
        GamePause.SetPaused(true, freezeTime: true);

        float t = 0f;
        while (t < swapPauseDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        GamePause.SetPaused(false, freezeTime: true);
    }

    /// <summary>
    /// Set every in-party member's controlled state. Only `controlled` becomes
    /// the controlled one; everyone else is dormant.
    /// </summary>
    void ApplyControlState(PlayerHealth controlled)
    {
        if (members == null) return;

        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null || m.root == null) continue;
            if (!m.root.activeSelf) continue;

            var ctrl = m.root.GetComponent<PartyMemberControl>();
            if (ctrl == null) continue;

            bool isControlled = (m.root == controlled.gameObject);
            ctrl.SetControlled(isControlled);
        }
    }

    // ============================================================
    // MEMBERSHIP
    // ============================================================

    void HideAll()
    {
        if (members == null) return;
        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null || m.root == null) continue;
            if (m.root.activeSelf) m.root.SetActive(false);
        }
    }

    /// <summary>
    /// Activate every in-party member's GameObject and set the active character
    /// as controlled. Called by SaveManager on New Game.
    /// </summary>
    public void ApplyNewGameConfig()
    {
        if (members == null) return;

        CharacterDefinition chosenActive = null;
        CharacterDefinition firstPresent = null;

        // Step 1a: defensively HIDE anyone NOT in party (catches cases where
        // they might have been left active in the editor or by another script).
        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null || m.root == null) continue;
            if (m.isInParty) continue;

            if (m.root.activeSelf)
            {
                m.root.SetActive(false);
                if (debugLog)
                    Debug.Log($"[PartyRoot] Hid '{m.character?.displayName}' (Is In Party = false).", this);
            }
        }

        // Step 1b: enable every in-party member's GameObject. Their PlayerHealth
        // registers with Party.Members automatically.
        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null || m.character == null || m.root == null) continue;
            if (!m.isInParty) continue;

            if (!m.root.activeSelf) m.root.SetActive(true);

            if (firstPresent == null) firstPresent = m.character;
            if (m.isActiveCharacter && chosenActive == null) chosenActive = m.character;
        }

        if (debugLog)
            Debug.Log($"[PartyRoot] ApplyNewGameConfig: in-party count = {Party.MemberCount}, chosenActive = '{chosenActive?.displayName}'.", this);

        if (chosenActive == null) chosenActive = firstPresent;

        if (chosenActive != null)
        {
            Party.SetActiveByCharacterId(chosenActive.id);

            // Party.SetActive only fires OnActiveChanged when the value actually
            // changes. Since Party.Active falls back to PlayerHealth.All[0] when
            // explicitActive is null, it can already equal `chosenActive` at this
            // point (no event, no HandleActiveChanged, currentControlled stays
            // null, LateUpdate never syncs the hidden member, first swap puts
            // them at PartyRoot's position). Force the setup explicitly.
            var ph = Party.Active;
            if (ph != null && currentControlled != ph)
            {
                ApplyControlState(ph);
                currentControlled = ph;
                SyncHiddenToControlled();

                if (debugLog)
                    Debug.Log($"[PartyRoot] Forced initial setup: currentControlled='{ph.name}'.", this);
            }
        }
    }

    public bool ShowMember(CharacterDefinition def)
    {
        var m = FindSlot(def);
        if (m == null || m.root == null) return false;
        if (!m.root.activeSelf)
        {
            m.root.SetActive(true);
            if (debugLog) Debug.Log($"[PartyRoot] Added '{def.displayName}' to party.", this);
        }
        return true;
    }

    public bool HideMember(CharacterDefinition def)
    {
        var m = FindSlot(def);
        if (m == null || m.root == null) return false;
        if (m.root.activeSelf)
        {
            m.root.SetActive(false);
            if (debugLog) Debug.Log($"[PartyRoot] Removed '{def.displayName}' from party.", this);
        }
        return true;
    }

    public bool IsPresent(CharacterDefinition def)
    {
        var m = FindSlot(def);
        return m != null && m.root != null && m.root.activeSelf;
    }

    public GameObject FindRootForCharacter(CharacterDefinition def)
    {
        var m = FindSlot(def);
        return m != null ? m.root : null;
    }

    public GameObject FindRootForCharacterId(string id)
    {
        if (string.IsNullOrEmpty(id) || members == null) return null;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] == null || members[i].character == null) continue;
            if (members[i].character.id == id) return members[i].root;
        }
        return null;
    }

    public IReadOnlyList<MemberSlot> AllSlots => members;

    MemberSlot FindSlot(CharacterDefinition def)
    {
        if (def == null || members == null) return null;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i] != null && members[i].character == def) return members[i];
        }
        return null;
    }

    // ============================================================
    // DEBUG — right-click the component in the Inspector during Play
    // to invoke these and test the system.
    // ============================================================

    [ContextMenu("Debug/0 — Print Slot Mapping")]
    void Debug_PrintSlots()
    {
        if (members == null) { Debug.Log("[PartyRoot] No members configured."); return; }
        Debug.Log("[PartyRoot] === SLOT MAPPING ===", this);
        for (int i = 0; i < members.Length; i++)
        {
            var m = members[i];
            if (m == null) { Debug.Log($"  Slot {i} = (null)"); continue; }
            string charName = m.character != null ? m.character.displayName : "(no character)";
            string inParty = m.isInParty ? "✓" : "✗";
            string active = m.isActiveCharacter ? "✓" : "✗";
            string sceneActive = (m.root != null && m.root.activeSelf) ? "ON" : "OFF";
            Debug.Log($"  Slot {i} = '{charName}'  | InParty={inParty}  ActiveChar={active}  | Scene SetActive={sceneActive}");
        }
    }

    [ContextMenu("Debug/1 — Log Party Live State")]
    void Debug_LogState()
    {
        Debug.Log($"[PartyRoot] === LIVE STATE === Party.Members.Count = {Party.MemberCount}", this);
        for (int i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            string activeTag = Party.IsActive(m) ? "  ★ ACTIVE" : "";
            string charName = m.character != null ? m.character.displayName : "(no character)";
            Debug.Log($"  In party [{i}] '{charName}' (GO: {m.name}, pos: {m.transform.position}){activeTag}", m);
        }
    }

    [ContextMenu("Debug/Toggle In Party — Slot 0")]
    void Debug_Toggle0() => DebugToggleSlot(0);

    [ContextMenu("Debug/Toggle In Party — Slot 1")]
    void Debug_Toggle1() => DebugToggleSlot(1);

    [ContextMenu("Debug/Set Active — Slot 0")]
    void Debug_Active0() => DebugSetActiveSlot(0);

    [ContextMenu("Debug/Set Active — Slot 1")]
    void Debug_Active1() => DebugSetActiveSlot(1);

    void DebugToggleSlot(int slotIdx)
    {
        if (members == null || slotIdx < 0 || slotIdx >= members.Length)
        {
            Debug.LogWarning($"[PartyRoot] Slot {slotIdx} out of range.");
            return;
        }
        var m = members[slotIdx];
        if (m == null || m.character == null || m.root == null)
        {
            Debug.LogWarning($"[PartyRoot] Slot {slotIdx} is not properly configured.");
            return;
        }

        string name = m.character.displayName;

        if (IsPresent(m.character))
        {
            HideMember(m.character);
            Debug.Log($"[PartyRoot] ⛔ REMOVED '{name}' (Slot {slotIdx}) from party.", this);
            if (Party.MemberCount > 0 && Party.Active == null)
                Party.SetActive(Party.Members[0]);
        }
        else
        {
            ShowMember(m.character);
            SyncHiddenToControlled();
            Debug.Log($"[PartyRoot] ✅ ADDED '{name}' (Slot {slotIdx}) to party.", this);
            if (Party.Active == null)
                Party.SetActiveByCharacterId(m.character.id);
        }
    }

    void DebugSetActiveSlot(int slotIdx)
    {
        if (members == null || slotIdx < 0 || slotIdx >= members.Length)
        {
            Debug.LogWarning($"[PartyRoot] Slot {slotIdx} out of range.");
            return;
        }
        var m = members[slotIdx];
        if (m == null || m.character == null)
        {
            Debug.LogWarning($"[PartyRoot] Slot {slotIdx} is not properly configured.");
            return;
        }

        string name = m.character.displayName;

        if (!IsPresent(m.character))
        {
            Debug.LogWarning($"[PartyRoot] '{name}' (Slot {slotIdx}) is NOT in the party — add them first with Toggle.", this);
            return;
        }

        bool ok = Party.SetActiveByCharacterId(m.character.id);
        Debug.Log($"[PartyRoot] ★ Set ACTIVE → '{name}' (Slot {slotIdx}) → {ok}.", this);
    }
}
