using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Slices <c>chat-notificaction-spritesheet.png</c> into named sprites and fills ChatboxSheetCatalog.
/// Menu: Tools → ARPG → UI → Slice Chatbox Spritesheet
/// </summary>
public static class ChatboxSheetImporter
{
    const string Menu = "Tools/ARPG/UI/Slice Chatbox Spritesheet";

    [MenuItem(Menu, false, 210)]
    public static void SliceAndAssign()
    {
        string path = ChatboxSheetCatalog.SheetPath;
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("Chatbox Sheet", $"Missing sheet:\n{path}", "OK");
            return;
        }

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Chatbox Sheet", "Could not open TextureImporter.", "OK");
            return;
        }

        var sprites = new List<SpriteMetaData>();
        for (int row = 0; row < ChatboxSheetCatalog.TailRows; row++)
        {
            var tail = (ChatboxSheetTail)row;
            for (int col = 0; col < ChatboxSheetCatalog.Columns; col++)
            {
                var column = (ChatboxSheetColumn)col;
                sprites.Add(MakeMeta(
                    ChatboxSheetCatalog.SpriteName(column, tail),
                    col,
                    row));
            }
        }

        // Item notification row (14th): large = col0, small = col1
        sprites.Add(MakeMeta(ChatboxSheetCatalog.ItemSpriteName(true), 0, 13));
        sprites.Add(MakeMeta(ChatboxSheetCatalog.ItemSpriteName(false), 1, 13));

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 16384;
        importer.spritesheet = sprites.ToArray();

        // Tall sheet must stay above 2048 or cells crush.
        var def = importer.GetDefaultPlatformTextureSettings();
        def.maxTextureSize = 16384;
        def.format = TextureImporterFormat.RGBA32;
        def.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(def);

        var standalone = new TextureImporterPlatformSettings
        {
            name = "Standalone",
            overridden = true,
            maxTextureSize = 16384,
            format = TextureImporterFormat.RGBA32,
            textureCompression = TextureImporterCompression.Uncompressed,
        };
        importer.SetPlatformTextureSettings(standalone);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Multiple;
        settings.spritePixelsPerUnit = 100f;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var catalog = EnsureCatalog();
        AssignCatalog(catalog);
        EditorUtility.SetDirty(catalog);
        EnsureResourcesCopy(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(catalog);
        Debug.Log($"[Chatbox Sheet] Sliced {sprites.Count} sprites → {ChatboxSheetCatalog.AssetPath}");
    }

    static SpriteMetaData MakeMeta(string name, int col, int row)
    {
        const int SheetHeight = 15600; // PNG pixel height
        int x = col * ChatboxSheetCatalog.CellWidth;
        // Unity sprite rect Y is bottom-up; rows are packed from the top of the PNG.
        int y = SheetHeight - (row + 1) * ChatboxSheetCatalog.CellHeight;

        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(x, y, ChatboxSheetCatalog.CellWidth, ChatboxSheetCatalog.CellHeight),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            border = Vector4.zero,
        };
    }

    static ChatboxSheetCatalog EnsureCatalog()
    {
        var cat = AssetDatabase.LoadAssetAtPath<ChatboxSheetCatalog>(ChatboxSheetCatalog.AssetPath);
        if (cat != null)
            return cat;

        Directory.CreateDirectory(Path.GetDirectoryName(ChatboxSheetCatalog.AssetPath));
        cat = ScriptableObject.CreateInstance<ChatboxSheetCatalog>();
        AssetDatabase.CreateAsset(cat, ChatboxSheetCatalog.AssetPath);
        return cat;
    }

    static void AssignCatalog(ChatboxSheetCatalog catalog)
    {
        catalog.largePortrait = new Sprite[ChatboxSheetCatalog.TailRows];
        catalog.largePlain = new Sprite[ChatboxSheetCatalog.TailRows];
        catalog.small = new Sprite[ChatboxSheetCatalog.TailRows];

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(ChatboxSheetCatalog.SheetPath);
        var map = new Dictionary<string, Sprite>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Sprite sp)
                map[sp.name] = sp;
        }

        for (int row = 0; row < ChatboxSheetCatalog.TailRows; row++)
        {
            var tail = (ChatboxSheetTail)row;
            catalog.largePortrait[row] = Find(map, ChatboxSheetCatalog.SpriteName(ChatboxSheetColumn.LargePortrait, tail));
            catalog.largePlain[row] = Find(map, ChatboxSheetCatalog.SpriteName(ChatboxSheetColumn.LargePlain, tail));
            catalog.small[row] = Find(map, ChatboxSheetCatalog.SpriteName(ChatboxSheetColumn.Small, tail));
        }

        catalog.itemNotificationLarge = Find(map, ChatboxSheetCatalog.ItemSpriteName(true));
        catalog.itemNotificationSmall = Find(map, ChatboxSheetCatalog.ItemSpriteName(false));
    }

    static Sprite Find(Dictionary<string, Sprite> map, string name)
    {
        if (map.TryGetValue(name, out var sp))
            return sp;
        Debug.LogWarning($"[Chatbox Sheet] Missing sprite '{name}'.");
        return null;
    }

    static void EnsureResourcesCopy(ChatboxSheetCatalog source)
    {
        Directory.CreateDirectory("Assets/Game/Resources");
        string dest = ChatboxSheetCatalog.ResourcesPath;
        if (AssetDatabase.LoadAssetAtPath<ChatboxSheetCatalog>(dest) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), dest);

        var existing = AssetDatabase.LoadAssetAtPath<ChatboxSheetCatalog>(dest);
        if (existing == null)
            return;

        // Mirror assignments
        existing.largePortrait = source.largePortrait;
        existing.largePlain = source.largePlain;
        existing.small = source.small;
        existing.itemNotificationLarge = source.itemNotificationLarge;
        existing.itemNotificationSmall = source.itemNotificationSmall;
        EditorUtility.SetDirty(existing);
    }
}
