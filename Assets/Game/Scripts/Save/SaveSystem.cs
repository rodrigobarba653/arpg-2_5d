using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Disk I/O for SaveData. Files live under Application.persistentDataPath/saves/.
/// One JSON file per slot, e.g. slot0.json, slot1.json...
///
/// Stateless static helpers: load when you load, save when you save. The
/// SaveManager is who fills SaveData from / pushes into the running scene.
/// </summary>
public static class SaveSystem
{
    public const int MaxSlots = 3;
    const string SubFolder = "saves";

    static string SavesFolder => Path.Combine(Application.persistentDataPath, SubFolder);
    static string PathForSlot(int slot) => Path.Combine(SavesFolder, $"slot{slot}.json");

    public static void EnsureFolder()
    {
        if (!Directory.Exists(SavesFolder))
            Directory.CreateDirectory(SavesFolder);
    }

    public static bool HasSlot(int slot)
    {
        return File.Exists(PathForSlot(slot));
    }

    public static void Save(int slot, SaveData data)
    {
        if (data == null) return;

        EnsureFolder();

        data.slot = slot;
        data.timestamp = DateTime.Now.ToString("o");

        string json = JsonUtility.ToJson(data, prettyPrint: true);

        try
        {
            File.WriteAllText(PathForSlot(slot), json);
            Debug.Log($"[SaveSystem] Saved slot {slot} → {PathForSlot(slot)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to save slot {slot}: {e.Message}");
        }
    }

    public static SaveData Load(int slot)
    {
        string path = PathForSlot(slot);
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data != null) data.slot = slot;
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to load slot {slot}: {e.Message}");
            return null;
        }
    }

    public static bool Delete(int slot)
    {
        string path = PathForSlot(slot);
        if (!File.Exists(path)) return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to delete slot {slot}: {e.Message}");
            return false;
        }
    }

    /// <summary>Returns lightweight metadata for each slot — used to render the Load Game UI.</summary>
    public static List<SaveData> GetAllSlots()
    {
        var list = new List<SaveData>();

        for (int i = 0; i < MaxSlots; i++)
        {
            var data = Load(i);
            list.Add(data); // null if the slot is empty
        }

        return list;
    }
}
