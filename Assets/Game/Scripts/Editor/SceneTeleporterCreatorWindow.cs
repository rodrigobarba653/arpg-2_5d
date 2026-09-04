using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool for level designers. Builds a complete SceneTeleporter in one
/// click: a scene dropdown (from Build Settings) for the destination, and a
/// second dropdown listing every SpawnPoint that already exists in that
/// destination scene (scanned by opening it additively and closing it again —
/// the current scene is never touched).
///
/// Naming: the created GameObject is named TRANSPORTA A "SCENE NAME" in
/// UPPERCASE so designers can spot every teleporter at a glance in the
/// Hierarchy, regardless of which scene they're pointed at.
///
/// Mirrors DoorCreatorWindow's structure/conventions.
/// </summary>
public class SceneTeleporterCreatorWindow : EditorWindow
{
    // Target scene (from Build Settings)
    string[] sceneDisplayNames = new string[0];
    string[] scenePaths = new string[0];
    int selectedSceneIndex = -1;

    // Spawn points scanned from the target scene
    string[] scannedSpawnIds = new string[0];
    int selectedSpawnIndex = -1;
    string lastScannedScenePath = "";

    // Manual override / new spawn point
    string customSpawnPointId = "";
    string newSpawnPointId = "entry";

    // Trigger
    Vector3 triggerSize = new Vector3(2f, 3f, 2f);
    bool requireInteract = false;
    GameObject interactPromptPrefab;

    // Fade
    CanvasGroup fadeCanvasGroup;
    float fadeDuration = 0.4f;
    float postLoadGrace = 1f;

    Vector2 scroll;

    [MenuItem("Tools/ARPG/Level Design/Scene Teleporter Creator", false, 104)]
    public static void Open()
    {
        var w = GetWindow<SceneTeleporterCreatorWindow>("Scene Teleporter Creator");
        w.minSize = new Vector2(420, 560);
        w.RefreshSceneList();
    }

    void OnEnable() => RefreshSceneList();

    void RefreshSceneList()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        scenePaths = scenes.Select(s => s.path).ToArray();
        sceneDisplayNames = scenePaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();

        if (scenePaths.Length == 0)
            selectedSceneIndex = -1;
        else if (selectedSceneIndex < 0 || selectedSceneIndex >= scenePaths.Length)
            selectedSceneIndex = 0;
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Scene Teleporter Creator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Pick a destination scene and (optionally) an existing " +
                                    "SpawnPoint in it — builds a ready-to-use SceneTeleporter.",
                                    EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        DrawTargetScenePicker();
        EditorGUILayout.Space();

        DrawSpawnPointPicker();
        EditorGUILayout.Space();

        DrawTriggerSettings();
        EditorGUILayout.Space();

        DrawFadeSettings();
        EditorGUILayout.Space(16);

        string err = Validate();
        if (!string.IsNullOrEmpty(err))
            EditorGUILayout.HelpBox(err, MessageType.Warning);

        GUI.enabled = string.IsNullOrEmpty(err);
        if (GUILayout.Button("Create Scene Teleporter", GUILayout.Height(36)))
            CreateTeleporter();
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    // UI SECTIONS
    // ============================================================
    void DrawTargetScenePicker()
    {
        EditorGUILayout.LabelField("Target Scene", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh Scene List (from Build Settings)"))
            RefreshSceneList();

        if (sceneDisplayNames.Length == 0)
        {
            EditorGUILayout.HelpBox("No enabled scenes in File ▸ Build Settings ▸ Scenes In Build.", MessageType.Warning);
            return;
        }

        int newIndex = EditorGUILayout.Popup("Destination Scene", selectedSceneIndex, sceneDisplayNames);
        if (newIndex != selectedSceneIndex)
        {
            selectedSceneIndex = newIndex;
            // Target changed — the scanned spawn point list is now stale.
            scannedSpawnIds = new string[0];
            selectedSpawnIndex = -1;
        }
    }

    void DrawSpawnPointPicker()
    {
        EditorGUILayout.LabelField("Arrival Point In That Scene", EditorStyles.boldLabel);

        if (selectedSceneIndex < 0 || selectedSceneIndex >= scenePaths.Length)
        {
            EditorGUILayout.HelpBox("Pick a destination scene first.", MessageType.Info);
            return;
        }

        string targetPath = scenePaths[selectedSceneIndex];

        if (GUILayout.Button("Scan Spawn Points In Target Scene"))
            ScanSpawnPoints(targetPath);

        if (scannedSpawnIds.Length > 0)
        {
            var options = scannedSpawnIds.Concat(new[] { "(custom id below)" }).ToArray();
            int clamped = Mathf.Clamp(selectedSpawnIndex, 0, options.Length - 1);
            selectedSpawnIndex = EditorGUILayout.Popup("Existing Spawn Point", clamped, options);

            if (selectedSpawnIndex == options.Length - 1)
                customSpawnPointId = EditorGUILayout.TextField("Custom Spawn Point Id", customSpawnPointId);
        }
        else
        {
            EditorGUILayout.LabelField(lastScannedScenePath == targetPath
                ? "No SpawnPoint found in that scene yet."
                : "Not scanned yet — click 'Scan Spawn Points In Target Scene' above.",
                EditorStyles.miniLabel);

            customSpawnPointId = EditorGUILayout.TextField("Spawn Point Id", customSpawnPointId);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Or create a brand-new SpawnPoint directly in the " +
                                    "target scene (placed at world origin — reposition it " +
                                    "after opening that scene):", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        newSpawnPointId = EditorGUILayout.TextField(newSpawnPointId);
        if (GUILayout.Button("Create In Target Scene", GUILayout.Width(160)))
            CreateSpawnPointInTargetScene(targetPath, newSpawnPointId);
        EditorGUILayout.EndHorizontal();
    }

    void DrawTriggerSettings()
    {
        EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel);
        triggerSize = EditorGUILayout.Vector3Field("Trigger Size", triggerSize);
        requireInteract = EditorGUILayout.Toggle("Require Interact", requireInteract);
        if (requireInteract)
        {
            interactPromptPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Interact Prompt Prefab (optional)", interactPromptPrefab, typeof(GameObject), false);
            EditorGUILayout.LabelField("Leave empty to skip the prompt visual — you can " +
                                        "assign one later on the SceneTeleporter component.",
                                        EditorStyles.wordWrappedMiniLabel);
        }
    }

    void DrawFadeSettings()
    {
        EditorGUILayout.LabelField("Fade & Timing", EditorStyles.boldLabel);
        fadeCanvasGroup = (CanvasGroup)EditorGUILayout.ObjectField(
            "Fade Canvas Group (optional)", fadeCanvasGroup, typeof(CanvasGroup), true);
        fadeDuration = EditorGUILayout.FloatField("Fade Duration", fadeDuration);
        postLoadGrace = EditorGUILayout.FloatField("Post-Load Grace (anti re-trigger)", postLoadGrace);
    }

    string Validate()
    {
        if (selectedSceneIndex < 0 || selectedSceneIndex >= scenePaths.Length)
            return "Pick a destination scene.";
        return "";
    }

    // ============================================================
    // SPAWN POINT SCANNING (additive open → scan → close, current scene untouched)
    // ============================================================
    void ScanSpawnPoints(string scenePath)
    {
        var found = new List<string>();
        Scene loaded = default;
        bool opened = false;

        try
        {
            loaded = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            opened = true;

            var spawnPoints = Object.FindObjectsOfType<SpawnPoint>();
            foreach (var sp in spawnPoints)
            {
                if (sp.gameObject.scene != loaded) continue; // safety: only this scene's own
                if (!found.Contains(sp.spawnId)) found.Add(sp.spawnId);
            }
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(loaded, true);
        }

        scannedSpawnIds = found.ToArray();
        lastScannedScenePath = scenePath;
        selectedSpawnIndex = scannedSpawnIds.Length > 0 ? 0 : -1;

        Debug.Log($"[SceneTeleporterCreator] Found {scannedSpawnIds.Length} SpawnPoint(s) in " +
                  $"'{Path.GetFileNameWithoutExtension(scenePath)}'.");
    }

    void CreateSpawnPointInTargetScene(string scenePath, string spawnId)
    {
        if (string.IsNullOrWhiteSpace(spawnId))
        {
            Debug.LogWarning("[SceneTeleporterCreator] Spawn point id can't be empty.");
            return;
        }

        Scene loaded = default;
        bool opened = false;

        try
        {
            loaded = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            opened = true;

            var go = new GameObject($"SpawnPoint_{spawnId}");
            SceneManager.MoveGameObjectToScene(go, loaded);
            var sp = go.AddComponent<SpawnPoint>();
            sp.spawnId = spawnId;

            EditorSceneManager.MarkSceneDirty(loaded);
            EditorSceneManager.SaveScene(loaded);

            Debug.Log($"[SceneTeleporterCreator] Created SpawnPoint '{spawnId}' in " +
                      $"'{Path.GetFileNameWithoutExtension(scenePath)}' at world origin. " +
                      "Open that scene and reposition it where the player should arrive.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(loaded, true);
        }

        // Refresh the dropdown so the new point is immediately selectable.
        ScanSpawnPoints(scenePath);
    }

    // ============================================================
    // BUILDER
    // ============================================================
    void CreateTeleporter()
    {
        string targetPath = scenePaths[selectedSceneIndex];
        string targetSceneName = Path.GetFileNameWithoutExtension(targetPath);

        string spawnId = ResolveSpawnPointId();

        // Uppercase, quoted, human-scannable name — every teleporter reads the
        // same way in the Hierarchy no matter which scene it points to.
        string rootName = $"TRANSPORTA A \"{targetSceneName.ToUpperInvariant()}\"";

        var root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Scene Teleporter");

        var sceneCam = SceneView.lastActiveSceneView;
        if (sceneCam != null && sceneCam.camera != null)
            root.transform.position = sceneCam.pivot;

        var box = Undo.AddComponent<BoxCollider>(root);
        box.isTrigger = true;
        box.size = triggerSize;

        var teleporter = Undo.AddComponent<SceneTeleporter>(root);
        teleporter.targetSceneName = targetSceneName;
        teleporter.spawnPointId = spawnId;
        teleporter.requireInteract = requireInteract;
        teleporter.fadeCanvasGroup = fadeCanvasGroup;
        teleporter.fadeDuration = fadeDuration;
        teleporter.postLoadGrace = postLoadGrace;

        if (requireInteract && interactPromptPrefab != null)
        {
            var prompt = (GameObject)PrefabUtility.InstantiatePrefab(interactPromptPrefab, root.transform);
            Undo.RegisterCreatedObjectUndo(prompt, "Teleporter interact prompt");
            teleporter.interactPrompt = prompt;
        }

        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log($"[SceneTeleporterCreator] Created '{rootName}' → scene " +
                  $"'{targetSceneName}', spawn id '{spawnId}'. Make sure '{targetSceneName}' " +
                  "is added to Build Settings.");
    }

    string ResolveSpawnPointId()
    {
        if (scannedSpawnIds.Length > 0 && selectedSpawnIndex >= 0 && selectedSpawnIndex < scannedSpawnIds.Length)
            return scannedSpawnIds[selectedSpawnIndex];

        return customSpawnPointId ?? "";
    }
}
