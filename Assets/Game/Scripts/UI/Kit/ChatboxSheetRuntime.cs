using UnityEngine;

/// <summary>Loads <see cref="ChatboxSheetCatalog"/> for play mode.</summary>
public static class ChatboxSheetRuntime
{
    const string ResourcesName = "ChatboxSheetCatalog";
    static ChatboxSheetCatalog cached;

    public static ChatboxSheetCatalog Get()
    {
        if (cached != null)
            return cached;

        cached = Resources.Load<ChatboxSheetCatalog>(ResourcesName);
#if UNITY_EDITOR
        if (cached == null)
        {
            cached = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatboxSheetCatalog>(ChatboxSheetCatalog.AssetPath);
            if (cached == null)
                cached = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatboxSheetCatalog>(ChatboxSheetCatalog.ResourcesPath);
        }
#endif
        if (cached == null)
        {
            Debug.LogWarning(
                "[Chatbox Sheet] Catalog missing. Run Tools → ARPG → UI → Slice Chatbox Spritesheet.");
        }

        return cached;
    }

    public static void ClearCache() => cached = null;
}
