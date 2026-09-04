using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool for level designers. Builds a complete climbable Ladder in one
/// click: trigger collider sized to the ladder's height, TopPoint/BottomPoint
/// anchors, climb-facing direction (which way the player animation faces
/// while climbing), and an optional mesh prefab (or placeholder).
///
/// Mirrors DoorCreatorWindow's structure/conventions.
/// </summary>
public class LadderCreatorWindow : EditorWindow
{
    // Mesh
    GameObject ladderPrefab;

    // Size
    float ladderHeight = 3f;
    float ladderWidth = 1f;
    float ladderDepth = 0.6f;

    // Climb facing
    ClimbFacingDir climbFacing = ClimbFacingDir.Up;

    // Bottom point
    bool createBottomPoint = true;

    // Top exit
    float topExitLift = 0.35f;
    float topExitForward = 0.5f;

    Vector2 scroll;

    [MenuItem("Tools/ARPG/Level Design/Ladder Creator", false, 103)]
    public static void Open()
    {
        var w = GetWindow<LadderCreatorWindow>("Ladder Creator");
        w.minSize = new Vector2(380, 420);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Ladder Creator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Builds a complete climbable ladder — trigger, " +
                                    "top/bottom points, and climb-facing direction.",
                                    EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        // --- Mesh ---
        EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
        ladderPrefab = (GameObject)EditorGUILayout.ObjectField("Ladder Prefab", ladderPrefab, typeof(GameObject), false);
        EditorGUILayout.LabelField("Leave empty to create a placeholder box.", EditorStyles.miniLabel);
        EditorGUILayout.Space();

        // --- Size ---
        EditorGUILayout.LabelField("Size", EditorStyles.boldLabel);
        ladderHeight = EditorGUILayout.FloatField("Height", ladderHeight);
        ladderWidth  = EditorGUILayout.FloatField("Width",  ladderWidth);
        ladderDepth  = EditorGUILayout.FloatField("Depth (trigger thickness)", ladderDepth);
        EditorGUILayout.Space();

        // --- Climb facing ---
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        climbFacing = (ClimbFacingDir)EditorGUILayout.EnumPopup("Climb Facing Direction", climbFacing);
        EditorGUILayout.LabelField("Direction the player sprite faces while climbing " +
                                    "(feeds the animator's MoveX/MoveY directly).",
                                    EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();

        // --- Bottom point ---
        EditorGUILayout.LabelField("Bottom Exit", EditorStyles.boldLabel);
        createBottomPoint = EditorGUILayout.Toggle("Create Bottom Point", createBottomPoint);
        EditorGUILayout.LabelField("Player exits cleanly when descending past this " +
                                    "point AND grounded. Leave off if the ladder bottom " +
                                    "just continues onto walkable floor.",
                                    EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();

        // --- Top exit ---
        EditorGUILayout.LabelField("Top Exit", EditorStyles.boldLabel);
        topExitLift    = EditorGUILayout.FloatField("Exit Lift",    topExitLift);
        topExitForward = EditorGUILayout.FloatField("Exit Forward", topExitForward);
        EditorGUILayout.Space(16);

        // --- Validation + Create ---
        string err = Validate();
        if (!string.IsNullOrEmpty(err))
            EditorGUILayout.HelpBox(err, MessageType.Warning);

        GUI.enabled = string.IsNullOrEmpty(err);
        if (GUILayout.Button("Create Ladder", GUILayout.Height(36)))
            CreateLadder();
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    string Validate()
    {
        if (ladderHeight <= 0f) return "Height must be greater than 0.";
        if (ladderWidth <= 0f) return "Width must be greater than 0.";
        if (ladderDepth <= 0f) return "Depth must be greater than 0.";
        return "";
    }

    // ============================================================
    // BUILDER
    // ============================================================
    void CreateLadder()
    {
        string rootName = $"Ladder_{climbFacing}";
        var root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Ladder");

        var sceneCam = SceneView.lastActiveSceneView;
        if (sceneCam != null && sceneCam.camera != null)
            root.transform.position = sceneCam.pivot;

        // Trigger collider — spans the full height, centered.
        var box = Undo.AddComponent<BoxCollider>(root);
        box.isTrigger = true;
        box.size = new Vector3(ladderWidth, ladderHeight, ladderDepth);
        box.center = new Vector3(0f, ladderHeight * 0.5f, 0f);

        // Mesh (or placeholder)
        InstantiateMeshOrPlaceholder(root, ladderPrefab,
            new Vector3(ladderWidth, ladderHeight, ladderDepth * 0.5f),
            new Vector3(0f, ladderHeight * 0.5f, 0f));

        // Ladder component
        var ladder = Undo.AddComponent<Ladder>(root);
        ladder.climbFacing = climbFacing;
        ladder.topExitLift = topExitLift;
        ladder.topExitForward = topExitForward;

        // Top point — always created.
        var topGO = new GameObject("TopPoint");
        Undo.RegisterCreatedObjectUndo(topGO, "Ladder TopPoint");
        topGO.transform.SetParent(root.transform, worldPositionStays: false);
        topGO.transform.localPosition = new Vector3(0f, ladderHeight, 0f);
        ladder.topPoint = topGO.transform;

        // Bottom point — optional.
        if (createBottomPoint)
        {
            var bottomGO = new GameObject("BottomPoint");
            Undo.RegisterCreatedObjectUndo(bottomGO, "Ladder BottomPoint");
            bottomGO.transform.SetParent(root.transform, worldPositionStays: false);
            bottomGO.transform.localPosition = Vector3.zero;
            ladder.bottomPoint = bottomGO.transform;
        }

        // ladderTrigger defaults to this object's own Collider in Ladder.Awake(),
        // no need to assign it explicitly.

        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log($"[LadderCreator] Created '{rootName}'. Position it against the wall, " +
                  "and drag your ladder mesh in if you used a placeholder.");
    }

    GameObject InstantiateMeshOrPlaceholder(GameObject parent, GameObject prefab,
        Vector3 placeholderScale, Vector3 placeholderLocalPos)
    {
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "Instantiate ladder mesh");
            go.transform.localPosition = Vector3.zero;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Mesh (placeholder)";
            Undo.RegisterCreatedObjectUndo(go, "Ladder placeholder");
            // Placeholder mesh shouldn't add its own collider — the ladder's
            // BoxCollider (on the root) is the one and only trigger.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.localScale = placeholderScale;
            go.transform.localPosition = placeholderLocalPos;
        }
        return go;
    }
}
