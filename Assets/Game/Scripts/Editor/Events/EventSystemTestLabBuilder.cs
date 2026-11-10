using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds an isolated EventSystem_TestLab scene for Sprint 01 QA.
/// Does not modify production gameplay scenes or production prefabs
/// (PlayTestBootstrap is only instantiated if it already exists).
/// </summary>
public static class EventSystemTestLabBuilder
{
    const string ScenePath = "Assets/Game/Scenes/EventSystem_TestLab.unity";
    const string BootstrapPrefabPath = "Assets/Game/Prefabs/PlayTestBootstrap.prefab";
    const string VcamPrefabPath = "Assets/Game/Prefabs/Camera/VCam_Main.prefab";

    // Prompt layer bit (layer 9) — matches BattleRing Main/UI camera split.
    const int PromptLayerBit = 1 << 9;
    const int EverythingExceptPrompt = ~PromptLayerBit;

    [InitializeOnLoadMethod]
    static void AutoPatchRequiresFlagWaitOnLoad()
    {
        EditorApplication.delayCall += () => { PatchRequiresFlagWaitInOpenScenes(); };
    }

    [MenuItem("Tools/ARPG/Events/Set RequiresFlag Wait to 5s", false, 203)]
    public static void PatchRequiresFlagWaitMenu()
    {
        if (PatchRequiresFlagWaitInOpenScenes())
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Event_WS_RequiresFlag Wait → 5s.\nSave the scene if prompted.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "No se encontró Event_WS_RequiresFlag.\n" +
                "Corre Tools/ARPG/Events/Add WorldState Test Fixtures primero.",
                "OK");
        }
    }

    static bool PatchRequiresFlagWaitInOpenScenes()
    {
        var go = GameObject.Find("Event_WS_RequiresFlag");
        if (go == null)
            return false;

        var runner = go.GetComponent<EventRunner>();
        if (runner == null || runner.actions == null)
            return false;

        bool dirty = false;
        for (int i = 0; i < runner.actions.Count; i++)
        {
            if (runner.actions[i] is WaitAction wait && !Mathf.Approximately(wait.seconds, 5f))
            {
                wait.seconds = 5f;
                dirty = true;
            }
        }

        if (!dirty)
            return true;

        EditorUtility.SetDirty(runner);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[EventSystemTestLab] Event_WS_RequiresFlag Wait set to 5s. Save the scene.", runner);
        return true;
    }

    [MenuItem("Tools/ARPG/Events/Create EventSystem TestLab Scene", false, 200)]
    public static void Create()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode before creating the TestLab scene.", "OK");
            return;
        }

        if (File.Exists(ScenePath))
        {
            if (!EditorUtility.DisplayDialog("EventSystem TestLab",
                    $"Scene already exists:\n{ScenePath}\n\nOverwrite?", "Overwrite", "Cancel"))
                return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);

        // Spawn marker
        var spawn = new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0f, 0.1f, 0f);
        var sp = spawn.AddComponent<SpawnPoint>();
        sp.spawnId = "new_game";

        // Optional play-test bootstrap (instance only — prefab asset untouched)
        if (File.Exists(BootstrapPrefabPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "PlayTestBootstrap";
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate PlayTestBootstrap");
            }
        }
        else
        {
            Debug.LogWarning("[EventSystemTestLab] PlayTestBootstrap.prefab not found. " +
                             "Create it via Tools/ARPG/Level Design/Create Play Test Bootstrap Prefab, " +
                             "then drag it into EventSystem_TestLab, or re-run this menu item.");
        }

        CreatePlayerControlEvent();
        CreateEnvironmentalEvent();
        CreateInteractEvent();
        CreateOverlapPlayerControlEvent();
        CreateWorldStateFixtures();
        CreateDoorFixtures();
        CreateInteractionFixtures();
        EnsureInteractionDetector();
        WirePlayerPromptSprites();
        CreateDialogueFixtures();

        var hud = new GameObject("EventSystemDebugHud");
        hud.AddComponent<EventSystemDebugHud>();
        hud.AddComponent<WorldStateDebugCommands>();

        var notes = new GameObject("README_TestLab");
        notes.transform.position = new Vector3(0f, 2f, 0f);

        SetupBattleRingStyleCamera(spawnPosition: new Vector3(0f, 0.1f, 0f));

        Directory.CreateDirectory("Assets/Game/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("EventSystem TestLab",
            $"Created:\n{ScenePath}\n\n" +
            "Camera stack matches BattleRing (Main + UI overlay + VCam_Main).\n" +
            "Open the scene, ensure PlayTestBootstrap is present, press Play.",
            "OK");

        EditorSceneManager.OpenScene(ScenePath);
    }

    /// <summary>
    /// Apply BattleRing-style camera to the existing TestLab scene
    /// (Main Camera + CinemachineBrain + UI Camera overlay + VCam_Main).
    /// Safe to run repeatedly.
    /// </summary>
    [MenuItem("Tools/ARPG/Events/Setup TestLab Camera (BattleRing)", false, 201)]
    public static void SetupCameraMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode before setting up the camera.", "OK");
            return;
        }

        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                $"Scene not found:\n{ScenePath}\n\nCreate it first.", "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var spawn = Object.FindFirstObjectByType<SpawnPoint>();
        Vector3 spawnPos = spawn != null ? spawn.transform.position : new Vector3(0f, 0.1f, 0f);

        SetupBattleRingStyleCamera(spawnPos);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[EventSystemTestLab] BattleRing-style camera applied and scene saved.");
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Camera setup complete (BattleRing style):\n" +
            "- Main Camera + CinemachineBrain\n" +
            "- UI Camera overlay (Prompt layer)\n" +
            "- VCam_Main follow camera",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add WorldState Test Fixtures", false, 202)]
    public static void AddWorldStateFixturesMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateWorldStateFixtures();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added WorldState fixtures (magenta/cyan/purple pads) + F5/F9/F6 debug keys.\n" +
            "Save the scene.",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add Door/Switch Test Fixtures", false, 204)]
    public static void AddDoorFixturesMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateDoorFixtures();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added Door/Switch fixtures along +X (z≈-6).\n" +
            "F5/F9 save-load · F6 clear WorldState · F7 grant Small Key.\n" +
            "Save the scene.",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add Interaction/Discovery Test Fixtures", false, 205)]
    public static void AddInteractionFixturesMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateInteractionFixtures();
        EnsureInteractionDetector();
        WirePlayerPromptSprites();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added Interaction/Discovery fixtures along +X (z≈-14).\n" +
            "Prompt stays above Jyn/Ax head (FFIX). E = interact.\n" +
            "Save the scene.",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add Dialogue Test Fixtures (Phase A)", false, 206)]
    public static void AddDialogueFixturesMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateDialogueFixtures();
        EnsureInteractionDetector();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added Dialogue Phase A fixtures along +X (z≈-20).\n" +
            "Ambient = auto proximity · Interactive = E (South).\n" +
            "Save the scene.",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add Dialogue Line Variant Fixture (Phase B Step 1)", false, 207)]
    public static void AddDialogueLineVariantFixtureMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateLineVariantFixture();
        EnsureInteractionDetector();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added NPC_LineVariants at (0, 1, -28).\n" +
            "One shared dialogue. Non-blocking so Q / Left Trigger can swap mid-talk.\n" +
            "Does not rebuild Phase A fixtures. Save the scene.",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add Dialogue Outcome Fixtures (Phase B Step 2)", false, 208)]
    public static void AddDialogueOutcomeFixturesMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateOutcomeFixtures();
        EnsureInteractionDetector();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added outcome pads along z≈-32.\n" +
            "Green complete, red fail, orange cancel, blue victim, purple interrupt.\n" +
            "Does not rebuild Phase A or the green NPC_LineVariants. Save the scene.",
            "OK");
    }

    [MenuItem("Tools/ARPG/Events/Add Dialogue Choice Fixture (Phase B Step 3)", false, 209)]
    public static void AddDialogueChoiceFixtureMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("EventSystem TestLab",
                "Exit Play Mode first.", "OK");
            return;
        }

        CreateChoiceFixtures();
        EnsureInteractionDetector();
        EnsureDebugCommands();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("EventSystem TestLab",
            "Added choice NPCs along z≈-36.\n" +
            "Gold converge, cyan automatic, red bad target, blue cancel.\n" +
            "E talks. W/S or arrows move the menu. Does not rebuild older fixtures. Save the scene.",
            "OK");
    }

    /// <summary>Batchmode entry: Unity -executeMethod EventSystemTestLabBuilder.SetupCameraBatch</summary>
    public static void SetupCameraBatch()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[EventSystemTestLab] Missing scene: {ScenePath}");
            EditorApplication.Exit(1);
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var spawn = Object.FindFirstObjectByType<SpawnPoint>();
        Vector3 spawnPos = spawn != null ? spawn.transform.position : new Vector3(0f, 0.1f, 0f);
        SetupBattleRingStyleCamera(spawnPos);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EventSystemTestLab] BattleRing-style camera applied (batch).");
        EditorApplication.Exit(0);
    }

    static void SetupBattleRingStyleCamera(Vector3 spawnPosition)
    {
        // Remove prior TestLab camera pieces so re-runs stay clean.
        DestroyNamedRoot("VCam_Main");
        DestroyNamedRoot("Main Camera");

        // --- Main Camera (BattleRing) ---
        var mainGo = new GameObject("Main Camera");
        mainGo.tag = "MainCamera";
        var mainCam = mainGo.AddComponent<Camera>();
        mainCam.clearFlags = CameraClearFlags.Skybox;
        mainCam.fieldOfView = 45f;
        mainCam.nearClipPlane = 0.3f;
        mainCam.farClipPlane = 1000f;
        mainCam.depth = -1;
        mainCam.cullingMask = EverythingExceptPrompt;
        mainGo.AddComponent<AudioListener>();
        mainGo.AddComponent<CinemachineBrain>();

        var mainUrp = mainGo.AddComponent<UniversalAdditionalCameraData>();
        mainUrp.renderType = CameraRenderType.Base;
        mainUrp.renderShadows = true;
        mainUrp.renderPostProcessing = true;
        mainUrp.antialiasing = AntialiasingMode.TemporalAntiAliasing;
        mainUrp.antialiasingQuality = AntialiasingQuality.High;
        mainUrp.stopNaN = true;
        mainUrp.dithering = true;
        mainUrp.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
        mainUrp.requiresColorOption = CameraOverrideOption.UsePipelineSettings;

        // Match VCam follow framing near spawn (FollowOffset 0, 4.5, -9.3).
        Vector3 camPos = spawnPosition + new Vector3(0f, 4.5f, -9.3f);
        Quaternion camRot = Quaternion.Euler(25.821f, 0f, 0f);
        mainGo.transform.SetPositionAndRotation(camPos, camRot);

        // --- UI Camera overlay (Prompt layer only) — child of Main, like BattleRing ---
        var uiGo = new GameObject("UI Camera");
        uiGo.transform.SetParent(mainGo.transform, false);
        uiGo.transform.localPosition = Vector3.zero;
        uiGo.transform.localRotation = Quaternion.identity;

        var uiCam = uiGo.AddComponent<Camera>();
        uiCam.clearFlags = CameraClearFlags.Depth;
        uiCam.fieldOfView = 45f;
        uiCam.nearClipPlane = 0.3f;
        uiCam.farClipPlane = 1000f;
        uiCam.depth = 0;
        uiCam.cullingMask = PromptLayerBit;
        uiCam.allowHDR = true;
        uiCam.allowMSAA = true;
        uiCam.useOcclusionCulling = false;

        var uiUrp = uiGo.AddComponent<UniversalAdditionalCameraData>();
        uiUrp.renderType = CameraRenderType.Overlay;
        uiUrp.renderShadows = false;
        uiUrp.renderPostProcessing = false;
        uiUrp.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
        uiUrp.requiresColorOption = CameraOverrideOption.UsePipelineSettings;

        if (mainUrp.cameraStack != null)
        {
            mainUrp.cameraStack.Clear();
            mainUrp.cameraStack.Add(uiCam);
        }

        // --- VCam_Main prefab instance ---
        var vcamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VcamPrefabPath);
        if (vcamPrefab == null)
        {
            Debug.LogError($"[EventSystemTestLab] Missing VCam prefab at {VcamPrefabPath}");
            return;
        }

        var vcamInstance = (GameObject)PrefabUtility.InstantiatePrefab(vcamPrefab);
        vcamInstance.name = "VCam_Main";
        vcamInstance.transform.SetPositionAndRotation(camPos, camRot);
        Undo.RegisterCreatedObjectUndo(vcamInstance, "Instantiate VCam_Main");
        Undo.RegisterCreatedObjectUndo(mainGo, "Create Main Camera");
    }

    static void DestroyNamedRoot(string objectName)
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == objectName)
                Object.DestroyImmediate(roots[i]);
        }
    }

    static void CreatePlayerControlEvent()
    {
        // Same center as Trigger B so ONE step into the pad fires both
        // OnTriggerEnter calls. Movement lock must NOT be required to test
        // player-control exclusivity (you cannot walk to a second volume
        // after A/B has already seized PlayerInput).
        var go = CreateTriggerVolume("Event_PlayerControl_TriggerA",
            new Vector3(-3f, 1f, 3f), new Vector3(2.5f, 2f, 2.5f),
            new Color(1f, 0.3f, 0.3f, 0.35f));

        var runner = go.AddComponent<EventRunner>();
        runner.seizesPlayerControl = true;
        runner.executionMode = EventExecutionMode.Repeat;
        runner.debugLog = true;
        runner.actions = new System.Collections.Generic.List<EventAction>
        {
            new DebugLogAction { message = "PlayerControl A: start" },
            new WaitAction { seconds = 1.5f },
            new DebugLogAction { message = "PlayerControl A: mid" },
            new WaitAction { seconds = 1.5f },
            new DebugLogAction { message = "PlayerControl A: end" },
        };

        var source = go.AddComponent<EventSourceTrigger>();
        source.runner = runner;
        source.mode = EventSourceTrigger.TriggerMode.OnEnter;
    }

    static void CreateEnvironmentalEvent()
    {
        var go = CreateTriggerVolume("Event_Environmental_TriggerC",
            new Vector3(3f, 1f, 3f), new Color(0.3f, 1f, 0.4f, 0.35f));

        var runner = go.AddComponent<EventRunner>();
        runner.seizesPlayerControl = false;
        runner.executionMode = EventExecutionMode.Repeat;
        runner.debugLog = true;
        runner.actions = new System.Collections.Generic.List<EventAction>
        {
            new DebugLogAction { message = "Env C: start (player should still move)" },
            new WaitAction { seconds = 2f },
            new DebugLogAction { message = "Env C: end" },
        };

        var source = go.AddComponent<EventSourceTrigger>();
        source.runner = runner;
    }

    static void CreateInteractEvent()
    {
        var go = CreateTriggerVolume("Event_Interact",
            new Vector3(0f, 1f, 5f), new Color(0.3f, 0.5f, 1f, 0.35f));

        var runner = go.AddComponent<EventRunner>();
        runner.seizesPlayerControl = true;
        runner.executionMode = EventExecutionMode.Repeat;
        runner.debugLog = true;
        runner.actions = new System.Collections.Generic.List<EventAction>
        {
            new DebugLogAction { message = "Interact event: start" },
            new WaitAction { seconds = 2f },
            new DebugLogAction { message = "Interact event: end" },
        };

        var source = go.AddComponent<EventSourceInteract>();
        source.runner = runner;
    }

    static void CreateOverlapPlayerControlEvent()
    {
        // Co-located with Trigger A — conflict pad for exclusivity QA.
        var go = CreateTriggerVolume("Event_PlayerControl_TriggerB_Overlap",
            new Vector3(-3f, 1f, 3f), new Vector3(2.5f, 2f, 2.5f),
            new Color(1f, 0.7f, 0.2f, 0.25f));

        var runner = go.AddComponent<EventRunner>();
        runner.seizesPlayerControl = true;
        runner.executionMode = EventExecutionMode.Repeat;
        runner.debugLog = true;
        runner.actions = new System.Collections.Generic.List<EventAction>
        {
            new DebugLogAction { message = "PlayerControl B: only if no other player-control event" },
            new WaitAction { seconds = 1f },
        };

        var source = go.AddComponent<EventSourceTrigger>();
        source.runner = runner;
    }

    /// <summary>
    /// Sprint 02 pads:
    /// Magenta = sets flag test_gate=true (no player lock).
    /// Cyan = requires test_gate true, then clears it (player lock briefly).
    /// Purple = Persistent Once sample (eventId test_persist_once).
    /// Yellow = AND conditions sample (gate true + persist not completed) — optional gated.
    /// </summary>
    static void CreateWorldStateFixtures()
    {
        DestroyNamedRoot("Event_WS_SetFlag_Gate");
        DestroyNamedRoot("Event_WS_RequiresFlag");
        DestroyNamedRoot("Event_WS_PersistOnce");
        DestroyNamedRoot("Event_WS_SessionOnce");
        DestroyNamedRoot("Event_WS_AndConditions");
        DestroyNamedRoot("Event_WS_DuplicateId_A");
        DestroyNamedRoot("Event_WS_DuplicateId_B");

        // 1) Set flag (env — can walk)
        {
            var go = CreateTriggerVolume("Event_WS_SetFlag_Gate",
                new Vector3(-6f, 1f, -3f), new Color(1f, 0.2f, 1f, 0.35f));
            var runner = go.AddComponent<EventRunner>();
            runner.eventId = "ws_set_gate";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "WS: setting test_gate = true" },
                new SetFlagAction { flagId = "test_gate", value = true },
                new WaitAction { seconds = 0.5f },
            };
            go.AddComponent<EventSourceTrigger>().runner = runner;
        }

        // 2) Requires flag (player control) — unavailable until magenta run
        {
            var go = CreateTriggerVolume("Event_WS_RequiresFlag",
                new Vector3(-3f, 1f, -3f), new Color(0.2f, 1f, 1f, 0.35f));
            var runner = go.AddComponent<EventRunner>();
            runner.eventId = "ws_requires_gate";
            runner.seizesPlayerControl = true;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.conditions = new System.Collections.Generic.List<EventCondition>
            {
                new FlagIsTrueCondition { flagId = "test_gate" },
            };
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "WS: gate was true — WAIT 5s then clear (disable GO now to test Cancel)" },
                new WaitAction { seconds = 5f },
                new ClearFlagAction { flagId = "test_gate" },
                new DebugLogAction { message = "WS: test_gate cleared" },
            };
            go.AddComponent<EventSourceTrigger>().runner = runner;
        }

        // 3) Persistent Once
        {
            var go = CreateTriggerVolume("Event_WS_PersistOnce",
                new Vector3(0f, 1f, -3f), new Color(0.6f, 0.3f, 1f, 0.35f));
            var runner = go.AddComponent<EventRunner>();
            runner.eventId = "test_persist_once";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Once;
            runner.persistOnce = true;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "WS: Persistent Once fired — save with F5, reload F9" },
                new WaitAction { seconds = 0.5f },
            };
            go.AddComponent<EventSourceTrigger>().runner = runner;
        }

        // 4) Session Once (not persist) — re-fires after F6 clear + domain, or new Play
        {
            var go = CreateTriggerVolume("Event_WS_SessionOnce",
                new Vector3(3f, 1f, -3f), new Color(1f, 0.55f, 0.1f, 0.35f));
            var runner = go.AddComponent<EventRunner>();
            runner.eventId = "test_session_once";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Once;
            runner.persistOnce = false;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "WS: Session Once fired (persistOnce=false)" },
            };
            go.AddComponent<EventSourceTrigger>().runner = runner;
        }

        // 5) AND conditions (gate true AND persist once not completed)
        {
            var go = CreateTriggerVolume("Event_WS_AndConditions",
                new Vector3(5.5f, 1f, -3f), new Color(1f, 1f, 0.2f, 0.35f));
            var runner = go.AddComponent<EventRunner>();
            runner.eventId = "ws_and_conditions";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.conditions = new System.Collections.Generic.List<EventCondition>
            {
                new FlagIsTrueCondition { flagId = "test_gate" },
                new EventIsNotCompletedCondition { eventId = "test_persist_once" },
            };
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "WS: AND conditions passed" },
            };
            go.AddComponent<EventSourceTrigger>().runner = runner;
        }

        // 6) Duplicate id warning pair (disabled by default — enable both in Hierarchy for QA)
        {
            var a = CreateTriggerVolume("Event_WS_DuplicateId_A",
                new Vector3(8f, 1f, -3f), new Vector3(1.2f, 2f, 1.2f),
                new Color(0.5f, 0.5f, 0.5f, 0.25f));
            var ra = a.AddComponent<EventRunner>();
            ra.eventId = "duplicate_test_id";
            ra.seizesPlayerControl = false;
            ra.executionMode = EventExecutionMode.Once;
            ra.persistOnce = true;
            ra.debugLog = true;
            a.SetActive(false);

            var b = CreateTriggerVolume("Event_WS_DuplicateId_B",
                new Vector3(9.5f, 1f, -3f), new Vector3(1.2f, 2f, 1.2f),
                new Color(0.4f, 0.4f, 0.4f, 0.25f));
            var rb = b.AddComponent<EventRunner>();
            rb.eventId = "duplicate_test_id";
            rb.seizesPlayerControl = false;
            rb.executionMode = EventExecutionMode.Once;
            rb.persistOnce = true;
            rb.debugLog = true;
            b.SetActive(false);
        }
    }

    /// <summary>
    /// Sprint 03 door/switch pads along z=-6.
    /// </summary>
    static void CreateDoorFixtures()
    {
        DestroyNamedRoot("Door_Event_Single");
        DestroyNamedRoot("Switch_Event_Single");
        DestroyNamedRoot("Door_Event_A");
        DestroyNamedRoot("Door_Event_B");
        DestroyNamedRoot("Switch_Event_TwoDoors");
        DestroyNamedRoot("Door_Toggle");
        DestroyNamedRoot("Switch_Toggle");
        DestroyNamedRoot("Event_Switch_ToggleOn");
        DestroyNamedRoot("Event_Switch_ToggleOff");
        DestroyNamedRoot("Door_KeyLocked");
        DestroyNamedRoot("Door_Persistent");
        DestroyNamedRoot("Switch_Persistent");
        DestroyNamedRoot("Door_Pressure");
        DestroyNamedRoot("PressurePlate_Lab");
        DestroyNamedRoot("Door_Legacy_NonEvent");
        DestroyNamedRoot("Door_Cancel");
        DestroyNamedRoot("Event_Cancel_DoorOpen");

        // 1) Switch → one door via EventRunner
        {
            var door = CreateLabDoor("Door_Event_Single", new Vector3(2f, 1f, -6f),
                new Color(0.55f, 0.35f, 0.2f, 1f));
            door.openMode = SimpleDoor.OpenMode.Switch;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;
            door.persistState = false;

            var runnerGo = CreateTriggerVolume("Switch_Event_Single",
                new Vector3(2f, 0.5f, -8f), new Vector3(1.5f, 1f, 1.5f),
                new Color(0.9f, 0.6f, 0.1f, 0.4f));

            var runner = runnerGo.AddComponent<EventRunner>();
            runner.eventId = "lab_switch_one_door";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "Lab: opening single door" },
                new OpenDoorAction { door = door, waitForCompletion = true },
            };

            var sw = runnerGo.AddComponent<DoorSwitch>();
            sw.eventRunner = runner;
            sw.oneShot = false;
            sw.doors = null;
        }

        // 2) Switch → two doors sequential
        {
            var doorA = CreateLabDoor("Door_Event_A", new Vector3(5f, 1f, -6f),
                new Color(0.5f, 0.25f, 0.15f, 1f));
            doorA.openMode = SimpleDoor.OpenMode.Switch;
            doorA.useRotate = false;
            doorA.useSlideUp = true;
            doorA.openHeight = 2.2f;

            var doorB = CreateLabDoor("Door_Event_B", new Vector3(7f, 1f, -6f),
                new Color(0.45f, 0.2f, 0.1f, 1f));
            doorB.openMode = SimpleDoor.OpenMode.Switch;
            doorB.useRotate = false;
            doorB.useSlideUp = true;
            doorB.openHeight = 2.2f;

            var runnerGo = CreateTriggerVolume("Switch_Event_TwoDoors",
                new Vector3(6f, 0.5f, -8f), new Vector3(1.5f, 1f, 1.5f),
                new Color(0.95f, 0.5f, 0.1f, 0.4f));

            var runner = runnerGo.AddComponent<EventRunner>();
            runner.eventId = "lab_switch_two_doors";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "Lab: open door A" },
                new OpenDoorAction { door = doorA, waitForCompletion = true },
                new DebugLogAction { message = "Lab: open door B" },
                new OpenDoorAction { door = doorB, waitForCompletion = true },
            };

            var sw = runnerGo.AddComponent<DoorSwitch>();
            sw.eventRunner = runner;
        }

        // 3) Toggle switch open/close
        {
            var door = CreateLabDoor("Door_Toggle", new Vector3(10f, 1f, -6f),
                new Color(0.3f, 0.5f, 0.7f, 1f));
            door.openMode = SimpleDoor.OpenMode.Switch;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;

            var onGo = new GameObject("Event_Switch_ToggleOn");
            var onRunner = onGo.AddComponent<EventRunner>();
            onRunner.eventId = "lab_toggle_on";
            onRunner.seizesPlayerControl = false;
            onRunner.executionMode = EventExecutionMode.Repeat;
            onRunner.debugLog = true;
            onRunner.actions = new System.Collections.Generic.List<EventAction>
            {
                new OpenDoorAction { door = door, waitForCompletion = true },
            };

            var offGo = new GameObject("Event_Switch_ToggleOff");
            var offRunner = offGo.AddComponent<EventRunner>();
            offRunner.eventId = "lab_toggle_off";
            offRunner.seizesPlayerControl = false;
            offRunner.executionMode = EventExecutionMode.Repeat;
            offRunner.debugLog = true;
            offRunner.actions = new System.Collections.Generic.List<EventAction>
            {
                new CloseDoorAction { door = door, waitForCompletion = true },
            };

            var swGo = CreateTriggerVolume("Switch_Toggle",
                new Vector3(10f, 0.5f, -8f), new Vector3(1.5f, 1f, 1.5f),
                new Color(0.2f, 0.7f, 0.9f, 0.4f));
            var sw = swGo.AddComponent<DoorSwitch>();
            sw.toggleMode = true;
            sw.eventRunner = onRunner;
            sw.deactivateEventRunner = offRunner;
        }

        // 4) Key-locked door (F7 grants Small Key)
        {
            var door = CreateLabDoor("Door_KeyLocked", new Vector3(13f, 1f, -6f),
                new Color(0.7f, 0.15f, 0.15f, 1f));
            door.openMode = SimpleDoor.OpenMode.Key;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;
            door.requiredKeyId = "smallKey00";
            door.consumeKeyOnUse = true;
            door.persistState = true;
            door.persistentId = "lab_key_door";

            // Trigger volume on door for Key mode (SimpleDoor listens OnTriggerEnter on same GO)
            var trigger = door.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.2f, 1.2f, 1.2f);
            trigger.center = new Vector3(0f, 0f, -1f);
        }

        // 5+6) Persistent door + switch
        {
            var door = CreateLabDoor("Door_Persistent", new Vector3(16f, 1f, -6f),
                new Color(0.2f, 0.7f, 0.3f, 1f));
            door.openMode = SimpleDoor.OpenMode.Switch;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;
            door.persistState = true;
            door.persistentId = "lab_persist_door";

            var runnerGo = CreateTriggerVolume("Switch_Persistent",
                new Vector3(16f, 0.5f, -8f), new Vector3(1.5f, 1f, 1.5f),
                new Color(0.3f, 0.9f, 0.4f, 0.4f));

            var runner = runnerGo.AddComponent<EventRunner>();
            runner.eventId = "lab_persist_switch_event";
            runner.seizesPlayerControl = false;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new OpenDoorAction { door = door, waitForCompletion = true },
            };

            var sw = runnerGo.AddComponent<DoorSwitch>();
            sw.eventRunner = runner;
            sw.oneShot = true;
            sw.persistState = true;
            sw.persistentId = "lab_persist_switch";
        }

        // 10) Pressure plate
        {
            var door = CreateLabDoor("Door_Pressure", new Vector3(19f, 1f, -6f),
                new Color(0.6f, 0.6f, 0.2f, 1f));
            door.openMode = SimpleDoor.OpenMode.Switch;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;

            var plate = CreateTriggerVolume("PressurePlate_Lab",
                new Vector3(19f, 0.15f, -8f), new Vector3(2f, 0.3f, 2f),
                new Color(0.8f, 0.8f, 0.2f, 0.5f));
            var pp = plate.AddComponent<PressurePlateDoor>();
            pp.door = door;
        }

        // 11) Legacy non-event door (Auto) — still works without EventRunner
        {
            var door = CreateLabDoor("Door_Legacy_NonEvent", new Vector3(-2f, 1f, -6f),
                new Color(0.4f, 0.4f, 0.45f, 1f));
            door.openMode = SimpleDoor.OpenMode.Auto;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;
            var trigger = door.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.5f, 1.5f, 1.5f);
            trigger.center = new Vector3(0f, 0f, -1.2f);
        }

        // 8) Cancel mid-open (seizes control — Esc/cancel while door is still moving)
        {
            var door = CreateLabDoor("Door_Cancel", new Vector3(22f, 1f, -6f),
                new Color(0.75f, 0.35f, 0.55f, 1f));
            door.openMode = SimpleDoor.OpenMode.Switch;
            door.useRotate = false;
            door.useSlideUp = true;
            door.openHeight = 2.2f;
            door.moveSpeed = 1.2f; // slow so cancel is easy mid-motion

            var go = CreateTriggerVolume("Event_Cancel_DoorOpen",
                new Vector3(22f, 0.5f, -8f), new Vector3(1.5f, 1f, 1.5f),
                new Color(0.9f, 0.3f, 0.6f, 0.4f));
            var runner = go.AddComponent<EventRunner>();
            runner.eventId = "lab_cancel_door";
            runner.seizesPlayerControl = true;
            runner.executionMode = EventExecutionMode.Repeat;
            runner.debugLog = true;
            runner.actions = new System.Collections.Generic.List<EventAction>
            {
                new DebugLogAction { message = "Lab: cancel-door — open then wait" },
                new OpenDoorAction { door = door, waitForCompletion = true },
                new WaitAction { seconds = 5f },
            };
            var src = go.AddComponent<EventSourceInteract>();
            src.runner = runner;
            src.requirePlayerTag = true;
        }
    }

    static SimpleDoor CreateLabDoor(string name, Vector3 position, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.5f, 2f, 0.3f);

        var renderer = go.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader != null && renderer != null)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }

        // Keep MeshCollider as solid door body (not trigger).
        var door = go.AddComponent<SimpleDoor>();
        door.moveSpeed = 4f;
        return door;
    }

    static void EnsureDebugCommands()
    {
        var hud = GameObject.Find("EventSystemDebugHud");
        if (hud == null)
        {
            hud = new GameObject("EventSystemDebugHud");
            hud.AddComponent<EventSystemDebugHud>();
        }

        if (hud.GetComponent<WorldStateDebugCommands>() == null)
            hud.AddComponent<WorldStateDebugCommands>();
    }

    static GameObject CreateTriggerVolume(string name, Vector3 position, Color color)
    {
        return CreateTriggerVolume(name, position, new Vector3(2.2f, 2f, 2.2f), color);
    }

    static GameObject CreateTriggerVolume(string name, Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;

        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;

        var renderer = go.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader != null && renderer != null)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }

        return go;
    }

    static void EnsureInteractionDetector()
    {
        var existing = Object.FindFirstObjectByType<InteractionDetector>();
        if (existing != null)
            return;

        var go = new GameObject("InteractionDetector");
        go.AddComponent<InteractionDetector>();
        Undo.RegisterCreatedObjectUndo(go, "Create InteractionDetector");
    }

    static void WirePlayerPromptSprites()
    {
        // ? / ! are driven by QuestionMarkController animation states
        // (QuestionMark_* and ExclamationMark_*). No per-prompt sprite wiring needed.
    }

    static void CreateInteractionFixtures()
    {
        DestroyNamedRoot("Interact_Discover");
        DestroyNamedRoot("Interact_OnlyBang");
        DestroyNamedRoot("Interact_Hidden");
        DestroyNamedRoot("Interact_Overlap_Bang");
        DestroyNamedRoot("Interact_Overlap_Hidden");
        DestroyNamedRoot("Interact_Unavailable");
        DestroyNamedRoot("Interact_Once");
        DestroyNamedRoot("Interact_NeedsFlag");
        DestroyNamedRoot("Interact_FlagSetter");

        // 1–2) Discovery + Interaction: ? then !
        CreateWorldInteractPad(
            "Interact_Discover",
            new Vector3(0f, 0.5f, -14f),
            new Color(0.3f, 0.6f, 1f, 0.45f),
            InteractPromptMode.DiscoveryAndInteraction,
            discoveryRadius: 5f,
            interactionRadius: 1.1f,
            eventId: "lab_interact_discover",
            logMessage: "Lab: Discover pad interacted");

        // 3) Interaction Only — no ?
        CreateWorldInteractPad(
            "Interact_OnlyBang",
            new Vector3(4f, 0.5f, -14f),
            new Color(1f, 0.85f, 0.2f, 0.45f),
            InteractPromptMode.InteractionOnly,
            discoveryRadius: 3f,
            interactionRadius: 1.1f,
            eventId: "lab_interact_bang_only",
            logMessage: "Lab: Bang-only interacted");

        // 4) Hidden — no prompt, still interactable
        CreateWorldInteractPad(
            "Interact_Hidden",
            new Vector3(8f, 0.5f, -14f),
            new Color(0.5f, 0.5f, 0.55f, 0.35f),
            InteractPromptMode.Hidden,
            discoveryRadius: 3f,
            interactionRadius: 1.1f,
            eventId: "lab_interact_hidden",
            logMessage: "Lab: Hidden pad interacted (no prompt expected)");

        // 5–6) Overlap: visible ! vs nearer Hidden — press must go to Bang
        CreateWorldInteractPad(
            "Interact_Overlap_Bang",
            new Vector3(12f, 0.5f, -14f),
            new Color(0.2f, 0.9f, 0.4f, 0.45f),
            InteractPromptMode.InteractionOnly,
            discoveryRadius: 3f,
            interactionRadius: 1.35f,
            eventId: "lab_overlap_bang",
            logMessage: "Lab: OVERLAP BANG — should receive press when both in range");

        CreateWorldInteractPad(
            "Interact_Overlap_Hidden",
            new Vector3(12.35f, 0.5f, -14f),
            new Color(0.35f, 0.35f, 0.4f, 0.3f),
            InteractPromptMode.Hidden,
            discoveryRadius: 3f,
            interactionRadius: 1.35f,
            eventId: "lab_overlap_hidden",
            logMessage: "Lab: OVERLAP HIDDEN — must NOT steal press while Bang shows !");

        // Unavailable (manual toggle available=false on component for QA #6)
        {
            var wi = CreateWorldInteractPad(
                "Interact_Unavailable",
                new Vector3(16f, 0.5f, -14f),
                new Color(0.7f, 0.2f, 0.2f, 0.4f),
                InteractPromptMode.DiscoveryAndInteraction,
                discoveryRadius: 3.5f,
                interactionRadius: 1.1f,
                eventId: "lab_interact_unavailable",
                logMessage: "Lab: Unavailable — should not fire");
            wi.available = false;
            wi.discoverableWhenUnavailable = false;
        }

        // Once-completed becomes unavailable
        {
            var wi = CreateWorldInteractPad(
                "Interact_Once",
                new Vector3(20f, 0.5f, -14f),
                new Color(0.8f, 0.4f, 0.9f, 0.45f),
                InteractPromptMode.InteractionOnly,
                discoveryRadius: 3f,
                interactionRadius: 1.1f,
                eventId: "lab_interact_once",
                logMessage: "Lab: Once interactable — second press should do nothing");
            wi.eventRunner.executionMode = EventExecutionMode.Once;
            wi.eventRunner.persistOnce = false;
        }

        // Needs flag — discoverable when unavailable
        {
            var setter = CreateWorldInteractPad(
                "Interact_FlagSetter",
                new Vector3(24f, 0.5f, -14f),
                new Color(0.9f, 0.6f, 0.2f, 0.45f),
                InteractPromptMode.InteractionOnly,
                discoveryRadius: 3f,
                interactionRadius: 1.1f,
                eventId: "lab_interact_flag_setter",
                logMessage: "Lab: set flag lab_interact_unlock");
            setter.eventRunner.actions = new System.Collections.Generic.List<EventAction>
            {
                new SetFlagAction { flagId = "lab_interact_unlock", value = true },
                new DebugLogAction { message = "Lab: flag lab_interact_unlock = true" },
            };

            var gated = CreateWorldInteractPad(
                "Interact_NeedsFlag",
                new Vector3(26.5f, 0.5f, -14f),
                new Color(0.4f, 0.8f, 0.9f, 0.45f),
                InteractPromptMode.DiscoveryAndInteraction,
                discoveryRadius: 3.5f,
                interactionRadius: 1.1f,
                eventId: "lab_interact_needs_flag",
                logMessage: "Lab: NeedsFlag unlocked interact");
            gated.discoverableWhenUnavailable = true;
            gated.conditions = new System.Collections.Generic.List<EventCondition>
            {
                new FlagIsTrueCondition { flagId = "lab_interact_unlock" },
            };
        }
    }

    static WorldInteractable CreateWorldInteractPad(
        string name,
        Vector3 position,
        Color color,
        InteractPromptMode mode,
        float discoveryRadius,
        float interactionRadius,
        string eventId,
        string logMessage)
    {
        // Visual marker only — NO object-owned prompt. Detection is distance-based.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);

        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.DestroyImmediate(col);

        var renderer = go.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader != null && renderer != null)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }

        var runner = go.AddComponent<EventRunner>();
        runner.eventId = eventId;
        runner.seizesPlayerControl = false;
        runner.executionMode = EventExecutionMode.Repeat;
        runner.debugLog = true;
        runner.actions = new System.Collections.Generic.List<EventAction>
        {
            new DebugLogAction { message = logMessage },
        };

        var wi = go.AddComponent<WorldInteractable>();
        wi.promptMode = mode;
        wi.discoveryRadius = discoveryRadius;
        wi.interactionRadius = interactionRadius;
        wi.useCentralDetector = true;
        wi.eventRunner = runner;

        return wi;
    }

    const string DialogueAssetFolder = "Assets/Game/Dialogue/TestLab";

    static void CreateDialogueFixtures()
    {
        EnsureDialogueAssetFolder();

        DestroyNamedRoot("NPC_Ambient_A");
        DestroyNamedRoot("NPC_Ambient_B");
        DestroyNamedRoot("NPC_Interactive");
        DestroyNamedRoot("NPC_Interactive_NoPortrait");
        DestroyNamedRoot("NPC_Both");
        DestroyNamedRoot("NPC_HiddenPrompt");
        DestroyNamedRoot("NPC_ActivePlayer_Variants");
        DestroyNamedRoot("Trigger_Narrative_Auto_NonBlocking");
        DestroyNamedRoot("Trigger_Narrative_Manual_Blocking");
        DestroyNamedRoot("Trigger_Narrative_OncePersistent");

        var roster = EnsureTestRoster();
        var ambientA = EnsureDialogueAsset("Lab_Ambient_A", "lab_ambient_a", lockGameplay: false,
            ("a1", "villager_a", "Hmm… the wind feels weird today.", "a2"),
            ("a2", "villager_a", "…or maybe it's just me.", ""));
        var ambientB = EnsureDialogueAsset("Lab_Ambient_B", "lab_ambient_b", lockGameplay: false,
            ("b1", "villager_b", "Did you hear that?", ""));
        // Shared dialogue: ActivePlayer speaker resolves name/portrait to Party.Active.
        var interactive = EnsureDialogueAsset("Lab_Interactive", "lab_interactive", lockGameplay: true,
            ("i1", "active_player", "You there. Got a second?", "i2"),
            ("i2", "villager_a", "For you? Always.", "i3"),
            ("i3", "active_player", "Just checking the dialogue system.", ""));
        MarkEnd(interactive, "i_end", "i3");
        var interactiveNoPort = EnsureDialogueAsset("Lab_Interactive_NoPortrait", "lab_interactive_np", lockGameplay: true,
            ("n1", "stranger", "I have no face in the roster.", "n2"),
            ("n2", "stranger", "Layout should still work.", ""));
        var bothAmbient = EnsureDialogueAsset("Lab_Both_Ambient", "lab_both_ambient", lockGameplay: false,
            ("ba1", "villager_b", "(mutters) Curious traveler…", ""));
        var bothInteractive = EnsureDialogueAsset("Lab_Both_Interactive", "lab_both_interactive", lockGameplay: true,
            ("bi1", "villager_b", "Oh! You want to talk for real?", "bi2"),
            ("bi2", "villager_b", "Interactive takes priority over my ambient mutter.", ""));

        var sharedFallback = EnsureDialogueAsset("Lab_Active_Fallback", "lab_active_fallback", lockGameplay: true,
            ("f1", "villager_a", "Fallback shared dialogue (no override matched).", "f2"),
            ("f2", "active_player", "I hear you.", ""));
        MarkEnd(sharedFallback, "f_end", "f2");
        var jynOnly = EnsureDialogueAsset("Lab_Active_Jyn", "lab_active_jyn", lockGameplay: true,
            ("j1", "villager_a", "Jyn! Good to see you.", "j2"),
            ("j2", "active_player", "Likewise — Jyn path.", ""));
        MarkEnd(jynOnly, "j_end", "j2");
        var axOnly = EnsureDialogueAsset("Lab_Active_Ax", "lab_active_ax", lockGameplay: true,
            ("x1", "villager_a", "Ax! Didn't expect you.", "x2"),
            ("x2", "active_player", "Ax path checking in.", ""));
        MarkEnd(axOnly, "x_end", "x2");

        // Wire end nodes where next is empty — GetEntry walks lines; empty next ends session.
        CreateNpcCapsule("NPC_Ambient_A", new Vector3(0f, 1f, -20f), new Color(0.3f, 0.7f, 1f),
            NpcDialogueMode.AmbientOnly, ambientA, null, InteractPromptMode.Hidden);
        CreateNpcCapsule("NPC_Ambient_B", new Vector3(2.5f, 1f, -20f), new Color(0.25f, 0.6f, 0.95f),
            NpcDialogueMode.AmbientOnly, ambientB, null, InteractPromptMode.Hidden);

        CreateNpcCapsule("NPC_Interactive", new Vector3(6f, 1f, -20f), new Color(0.95f, 0.55f, 0.2f),
            NpcDialogueMode.InteractiveOnly, null, interactive, InteractPromptMode.DiscoveryAndInteraction);

        CreateNpcCapsule("NPC_Interactive_NoPortrait", new Vector3(10f, 1f, -20f), new Color(0.7f, 0.7f, 0.3f),
            NpcDialogueMode.InteractiveOnly, null, interactiveNoPort, InteractPromptMode.InteractionOnly);

        CreateNpcCapsule("NPC_Both", new Vector3(14f, 1f, -20f), new Color(0.6f, 0.3f, 0.85f),
            NpcDialogueMode.Both, bothAmbient, bothInteractive, InteractPromptMode.InteractionOnly);

        CreateNpcCapsule("NPC_HiddenPrompt", new Vector3(18f, 1f, -20f), new Color(0.45f, 0.45f, 0.5f),
            NpcDialogueMode.InteractiveOnly, null, interactive, InteractPromptMode.Hidden);

        var variants = CreateNpcCapsule("NPC_ActivePlayer_Variants", new Vector3(22f, 1f, -20f),
            new Color(0.9f, 0.35f, 0.55f),
            NpcDialogueMode.InteractiveOnly, null, sharedFallback, InteractPromptMode.DiscoveryAndInteraction);
        variants.interactiveByCharacter = new System.Collections.Generic.List<DialogueCharacterAssetOverride>
        {
            new DialogueCharacterAssetOverride { characterId = "jyn", asset = jynOnly },
            new DialogueCharacterAssetOverride { characterId = "ax", asset = axOnly },
        };

        var narrativeAuto = EnsureDialogueAsset("Lab_Narrative_Auto", "lab_narrative_auto", lockGameplay: false,
            ("na1", "active_player", "Something about this place feels wrong…", "na2"),
            ("na2", "active_player", "(I should keep moving.)", ""));
        MarkEnd(narrativeAuto, "na_end", "na2");
        var narrativeManual = EnsureDialogueAsset("Lab_Narrative_Manual", "lab_narrative_manual", lockGameplay: true,
            ("nm1", "active_player", "Hold up — did you hear that?", "nm2"),
            ("nm2", "active_player", "…Okay. Confirm to continue.", ""));
        MarkEnd(narrativeManual, "nm_end", "nm2");
        var narrativeOnce = EnsureDialogueAsset("Lab_Narrative_Once", "lab_narrative_once", lockGameplay: false,
            ("no1", "active_player", "I'll only say this once (persistent flag).", ""));
        MarkEnd(narrativeOnce, "no_end", "no1");

        // z ≈ -24: triggered narrative volumes (walk through, no Interact).
        CreateNarrativeTrigger(
            "Trigger_Narrative_Auto_NonBlocking",
            new Vector3(0f, 1f, -24f),
            narrativeAuto,
            NarrativeDialogueTrigger.FireMode.Repeat,
            NarrativeDialogueTrigger.LockOverride.ForceNonBlocking,
            DialogueProgressionMode.Automatic,
            NarrativeDialogueTrigger.ConflictPolicy.Queue);

        CreateNarrativeTrigger(
            "Trigger_Narrative_Manual_Blocking",
            new Vector3(6f, 1f, -24f),
            narrativeManual,
            NarrativeDialogueTrigger.FireMode.Repeat,
            NarrativeDialogueTrigger.LockOverride.ForceBlocking,
            DialogueProgressionMode.Manual,
            NarrativeDialogueTrigger.ConflictPolicy.Interrupt);

        CreateNarrativeTrigger(
            "Trigger_Narrative_OncePersistent",
            new Vector3(12f, 1f, -24f),
            narrativeOnce,
            NarrativeDialogueTrigger.FireMode.OncePersistent,
            NarrativeDialogueTrigger.LockOverride.ForceNonBlocking,
            DialogueProgressionMode.Automatic,
            NarrativeDialogueTrigger.ConflictPolicy.Queue);

        // Ensure a DialogueService exists in-scene for designers (runtime also bootstraps).
        if (Object.FindFirstObjectByType<DialogueService>() == null)
        {
            var svc = new GameObject("DialogueService");
            var ds = svc.AddComponent<DialogueService>();
            ds.roster = roster;
            ds.debugLog = true;
            Undo.RegisterCreatedObjectUndo(svc, "Create DialogueService");
        }
        else
        {
            var ds = Object.FindFirstObjectByType<DialogueService>();
            if (ds.roster == null)
                ds.roster = roster;
        }
    }

    static void EnsureDialogueAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Dialogue"))
            AssetDatabase.CreateFolder("Assets/Game", "Dialogue");
        if (!AssetDatabase.IsValidFolder(DialogueAssetFolder))
            AssetDatabase.CreateFolder("Assets/Game/Dialogue", "TestLab");
    }

    static CharacterPortraitRoster EnsureTestRoster()
    {
        const string path = DialogueAssetFolder + "/Lab_PortraitRoster.asset";
        var roster = AssetDatabase.LoadAssetAtPath<CharacterPortraitRoster>(path);
        if (roster == null)
        {
            roster = ScriptableObject.CreateInstance<CharacterPortraitRoster>();
            AssetDatabase.CreateAsset(roster, path);
        }

        roster.characters = new System.Collections.Generic.List<CharacterPortraitSet>
        {
            new CharacterPortraitSet
            {
                characterId = "jyn",
                displayName = "Jyn",
                expressions = new System.Collections.Generic.List<PortraitExpression>
                {
                    new PortraitExpression { expressionId = "Neutral", sprite = LoadPlaceholderPortrait("Jyn_Placeholder") },
                },
            },
            new CharacterPortraitSet
            {
                characterId = "ax",
                displayName = "Ax",
                expressions = new System.Collections.Generic.List<PortraitExpression>
                {
                    new PortraitExpression { expressionId = "Neutral", sprite = LoadPlaceholderPortrait("Ax_Placeholder") },
                },
            },
            new CharacterPortraitSet
            {
                characterId = "villager_a",
                displayName = "Villager A",
                expressions = new System.Collections.Generic.List<PortraitExpression>
                {
                    new PortraitExpression { expressionId = "Neutral", sprite = null },
                },
            },
            new CharacterPortraitSet
            {
                characterId = "villager_b",
                displayName = "Villager B",
                expressions = new System.Collections.Generic.List<PortraitExpression>
                {
                    new PortraitExpression { expressionId = "Neutral", sprite = null },
                },
            },
        };
        EditorUtility.SetDirty(roster);
        return roster;
    }

    const string PlaceholderPortraitFolder = "Assets/Game/Dialogue/Portraits/Placeholders";

    static Sprite LoadPlaceholderPortrait(string spriteName)
    {
        string path = $"{PlaceholderPortraitFolder}/{spriteName}.jpg";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[EventSystemTestLab] Missing placeholder portrait at {path}.");
        return sprite;
    }

    static DialogueAsset EnsureDialogueAsset(
        string assetName,
        string dialogueId,
        bool lockGameplay,
        params (string id, string speaker, string text, string next)[] lines)
    {
        string path = $"{DialogueAssetFolder}/{assetName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.dialogueId = dialogueId;
        asset.lockGameplay = lockGameplay;
        asset.defaultTypewriterCharsPerSecond = 48f;
        asset.nodes = new System.Collections.Generic.List<DialogueNode>();
        for (int i = 0; i < lines.Length; i++)
        {
            var L = lines[i];
            asset.nodes.Add(new DialogueLineNode
            {
                nodeId = L.id,
                speakerId = L.speaker,
                text = L.text,
                nextNodeId = L.next,
                portraitExpression = "Neutral",
            });
        }

        if (lines.Length > 0)
            asset.entryNodeId = lines[0].id;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    static void MarkEnd(DialogueAsset asset, string endId, string lastLineId)
    {
        // Optional explicit end node — empty next already ends in Phase A.
        if (asset.FindNode(endId) == null)
        {
            asset.nodes.Add(new DialogueEndNode { nodeId = endId });
            var last = asset.FindNode(lastLineId) as DialogueLineNode;
            if (last != null && string.IsNullOrEmpty(last.nextNodeId))
                last.nextNodeId = endId;
            EditorUtility.SetDirty(asset);
        }
    }

    static NarrativeDialogueTrigger CreateNarrativeTrigger(
        string name,
        Vector3 position,
        DialogueAsset asset,
        NarrativeDialogueTrigger.FireMode fireMode,
        NarrativeDialogueTrigger.LockOverride lockOverride,
        DialogueProgressionMode progression,
        NarrativeDialogueTrigger.ConflictPolicy conflict)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3.5f, 3f, 3.5f);
        box.center = new Vector3(0f, 1f, 0f);

        var trigger = go.AddComponent<NarrativeDialogueTrigger>();
        trigger.triggerId = name;
        trigger.dialogueAsset = asset;
        trigger.fireMode = fireMode;
        trigger.lockGameplay = lockOverride;
        trigger.progressionMode = progression;
        trigger.autoAdvanceHoldSeconds = 1.5f;
        trigger.retriggerCooldownSeconds = 2.5f;
        trigger.conflictPolicy = conflict;
        trigger.cancelPendingOnExit = true;
        trigger.actorFilter = NarrativeDialogueTrigger.ActorFilter.ActivePlayerOnly;
        trigger.allowCancel = true;

        // Visible floor pad for QA.
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "Pad";
        pad.transform.SetParent(go.transform, false);
        pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pad.transform.localScale = new Vector3(3.2f, 0.1f, 3.2f);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        var rend = pad.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null && rend != null)
        {
            var mat = new Material(shader);
            var color = new Color(1f, 0.85f, 0.2f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            rend.sharedMaterial = mat;
        }

        Undo.RegisterCreatedObjectUndo(go, "Create Narrative Trigger");
        return trigger;
    }

    static NpcDialogueHost CreateNpcCapsule(
        string name,
        Vector3 position,
        Color color,
        NpcDialogueMode mode,
        DialogueAsset ambient,
        DialogueAsset interactive,
        InteractPromptMode promptMode)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

        var renderer = go.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null && renderer != null)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }

        // Solid capsule is fine; interaction uses distance, not this collider as trigger.
        var host = go.AddComponent<NpcDialogueHost>();
        host.mode = mode;
        host.ambientDialogue = ambient;
        host.interactiveDialogue = interactive;
        host.promptMode = promptMode;
        host.ambientProximityRadius = 2.8f;
        host.ambientCooldownSeconds = 3f;
        host.ambientHoldSeconds = 1.6f;
        host.ambientRepeat = NpcDialogueHost.AmbientRepeatMode.Repeat;
        host.discoveryRadius = 4.5f;
        host.interactionRadius = 2.5f;
        host.interactiveLockGameplay = true;
        host.requireFacingWithinDegrees = 0f; // full 360° (billboard-safe)

        // Infer speaker id from dialogue asset lines when possible.
        if (interactive != null && interactive.nodes != null)
        {
            for (int i = 0; i < interactive.nodes.Count; i++)
            {
                if (interactive.nodes[i] is DialogueLineNode ln &&
                    !string.IsNullOrEmpty(ln.speakerId) &&
                    ln.speakerId != "jyn" && ln.speakerId != "ax" &&
                    ln.speakerId != "player" && ln.speakerId != "active_player")
                {
                    host.npcSpeakerId = ln.speakerId;
                    break;
                }
            }
        }

        if (ambient != null && string.IsNullOrEmpty(host.npcSpeakerId) && ambient.nodes != null)
        {
            for (int i = 0; i < ambient.nodes.Count; i++)
            {
                if (ambient.nodes[i] is DialogueLineNode ln && !string.IsNullOrEmpty(ln.speakerId))
                {
                    host.npcSpeakerId = ln.speakerId;
                    break;
                }
            }
        }

        // Trigger collider so the player can stand in the footprint from any direction.
        var col = go.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        // InteractionVolume is created in NpcDialogueHost.Awake (360° sphere at NPC center).

        var anchor = new GameObject("SpeechAnchor");
        anchor.transform.SetParent(go.transform, false);
        anchor.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        host.speechAnchor = anchor.transform;

        return host;
    }

    /// <summary>
    /// One linear DialogueAsset with per-character body text.
    /// Does not rewrite Phase A assets or destroy Phase A scene objects.
    /// </summary>
    static void CreateLineVariantFixture()
    {
        EnsureDialogueAssetFolder();
        DestroyNamedRoot("NPC_LineVariants");

        var asset = CreateLineVariantAsset();
        var host = CreateNpcCapsule(
            "NPC_LineVariants",
            new Vector3(0f, 1f, -28f),
            new Color(0.2f, 0.85f, 0.55f),
            NpcDialogueMode.InteractiveOnly,
            null,
            asset,
            InteractPromptMode.DiscoveryAndInteraction);
        // Non-blocking so QA can swap party mid-session and confirm the id stays frozen.
        host.interactiveLockGameplay = false;
        EditorUtility.SetDirty(host);
    }

    static DialogueAsset CreateLineVariantAsset()
    {
        const string assetName = "Lab_Line_Variants";
        string path = $"{DialogueAssetFolder}/{assetName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.dialogueId = "lab_line_variants";
        asset.lockGameplay = false;
        asset.defaultTypewriterCharsPerSecond = 48f;
        asset.entryNodeId = "v1";
        asset.nodes = new System.Collections.Generic.List<DialogueNode>
        {
            new DialogueLineNode
            {
                nodeId = "v1",
                speakerId = "villager_a",
                text = "We should investigate.",
                nextNodeId = "v2",
                portraitExpression = "Neutral",
                textVariants = new System.Collections.Generic.List<DialogueTextVariant>
                {
                    new DialogueTextVariant { characterId = "ax", text = "Let's go! I'm not scared." },
                    new DialogueTextVariant { characterId = "jyn", text = "We should approach carefully." },
                },
            },
            new DialogueLineNode
            {
                nodeId = "v2",
                speakerId = "villager_a",
                text = "The road looks the same either way.",
                nextNodeId = "v3",
                portraitExpression = "Neutral",
            },
            new DialogueLineNode
            {
                nodeId = "v3",
                speakerId = "active_player",
                text = "I'll follow your lead.",
                nextNodeId = "v4",
                portraitExpression = "Neutral",
                textVariants = new System.Collections.Generic.List<DialogueTextVariant>
                {
                    new DialogueTextVariant { characterId = "ax", text = "I'll take point." },
                },
            },
            new DialogueLineNode
            {
                nodeId = "v4",
                speakerId = "villager_a",
                text = "Blank variants fall back to this.",
                nextNodeId = "v_end",
                portraitExpression = "Neutral",
                textVariants = new System.Collections.Generic.List<DialogueTextVariant>
                {
                    new DialogueTextVariant { characterId = "ax", text = "" },
                },
            },
            new DialogueEndNode { nodeId = "v_end" },
        };

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>
    /// OncePersistent pads for Completed / Failed / Cancelled.
    /// Does not rewrite Phase A assets or NPC_LineVariants.
    /// </summary>
    static void CreateOutcomeFixtures()
    {
        EnsureDialogueAssetFolder();

        DestroyNamedRoot("Trigger_Outcome_OnceComplete");
        DestroyNamedRoot("Trigger_Outcome_OnceFail");
        DestroyNamedRoot("Trigger_Outcome_OnceCancel");
        DestroyNamedRoot("Trigger_Outcome_InterruptVictim");
        DestroyNamedRoot("Trigger_Outcome_InterruptIncoming");

        var complete = CreateOutcomeLineAsset(
            "Lab_Outcome_Complete",
            "lab_outcome_complete",
            "oc1",
            "Outcome complete. Confirm once. This should set the seen flag.",
            "oc_end",
            includeEnd: true);
        var fail = CreateOutcomeLineAsset(
            "Lab_Outcome_Fail",
            "lab_outcome_fail",
            "of1",
            "Outcome fail. Confirm once. The next id does not exist.",
            "missing_outcome_node",
            includeEnd: false);
        var hold = CreateOutcomeLineAsset(
            "Lab_Outcome_Hold",
            "lab_outcome_hold",
            "oh1",
            "Hold line. Esc cancels and does not set the seen flag. Confirm repeats this line.",
            "oh1",
            includeEnd: false);
        var interruptLine = CreateOutcomeLineAsset(
            "Lab_Outcome_Interrupt",
            "lab_outcome_interrupt",
            "oi1",
            "Interrupt landed. The blue pad's seen flag must stay off.",
            "oi_end",
            includeEnd: true);

        const float z = -32f;
        TintPad(CreateNarrativeTrigger(
            "Trigger_Outcome_OnceComplete",
            new Vector3(0f, 1f, z),
            complete,
            NarrativeDialogueTrigger.FireMode.OncePersistent,
            NarrativeDialogueTrigger.LockOverride.ForceBlocking,
            DialogueProgressionMode.Manual,
            NarrativeDialogueTrigger.ConflictPolicy.Ignore),
            new Color(0.25f, 0.8f, 0.35f));

        TintPad(CreateNarrativeTrigger(
            "Trigger_Outcome_OnceFail",
            new Vector3(6f, 1f, z),
            fail,
            NarrativeDialogueTrigger.FireMode.OncePersistent,
            NarrativeDialogueTrigger.LockOverride.ForceBlocking,
            DialogueProgressionMode.Manual,
            NarrativeDialogueTrigger.ConflictPolicy.Ignore),
            new Color(0.9f, 0.28f, 0.25f));

        TintPad(CreateNarrativeTrigger(
            "Trigger_Outcome_OnceCancel",
            new Vector3(12f, 1f, z),
            hold,
            NarrativeDialogueTrigger.FireMode.OncePersistent,
            NarrativeDialogueTrigger.LockOverride.ForceBlocking,
            DialogueProgressionMode.Manual,
            NarrativeDialogueTrigger.ConflictPolicy.Ignore),
            new Color(0.95f, 0.55f, 0.2f));

        TintPad(CreateNarrativeTrigger(
            "Trigger_Outcome_InterruptVictim",
            new Vector3(18f, 1f, z),
            hold,
            NarrativeDialogueTrigger.FireMode.OncePersistent,
            NarrativeDialogueTrigger.LockOverride.ForceNonBlocking,
            DialogueProgressionMode.Manual,
            NarrativeDialogueTrigger.ConflictPolicy.Ignore),
            new Color(0.3f, 0.55f, 1f));

        TintPad(CreateNarrativeTrigger(
            "Trigger_Outcome_InterruptIncoming",
            new Vector3(23f, 1f, z),
            interruptLine,
            NarrativeDialogueTrigger.FireMode.Repeat,
            NarrativeDialogueTrigger.LockOverride.ForceBlocking,
            DialogueProgressionMode.Manual,
            NarrativeDialogueTrigger.ConflictPolicy.Interrupt),
            new Color(0.72f, 0.32f, 0.85f));
    }

    static DialogueAsset CreateOutcomeLineAsset(
        string assetName,
        string dialogueId,
        string lineId,
        string text,
        string nextNodeId,
        bool includeEnd)
    {
        string path = $"{DialogueAssetFolder}/{assetName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.dialogueId = dialogueId;
        asset.lockGameplay = true;
        asset.defaultTypewriterCharsPerSecond = 48f;
        asset.entryNodeId = lineId;
        asset.nodes = new System.Collections.Generic.List<DialogueNode>
        {
            new DialogueLineNode
            {
                nodeId = lineId,
                speakerId = "active_player",
                text = text,
                nextNodeId = nextNodeId,
                portraitExpression = "Neutral",
            },
        };

        if (includeEnd && !string.IsNullOrEmpty(nextNodeId))
            asset.nodes.Add(new DialogueEndNode { nodeId = nextNodeId });

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    static void CreateChoiceFixtures()
    {
        EnsureDialogueAssetFolder();

        DestroyNamedRoot("NPC_Choice_Converge");
        DestroyNamedRoot("NPC_Choice_Automatic");
        DestroyNamedRoot("NPC_Choice_BadTarget");
        DestroyNamedRoot("NPC_Choice_Cancel");

        var converge = CreateChoiceAsset("Lab_Choice_Converge", "lab_choice_converge", "cq1", new DialogueNode[]
        {
            Line("cq1", "villager_a", "Which road should we take?", "cq_choice"),
            Choice("cq_choice", "Choose a road", new[]
            {
                Option("opt_left", "The left road.", "cq_left"),
                Option("opt_middle", "The middle road.", "cq_mid"),
                Option("opt_right", "The right road.", "cq_right"),
            }),
            Line("cq_left", "active_player", "Left road it is.", "cq_shared"),
            Line("cq_mid", "active_player", "Middle road.", "cq_shared"),
            Line("cq_right", "active_player", "Right road.", "cq_shared"),
            Line("cq_shared", "villager_a", "Then we agree.", "cq_end"),
            new DialogueEndNode { nodeId = "cq_end" },
        });

        var automatic = CreateChoiceAsset("Lab_Choice_Automatic", "lab_choice_automatic", "aq1", new DialogueNode[]
        {
            Line("aq1", "villager_a", "This line advances on its own. The menu must wait for you.", "aq_choice"),
            Choice("aq_choice", "Confirm a reply", new[]
            {
                Option("opt_waited", "I waited.", "aq_reply"),
                Option("opt_also", "I also waited.", "aq_reply"),
            }),
            Line("aq_reply", "active_player", "I confirmed it myself.", "aq_end"),
            new DialogueEndNode { nodeId = "aq_end" },
        });

        var bad = CreateChoiceAsset("Lab_Choice_BadTarget", "lab_choice_bad_target", "bq1", new DialogueNode[]
        {
            Line("bq1", "villager_a", "Choose the broken reply.", "bq_choice"),
            Choice("bq_choice", "", new[]
            {
                Option("opt_missing", "This destination does not exist.", "missing_choice_node"),
            }),
        });

        var cancel = CreateChoiceAsset("Lab_Choice_Cancel", "lab_choice_cancel", "xq1", new DialogueNode[]
        {
            Line("xq1", "villager_a", "Esc cancels this menu. Choosing continues.", "xq_choice"),
            Choice("xq_choice", "Cancel or stay", new[]
            {
                Option("opt_stay", "Stay and talk.", "xq_stay"),
                Option("opt_leave", "Leave the topic.", "xq_end"),
            }),
            Line("xq_stay", "active_player", "I'll stay.", "xq_end"),
            new DialogueEndNode { nodeId = "xq_end" },
        });

        const float z = -36f;
        var gold = CreateNpcCapsule(
            "NPC_Choice_Converge",
            new Vector3(0f, 1f, z),
            new Color(0.95f, 0.78f, 0.25f),
            NpcDialogueMode.InteractiveOnly,
            null,
            converge,
            InteractPromptMode.DiscoveryAndInteraction);
        gold.interactiveLockGameplay = true;
        gold.allowCancelInteractive = true;
        gold.interactiveProgression = DialogueProgressionMode.Manual;

        var cyan = CreateNpcCapsule(
            "NPC_Choice_Automatic",
            new Vector3(8f, 1f, z),
            new Color(0.3f, 0.85f, 0.9f),
            NpcDialogueMode.InteractiveOnly,
            null,
            automatic,
            InteractPromptMode.DiscoveryAndInteraction);
        cyan.interactiveLockGameplay = true;
        cyan.allowCancelInteractive = true;
        cyan.interactiveProgression = DialogueProgressionMode.Automatic;

        var red = CreateNpcCapsule(
            "NPC_Choice_BadTarget",
            new Vector3(16f, 1f, z),
            new Color(0.75f, 0.22f, 0.22f),
            NpcDialogueMode.InteractiveOnly,
            null,
            bad,
            InteractPromptMode.DiscoveryAndInteraction);
        red.interactiveLockGameplay = true;
        red.allowCancelInteractive = true;

        var blue = CreateNpcCapsule(
            "NPC_Choice_Cancel",
            new Vector3(24f, 1f, z),
            new Color(0.35f, 0.5f, 0.85f),
            NpcDialogueMode.InteractiveOnly,
            null,
            cancel,
            InteractPromptMode.DiscoveryAndInteraction);
        blue.interactiveLockGameplay = true;
        blue.allowCancelInteractive = true;
    }

    static DialogueLineNode Line(string id, string speaker, string text, string next)
    {
        return new DialogueLineNode
        {
            nodeId = id,
            speakerId = speaker,
            text = text,
            nextNodeId = next,
            portraitExpression = "Neutral",
        };
    }

    static DialogueChoiceOption Option(string id, string text, string next)
    {
        return new DialogueChoiceOption
        {
            optionId = id,
            text = text,
            nextNodeId = next,
        };
    }

    static DialogueChoiceNode Choice(string id, string prompt, DialogueChoiceOption[] options)
    {
        return new DialogueChoiceNode
        {
            nodeId = id,
            promptText = prompt,
            options = new System.Collections.Generic.List<DialogueChoiceOption>(options),
        };
    }

    static DialogueAsset CreateChoiceAsset(string assetName, string dialogueId, string entry, DialogueNode[] nodes)
    {
        string path = $"{DialogueAssetFolder}/{assetName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.dialogueId = dialogueId;
        asset.lockGameplay = true;
        asset.overrideChoicePresentation = false;
        asset.defaultTypewriterCharsPerSecond = 48f;
        asset.entryNodeId = entry;
        asset.nodes = new System.Collections.Generic.List<DialogueNode>(nodes);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    static void TintPad(Component owner, Color color)
    {
        if (owner == null)
            return;
        var pad = owner.transform.Find("Pad");
        if (pad == null)
            return;
        var rend = pad.GetComponent<MeshRenderer>();
        if (rend == null || rend.sharedMaterial == null)
            return;
        if (rend.sharedMaterial.HasProperty("_BaseColor"))
            rend.sharedMaterial.SetColor("_BaseColor", color);
        else if (rend.sharedMaterial.HasProperty("_Color"))
            rend.sharedMaterial.SetColor("_Color", color);
    }
}
