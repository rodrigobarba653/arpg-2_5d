using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central registry of every ItemDefinition in the project. Lookup by id, by
/// category, or iterate all. Useful for save/load, drop tables, recipes, and
/// console commands.
///
/// Create ONE asset of this type and place it inside a folder called "Resources"
/// (e.g. Assets/Resources/ItemDatabase.asset). The static Instance auto-loads it.
///
/// In the editor you can click "Auto Populate From Project" to scan and fill
/// the list automatically — no need to drag every item by hand.
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "ARPG/Items/Item Database")]
public class ItemDatabase : ScriptableObject
{
    static ItemDatabase _instance;

    public static ItemDatabase Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Resources.Load<ItemDatabase>("ItemDatabase");

            if (_instance == null)
            {
                Debug.LogWarning("[ItemDatabase] No ItemDatabase.asset found in any Resources folder. " +
                                 "Create one via Assets > Create > ARPG > Items > Item Database, " +
                                 "place it in Assets/Resources/, and click Auto Populate From Project.");
            }

            return _instance;
        }
    }

    [Tooltip("Every item known to the game. Use Auto Populate to fill from the project.")]
    [SerializeField] List<ItemDefinition> items = new List<ItemDefinition>();

    public IReadOnlyList<ItemDefinition> Items => items;

    // Internal lookup tables (rebuilt lazily)
    Dictionary<string, ItemDefinition> byId;

    public ItemDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        EnsureCaches();
        byId.TryGetValue(id, out var found);
        return found;
    }

    public List<ItemDefinition> GetByCategory(ItemCategory category)
    {
        var result = new List<ItemDefinition>();
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null && items[i].Category == category)
                result.Add(items[i]);

        return result;
    }

    public List<T> GetAllOfType<T>() where T : ItemDefinition
    {
        var result = new List<T>();
        for (int i = 0; i < items.Count; i++)
            if (items[i] is T t) result.Add(t);

        return result;
    }

    void EnsureCaches()
    {
        if (byId != null) return;

        byId = new Dictionary<string, ItemDefinition>();

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;

            if (string.IsNullOrEmpty(it.id))
            {
                Debug.LogWarning($"[ItemDatabase] Item '{it.name}' has an empty id — skipped in lookup.", it);
                continue;
            }

            if (byId.ContainsKey(it.id))
            {
                Debug.LogWarning($"[ItemDatabase] Duplicate item id '{it.id}' between " +
                                 $"'{byId[it.id].name}' and '{it.name}'. Keeping the first.", this);
                continue;
            }

            byId[it.id] = it;
        }
    }

    public void InvalidateCache() => byId = null;

    // ============================================================
    // EDITOR-ONLY: auto-populate by scanning all assets of type ItemDefinition.
    // ============================================================
#if UNITY_EDITOR
    [ContextMenu("Auto Populate From Project")]
    void AutoPopulate()
    {
        items.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item != null && !items.Contains(item))
                items.Add(item);
        }

        InvalidateCache();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssetIfDirty(this);

        Debug.Log($"[ItemDatabase] Auto-populated with {items.Count} items.", this);
    }

    [ContextMenu("Validate IDs")]
    void ValidateIds()
    {
        var seen = new HashSet<string>();
        int empty = 0, dupes = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;

            if (string.IsNullOrEmpty(items[i].id))
            {
                Debug.LogWarning($"  · '{items[i].name}' has an empty id.", items[i]);
                empty++;
                continue;
            }

            if (!seen.Add(items[i].id))
            {
                Debug.LogWarning($"  · Duplicate id '{items[i].id}' on '{items[i].name}'.", items[i]);
                dupes++;
            }
        }

        Debug.Log($"[ItemDatabase] Validation: {items.Count} items, {empty} empty ids, {dupes} duplicates.");
    }
#endif
}
