using UnityEngine;

/// <summary>
/// Loads <see cref="UiKitSprites"/> for play mode / builds.
/// Prefer Resources; Editor Play falls back to the project asset path.
/// </summary>
public static class UiKitRuntime
{
    const string ResourcesName = "UiKitSprites";
    static UiKitSprites cached;
    static bool tried;

    public static UiKitSprites Get()
    {
        if (cached != null)
            return cached;
        if (tried)
            return null;

        tried = true;
        cached = Resources.Load<UiKitSprites>(ResourcesName);

#if UNITY_EDITOR
        if (cached == null)
        {
            cached = UnityEditor.AssetDatabase.LoadAssetAtPath<UiKitSprites>(UiKitSprites.AssetPath);
            if (cached == null)
                cached = UnityEditor.AssetDatabase.LoadAssetAtPath<UiKitSprites>(
                    "Assets/Game/Resources/UiKitSprites.asset");
        }
#endif

        if (cached == null || cached.dialogueFrame == null)
        {
            Debug.LogWarning(
                "[UI Kit] UiKitSprites missing or empty. Run Tools → ARPG → UI → Rebuild UI Kit Visual Test.");
            cached = null;
        }

        return cached;
    }

    public static bool HasKit
    {
        get
        {
            var kit = Get();
            return kit != null && kit.dialogueFrame != null && kit.notificationFrame != null;
        }
    }

#if UNITY_EDITOR
    public static void ClearCache()
    {
        cached = null;
        tried = false;
    }
#endif
}
