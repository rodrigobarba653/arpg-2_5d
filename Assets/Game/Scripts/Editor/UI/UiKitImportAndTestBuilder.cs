using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Configures UI Kit v2 PNG import settings from README.txt and builds a visual test scene.
/// Menu: Tools → ARPG → UI → Configure UI Kit Sprites / Build UI Kit Visual Test Scene
/// </summary>
public static class UiKitImportAndTestBuilder
{
    const string ScenePath = "Assets/Game/Scenes/UIKit_VisualTest.unity";
    const string ResourcesAssetPath = "Assets/Game/Resources/UiKitSprites.asset";
    const string MenuRoot = "Tools/ARPG/UI/";

    struct SliceSpec
    {
        public string fileName;
        public Vector4 border; // L, B, R, T
        public bool sliced;
    }

    static readonly SliceSpec[] Specs =
    {
        new SliceSpec { fileName = "dialogue_frame_9slice.png", border = new Vector4(264, 100, 264, 100), sliced = true },
        new SliceSpec { fileName = "dialogue_background_9slice.png", border = new Vector4(264, 100, 264, 100), sliced = true },
        new SliceSpec { fileName = "notification_frame_9slice.png", border = new Vector4(230, 60, 100, 60), sliced = true },
        new SliceSpec { fileName = "notification_background_9slice.png", border = new Vector4(230, 60, 100, 60), sliced = true },
        new SliceSpec { fileName = "notification_icon_frame.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "portrait_frame.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "portrait_background.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_bottom_right_angle-bg.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_bottom_right_angle-ot.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_top_right_angle-bg.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_top_right_angle-ot.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_left_right_angle-bg.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_left_right_angle-ot.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_right_right_angle-bg.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_right_right_angle-ot.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "tail_connection_mask.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "continue_indicator.png", border = Vector4.zero, sliced = false },
        // Corner/edge alternatives — import as Simple sprites, but visual test does NOT render them
        // alongside the assembled 9-slice (README: do not render both at once).
        new SliceSpec { fileName = "corner_top_left.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "corner_top_right.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "corner_bottom_left.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "corner_bottom_right.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "edge_top.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "edge_bottom.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "edge_left.png", border = Vector4.zero, sliced = false },
        new SliceSpec { fileName = "edge_right.png", border = Vector4.zero, sliced = false },
    };

    [MenuItem(MenuRoot + "1. Configure UI Kit Sprites", false, 200)]
    public static void ConfigureSprites()
    {
        int configured = 0;
        var paths = new List<string>();
        for (int i = 0; i < Specs.Length; i++)
        {
            string path = Path.Combine(UiKitSprites.PngFolder, Specs[i].fileName).Replace('\\', '/');
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[UI Kit] Missing PNG: {path}");
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[UI Kit] No TextureImporter for {path}");
                continue;
            }

            // PPU 100 matches Canvas.referencePixelsPerUnit so 9-slice borders
            // scale via Image.pixelsPerUnitMultiplier (PPU 1 makes borders clamp forever).
            float ppu = 100f;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spriteBorder = Specs[i].border;

            // Mesh Type Full Rect (API varies by Unity version — use settings bag).
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = ppu;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            paths.Add(path);
            configured++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var kit = EnsureKitAsset();
        AssignSprites(kit);
        EditorUtility.SetDirty(kit);
        // Runtime loader uses Resources.Load("UiKitSprites").
        EnsureResourcesCopy(kit);
        AssetDatabase.SaveAssets();
        UiKitRuntime.ClearCache();

        Debug.Log($"[UI Kit] Configured {configured} sprites + refreshed {UiKitSprites.AssetPath} / Resources. Borders from README.txt.");
        EditorGUIUtility.PingObject(kit);
    }

    [MenuItem(MenuRoot + "2. Build UI Kit Visual Test Scene", false, 201)]
    public static void BuildVisualTestScene()
    {
        ConfigureSprites();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // NewScene can invalidate previously loaded asset refs — reload after.
        var kit = AssetDatabase.LoadAssetAtPath<UiKitSprites>(UiKitSprites.AssetPath);
        if (kit == null)
            kit = EnsureKitAsset();
        if (kit != null)
        {
            AssignSprites(kit);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
        }

        if (kit == null || kit.dialogueFrame == null)
        {
            EditorUtility.DisplayDialog(
                "UI Kit",
                "Sprites not ready. Run Configure UI Kit Sprites first and wait for import.",
                "OK");
            return;
        }

        // Neutral backdrop so coral/cyan read correctly.
        Camera.main.backgroundColor = new Color(0.07f, 0.06f, 0.09f, 1f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        var canvasGo = new GameObject("UIKit_TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRt = canvasGo.GetComponent<RectTransform>();

        var font = FindDogica();

        // --- Three chatbox widths (bottom tails, with portrait) ---
        float[] widths = { 420f, 640f, 900f };
        float chatY = 220f;
        for (int i = 0; i < widths.Length; i++)
        {
            float x = -520f + i * 520f;
            var view = DialogueBubbleKitBuilder.Build(
                canvasRt,
                $"Chatbox_W{widths[i]:0}",
                kit,
                new Vector2(widths[i], 220f),
                DialogueChatboxTailDirection.Bottom,
                withPortraitSlot: true,
                speaker: "Ax",
                body: widths[i] < 500f
                    ? "Short line."
                    : "A longer line of dialogue that expands the box dynamically without stretching the corners.",
                portraitSprite: null,
                font: font);
            view.rectTransform.anchoredPosition = new Vector2(x, chatY);
            AddLabel(canvasRt, $"width {widths[i]:0}", new Vector2(x, chatY + 140f), font);
        }

        // --- Four pointer directions (medium width, no portrait so full text column) ---
        var dirs = new[]
        {
            DialogueChatboxTailDirection.Bottom,
            DialogueChatboxTailDirection.Top,
            DialogueChatboxTailDirection.Left,
            DialogueChatboxTailDirection.Right,
        };
        float dirY = -80f;
        for (int i = 0; i < dirs.Length; i++)
        {
            float x = -540f + i * 360f;
            var view = DialogueBubbleKitBuilder.Build(
                canvasRt,
                $"Chatbox_Tail_{dirs[i]}",
                kit,
                new Vector2(300f, 160f),
                dirs[i],
                withPortraitSlot: false,
                speaker: "Jyn",
                body: $"Tail → {dirs[i]}",
                font: font);
            view.rectTransform.anchoredPosition = new Vector2(x, dirY);
            AddLabel(canvasRt, dirs[i].ToString(), new Vector2(x, dirY + 110f), font);
        }

        // --- Two notification widths (same slice PPU as chatboxes, shorter height) ---
        float noteY = -320f;
        float noteH = NotificationKitBuilder.DefaultHeight;
        NotificationKitBuilder.Build(
            canvasRt,
            "Notification_Compact",
            kit,
            new Vector2(420f, noteH),
            "Ether Potion x1 obtenido.",
            kit.notificationIconPotion,
            font).anchoredPosition = new Vector2(-280f, noteY);

        NotificationKitBuilder.Build(
            canvasRt,
            "Notification_Wide",
            kit,
            new Vector2(720f, noteH),
            "Varium x25 — wide banner check",
            kit.notificationIconVarium,
            font).anchoredPosition = new Vector2(280f, noteY);

        AddLabel(canvasRt, "Notifications (no pointer)", new Vector2(0f, noteY + 80f), font);
        AddLabel(
            canvasRt,
            "UI Kit v2 Visual Test — check: corners sharp, tails seamless, portraits not stretched, 9-slice only (no dual assembly)",
            new Vector2(0f, 480f),
            font);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log($"[UI Kit] Visual test scene saved: {ScenePath}");
    }

    static UiKitSprites EnsureKitAsset()
    {
        var kit = AssetDatabase.LoadAssetAtPath<UiKitSprites>(UiKitSprites.AssetPath);
        if (kit != null)
            return kit;

        Directory.CreateDirectory(Path.GetDirectoryName(UiKitSprites.AssetPath));
        kit = ScriptableObject.CreateInstance<UiKitSprites>();
        AssetDatabase.CreateAsset(kit, UiKitSprites.AssetPath);
        return kit;
    }

    static void EnsureResourcesCopy(UiKitSprites source)
    {
        if (source == null)
            return;

        Directory.CreateDirectory("Assets/Game/Resources");
        var existing = AssetDatabase.LoadAssetAtPath<UiKitSprites>(ResourcesAssetPath);
        if (existing == null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), ResourcesAssetPath);
            existing = AssetDatabase.LoadAssetAtPath<UiKitSprites>(ResourcesAssetPath);
        }

        if (existing == null)
            return;

        AssignSprites(existing);
        EditorUtility.SetDirty(existing);
    }

    static void AssignSprites(UiKitSprites kit)
    {
        kit.dialogueFrame = LoadSprite("dialogue_frame_9slice.png");
        kit.dialogueBackground = LoadSprite("dialogue_background_9slice.png");
        kit.notificationFrame = LoadSprite("notification_frame_9slice.png");
        kit.notificationBackground = LoadSprite("notification_background_9slice.png");
        kit.notificationIconFrame = LoadSprite("notification_icon_frame.png");
        kit.notificationIconVarium = LoadNotificationIcon("varium.png");
        kit.notificationIconPotion = LoadNotificationIcon("potion.png");
        kit.notificationIconEther = LoadNotificationIcon("ether.png");
        kit.notificationIconGear = LoadNotificationIcon("gear.png");
        kit.portraitFrame = LoadSprite("portrait_frame.png");
        kit.portraitBackground = LoadSprite("portrait_background.png");
        kit.tailBottomBg = LoadSprite("tail_bottom_right_angle-bg.png");
        kit.tailTopBg = LoadSprite("tail_top_right_angle-bg.png");
        kit.tailLeftBg = LoadSprite("tail_left_right_angle-bg.png");
        kit.tailRightBg = LoadSprite("tail_right_right_angle-bg.png");
        kit.tailBottomOt = LoadSprite("tail_bottom_right_angle-ot.png");
        kit.tailTopOt = LoadSprite("tail_top_right_angle-ot.png");
        kit.tailLeftOt = LoadSprite("tail_left_right_angle-ot.png");
        kit.tailRightOt = LoadSprite("tail_right_right_angle-ot.png");
        kit.tailConnectionMask = LoadSprite("tail_connection_mask.png");
        kit.continueIndicator = LoadSprite("continue_indicator.png");
    }

    static Sprite LoadSprite(string fileName)
    {
        string path = Path.Combine(UiKitSprites.PngFolder, fileName).Replace('\\', '/');
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadNotificationIcon(string fileName)
    {
        string path = Path.Combine(UiKitSprites.NotificationIconsFolder, fileName).Replace('\\', '/');
        EnsureSpriteImport(path);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            dirty = true;
        }

        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }

    static TMP_FontAsset FindDogica()
    {
        string[] guids = AssetDatabase.FindAssets("dogica t:TMP_FontAsset");
        for (int i = 0; i < guids.Length; i++)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (font != null)
                return font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    static void AddLabel(RectTransform parent, string text, Vector2 anchoredPos, TMP_FontAsset font)
    {
        var go = new GameObject("Label_" + text, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.fontSize = 18f;
        tmp.color = new Color(1f, 1f, 1f, 0.7f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = text;
        tmp.raycastTarget = false;
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480f, 28f);
        rt.anchoredPosition = anchoredPos;
    }
}
