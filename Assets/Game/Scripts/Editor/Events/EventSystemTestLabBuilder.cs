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
}
