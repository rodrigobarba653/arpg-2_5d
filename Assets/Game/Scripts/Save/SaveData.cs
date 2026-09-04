using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of everything that needs to survive between play sessions.
/// Plain serializable class — Unity's JsonUtility writes it straight to disk.
///
/// Add fields as new systems come online. Keep them simple types (int, float,
/// string, Vector3, List of serializable structs).
/// </summary>
[Serializable]
public class SaveData
{
    // ===== Metadata =====
    public int slot;
    public string saveName = "";
    public string timestamp = "";   // ISO 8601 e.g. "2026-05-22T10:34:00"
    public float playTimeSeconds;
    public int saveVersion = 1;     // bump if format changes

    // ===== Scene & world =====
    public string sceneName = "";
    public string currentLocation = "";

    // ===== Player transform =====
    public Vector3 playerPosition;
    public Vector3 playerEulerAngles;

    // ===== Player character & stats =====
    public string characterId = "";  // CharacterDefinition.id (legacy single-character field)

    [Tooltip("Character id of the currently-controlled party member. Restored " +
             "on Load via Party.SetActiveByCharacterId.")]
    public string activeCharacterId = "";

    public int level = 1;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public int maxMana;
    public int currentAP;
    public int maxAP;

    // ===== Wallet =====
    public int varium;

    // ===== Inventory =====
    public List<SavedItem> items = new List<SavedItem>();

    // ===== Equipment (legacy single-character) =====
    [Tooltip("Legacy field for single-character saves. New saves use partyEquipment.")]
    public string equippedWeaponId = "";

    // ===== Equipment (per character — supports party) =====
    public List<SavedCharacterEquipment> partyEquipment = new List<SavedCharacterEquipment>();

    // ===== Stats (per character — supports party) =====
    [Tooltip("HP/MP per party member. New saves use this; legacy single-" +
             "character fields above (currentHealth/maxHealth/currentMana/" +
             "maxMana) are only read as a fallback for old saves.")]
    public List<SavedCharacterStats> partyStats = new List<SavedCharacterStats>();

    // ===== Magic (per character — supports party) =====
    [Tooltip("Which SpellItem is bound to each of the 3 cast-wheel slots, " +
             "per party member. The party-wide pool of KNOWN spells doesn't " +
             "need its own list — it's just whatever SpellItems are in the " +
             "shared inventory (already covered by 'items' above).")]
    public List<SavedCharacterMagic> partyMagic = new List<SavedCharacterMagic>();

    // ===== World state =====
    [Tooltip("IDs of pickups that have been collected. Their ItemPickup will " +
             "hide itself on Awake when its pickupId appears in this list.")]
    public List<string> consumedPickupIds = new List<string>();
}

/// <summary>One row of inventory storage in the save file.</summary>
[Serializable]
public struct SavedItem
{
    public string itemId;
    public int quantity;
}

/// <summary>
/// Equipment state of one party member. Matched on load via characterId
/// (PlayerHealth.character.id). All ids resolve to ScriptableObjects via
/// ItemDatabase.
/// </summary>
[Serializable]
public struct SavedCharacterEquipment
{
    public string characterId;
    public string weaponId;
    // Generic slot 1/2/3 — replaces the legacy handle/guard/blade fields.
    public string slot1PartId;
    public string slot2PartId;
    public string slot3PartId;

    // Legacy fields, only read if slot1..3 are empty (backward compat for old saves).
    public string handlePartId;
    public string guardPartId;
    public string bladePartId;
}

/// <summary>HP/MP of one party member, matched on load via characterId.</summary>
[Serializable]
public struct SavedCharacterStats
{
    public string characterId;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public int maxMana;
}

/// <summary>
/// Which SpellItem (by id, resolved via ItemDatabase) is equipped in each of
/// one party member's 3 cast-wheel slots. Matched on load via characterId.
/// An empty slotXSpellId means that slot was empty.
/// </summary>
[Serializable]
public struct SavedCharacterMagic
{
    public string characterId;
    public string slot1SpellId;
    public string slot2SpellId;
    public string slot3SpellId;
}
