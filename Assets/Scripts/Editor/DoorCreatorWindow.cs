using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool for level designers. One window, all door options:
///   - Open Mode (Auto / Switch / Key)
///   - Motion Mode (Rotate / Slide Up / Slide Horizontal / Double Horizontal)
///   - Door mesh prefab (drag & drop)
///   - Key id (for Key mode)
///
/// Builds the full hierarchy in one click. Prefab is instantiated as a child of
/// the Door GameObject so the designer can still tweak the mesh independently.
/// </summary>
public class DoorCreatorWindow : EditorWindow
{
    enum DoorOpenMode { Auto, Switch, Key }
    enum DoorMotionMode { Rotate, SlideUp, SlideHorizontal, DoubleHorizontal }

    DoorOpenMode openMode = DoorOpenMode.Switch;
    DoorMotionMode motionMode = DoorMotionMode.SlideUp;

    GameObject doorPrefab;
    GameObject leftPanelPrefab;
    GameObject rightPanelPrefab;

    // Mode-specific tuning
    float openAngle = 90f;
    float rotateSpeed = 3f;
    float openHeight = 3f;
    float openDistance = 3f;
    float moveSpeed = 3f;
    bool slideToRight = true;
    float doubleOpenDistance = 1.5f;

    // Key mode
    ItemDefinition requiredKey;
    string requiredKeyId = "";
    bool consumeKey = false;

    // Switch positioning
    Vector3 switchOffset = new Vector3(2f, 0f, 0f);

    // Auto/Key trigger size
    float triggerRadius = 2f;

    Vector2 scroll;

    [MenuItem("Tools/ARPG/Level Design/Door Creator", false, 100)]
    public static void Open()
    {
        var w = GetWindow<DoorCreatorWindow>("Door Creator");
        w.minSize = new Vector2(380, 460);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Door Creator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Build a complete door with the chosen settings.", EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        // --- Modes ---
        EditorGUILayout.LabelField("Modes", EditorStyles.boldLabel);
        openMode = (DoorOpenMode)EditorGUILayout.EnumPopup("Open Mode", openMode);
        motionMode = (DoorMotionMode)EditorGUILayout.EnumPopup("Motion Mode", motionMode);
        EditorGUILayout.Space();

        // --- Mesh prefabs ---
        EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
        if (motionMode == DoorMotionMode.DoubleHorizontal)
        {
            leftPanelPrefab  = (GameObject)EditorGUILayout.ObjectField("Left Panel Prefab",  leftPanelPrefab,  typeof(GameObject), false);
            rightPanelPrefab = (GameObject)EditorGUILayout.ObjectField("Right Panel Prefab", rightPanelPrefab, typeof(GameObject), false);
        }
        else
        {
            doorPrefab = (GameObject)EditorGUILayout.ObjectField("Door Prefab", doorPrefab, typeof(GameObject), false);
        }
        EditorGUILayout.LabelField("Leave empty to create a placeholder cube.", EditorStyles.miniLabel);
        EditorGUILayout.Space();

        // --- Motion tuning ---
        EditorGUILayout.LabelField("Motion Settings", EditorStyles.boldLabel);
        switch (motionMode)
        {
            case DoorMotionMode.Rotate:
                openAngle = EditorGUILayout.FloatField("Open Angle (°)", openAngle);
                rotateSpeed = EditorGUILayout.FloatField("Rotate Speed", rotateSpeed);
                break;

            case DoorMotionMode.SlideUp:
                openHeight = EditorGUILayout.FloatField("Open Height", openHeight);
                moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
                break;

            case DoorMotionMode.SlideHorizontal:
                openDistance = EditorGUILayout.FloatField("Open Distance", openDistance);
                slideToRight = EditorGUILayout.Toggle("Slide To Right", slideToRight);
                moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
                break;

            case DoorMotionMode.DoubleHorizontal:
                doubleOpenDistance = EditorGUILayout.FloatField("Double Open Distance", doubleOpenDistance);
                moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
                break;
        }
        EditorGUILayout.Space();

        // --- Open mode specifics ---
        EditorGUILayout.LabelField("Open Mode Settings", EditorStyles.boldLabel);
        switch (openMode)
        {
            case DoorOpenMode.Auto:
                triggerRadius = EditorGUILayout.FloatField("Trigger Radius", triggerRadius);
                break;

            case DoorOpenMode.Switch:
                switchOffset = EditorGUILayout.Vector3Field("Switch Offset", switchOffset);
                triggerRadius = EditorGUILayout.FloatField("Switch Trigger Radius", triggerRadius);
                break;

            case DoorOpenMode.Key:
                requiredKey = (ItemDefinition)EditorGUILayout.ObjectField("Required Key Item", requiredKey, typeof(ItemDefinition), false);
                requiredKeyId = EditorGUILayout.TextField("Required Key Id (fallback)", requiredKeyId);
                consumeKey = EditorGUILayout.Toggle("Consume Key On Use", consumeKey);
                triggerRadius = EditorGUILayout.FloatField("Trigger Radius", triggerRadius);
                break;
        }
        EditorGUILayout.Space(16);

        // --- Validation + Create ---
        string err = Validate();
        if (!string.IsNullOrEmpty(err))
            EditorGUILayout.HelpBox(err, MessageType.Warning);

        GUI.enabled = string.IsNullOrEmpty(err);
        if (GUILayout.Button("Create Door", GUILayout.Height(36)))
            CreateDoor();
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    string Validate()
    {
        if (motionMode == DoorMotionMode.DoubleHorizontal)
        {
            if (leftPanelPrefab == null || rightPanelPrefab == null)
                return "Double Horizontal requires both Left and Right panel prefabs.";
        }

        if (openMode == DoorOpenMode.Key && requiredKey == null && string.IsNullOrEmpty(requiredKeyId))
            return "Key mode requires either a Required Key Item or a Required Key Id.";

        return "";
    }

    // ============================================================
    // BUILDER
    // ============================================================
    void CreateDoor()
    {
        string rootName = $"Door_{openMode}_{motionMode}";
        var root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Door");

        // Position at scene view camera so the designer sees it immediately
        var sceneCam = SceneView.lastActiveSceneView;
        if (sceneCam != null && sceneCam.camera != null)
            root.transform.position = sceneCam.pivot;

        SimpleDoor primaryDoor;

        if (motionMode == DoorMotionMode.DoubleHorizontal)
        {
            primaryDoor = BuildDoubleHorizontal(root);
        }
        else
        {
            primaryDoor = BuildSingleDoor(root);
        }

        // Apply open mode specifics
        switch (openMode)
        {
            case DoorOpenMode.Auto:
                primaryDoor.openMode = SimpleDoor.OpenMode.Auto;
                AddAutoTrigger(primaryDoor);
                break;

            case DoorOpenMode.Switch:
                primaryDoor.openMode = SimpleDoor.OpenMode.Switch;
                BuildSwitch(root, primaryDoor);
                break;

            case DoorOpenMode.Key:
                primaryDoor.openMode = SimpleDoor.OpenMode.Key;
                primaryDoor.requiredKey = requiredKey;
                primaryDoor.requiredKeyId = requiredKeyId;
                primaryDoor.consumeKeyOnUse = consumeKey;
                AddKeyTrigger(primaryDoor);
                break;
        }

        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log($"[DoorCreator] Created {rootName}. Tweak position in scene, " +
                  $"reparent your mesh prefab if needed, and you're done.");
    }

    SimpleDoor BuildSingleDoor(GameObject root)
    {
        var doorGO = new GameObject("Door");
        Undo.RegisterCreatedObjectUndo(doorGO, "Door GameObject");
        doorGO.transform.SetParent(root.transform, worldPositionStays: false);

        InstantiateMeshOrPlaceholder(doorGO, doorPrefab, new Vector3(1.5f, 2f, 0.2f));

        // SimpleDoor + collider on the door root (collider needed so player can collide / triggers work)
        EnsureBlockingCollider(doorGO);
        var door = Undo.AddComponent<SimpleDoor>(doorGO);
        ApplyMotionToSingle(door);
        return door;
    }

    SimpleDoor BuildDoubleHorizontal(GameObject root)
    {
        // Parent that holds the SimpleDoor (with double-panel motion)
        var doorGO = new GameObject("Door");
        Undo.RegisterCreatedObjectUndo(doorGO, "Door GameObject");
        doorGO.transform.SetParent(root.transform, worldPositionStays: false);

        // Left panel child
        var leftGO = InstantiateMeshOrPlaceholder(doorGO, leftPanelPrefab, new Vector3(0.75f, 2f, 0.2f));
        leftGO.name = "LeftPanel";
        leftGO.transform.localPosition = new Vector3(-0.4f, 0f, 0f);

        // Right panel child
        var rightGO = InstantiateMeshOrPlaceholder(doorGO, rightPanelPrefab, new Vector3(0.75f, 2f, 0.2f));
        rightGO.name = "RightPanel";
        rightGO.transform.localPosition = new Vector3(0.4f, 0f, 0f);

        EnsureBlockingCollider(doorGO);
        var door = Undo.AddComponent<SimpleDoor>(doorGO);

        door.useRotate = false;
        door.useSlideUp = false;
        door.useSlideHorizontal = false;
        door.useDoubleHorizontal = true;

        door.leftPanel = leftGO.transform;
        door.rightPanel = rightGO.transform;
        door.doubleOpenDistance = doubleOpenDistance;
        door.moveSpeed = moveSpeed;

        return door;
    }

    void ApplyMotionToSingle(SimpleDoor door)
    {
        door.useRotate = false;
        door.useSlideUp = false;
        door.useSlideHorizontal = false;
        door.useDoubleHorizontal = false;

        switch (motionMode)
        {
            case DoorMotionMode.Rotate:
                door.useRotate = true;
                door.openAngle = openAngle;
                door.rotateSpeed = rotateSpeed;
                break;

            case DoorMotionMode.SlideUp:
                door.useSlideUp = true;
                door.openHeight = openHeight;
                door.moveSpeed = moveSpeed;
                break;

            case DoorMotionMode.SlideHorizontal:
                door.useSlideHorizontal = true;
                door.openDistance = openDistance;
                door.slideToRight = slideToRight;
                door.moveSpeed = moveSpeed;
                break;
        }
    }

    GameObject InstantiateMeshOrPlaceholder(GameObject parent, GameObject prefab, Vector3 placeholderScale)
    {
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "Instantiate door mesh");
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Mesh (placeholder)";
            Undo.RegisterCreatedObjectUndo(go, "Door placeholder");
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.localScale = placeholderScale;
        }
        return go;
    }

    void EnsureBlockingCollider(GameObject doorGO)
    {
        // The SimpleDoor itself needs a collider on the same GameObject for
        // OnTriggerEnter (Auto/Key modes) OR a non-trigger blocking collider (Switch).
        if (doorGO.GetComponent<Collider>() == null)
            Undo.AddComponent<BoxCollider>(doorGO);
    }

    void AddAutoTrigger(SimpleDoor door)
    {
        // For Auto mode, the SimpleDoor's own collider is the trigger.
        var col = door.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Also add a sphere collider as a wider proximity zone
        var trig = Undo.AddComponent<SphereCollider>(door.gameObject);
        trig.isTrigger = true;
        trig.radius = triggerRadius;
    }

    void AddKeyTrigger(SimpleDoor door)
    {
        var col = door.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var trig = Undo.AddComponent<SphereCollider>(door.gameObject);
        trig.isTrigger = true;
        trig.radius = triggerRadius;

        // Key doors need the prompt for "press E to open"
        if (door.GetComponent<InteractivePromptTrigger>() == null)
            Undo.AddComponent<InteractivePromptTrigger>(door.gameObject);
    }

    void BuildSwitch(GameObject root, SimpleDoor door)
    {
        var switchGO = new GameObject("Switch");
        Undo.RegisterCreatedObjectUndo(switchGO, "Switch");
        switchGO.transform.SetParent(root.transform, worldPositionStays: false);
        switchGO.transform.localPosition = switchOffset;

        // Visual placeholder
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual (placeholder)";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(visual, "Switch visual");
        visual.transform.SetParent(switchGO.transform, worldPositionStays: false);
        visual.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);

        // Trigger
        var trig = Undo.AddComponent<SphereCollider>(switchGO);
        trig.isTrigger = true;
        trig.radius = triggerRadius;

        // DoorSwitch linked to the door
        var ds = Undo.AddComponent<DoorSwitch>(switchGO);
        ds.doors = new[] { door };

        // Player prompt
        if (switchGO.GetComponent<InteractivePromptTrigger>() == null)
            Undo.AddComponent<InteractivePromptTrigger>(switchGO);
    }
}
