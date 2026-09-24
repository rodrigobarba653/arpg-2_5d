using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;

/// <summary>
/// Builds a combat test arena: 4 hollow ProBuilder rings linked by bridges,
/// with inner/outer rim walls so players don't fall into the holes or off the edge.
///
/// Menu: Tools / ARPG / Level Design / Create Battle Ring Scene
/// Batch: Unity -batchmode -executeMethod BattleRingSceneBuilder.Build
/// </summary>
public static class BattleRingSceneBuilder
{
    const string ScenePath = "Assets/Game/Scenes/BattleRing.unity";

    // Ring floor (hollow annulus)
    const float RingOuterRadius = 10f;
    const float RingWalkWidth = 3.5f;
    const float FloorHeight = 0.45f;
    const int RingSides = 32;

    // Layout: 2x2 centers
    const float RingCenterOffset = 13f;

    // Safety walls
    const float WallHeight = 2.2f;
    const float WallThickness = 0.35f;

    // Bridges
    const float BridgeWidth = 3.2f;
    const float BridgeOverlap = 1.25f;

    [MenuItem("Tools/ARPG/Level Design/Create Battle Ring Scene", false, 120)]
    public static void CreateFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Create Battle Ring Scene",
                "This creates/overwrites:\n" + ScenePath + "\n\nContinue?",
                "Create", "Cancel"))
            return;

        Build();
        EditorUtility.DisplayDialog("Battle Ring", "Scene ready:\n" + ScenePath, "OK");
    }

    /// <summary>Batchmode entry: Unity -batchmode -executeMethod BattleRingSceneBuilder.Build</summary>
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "BattleRing";

        var root = new GameObject("BattleRing");

        // --- Lighting ---
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);

        // --- Rings ---
        Vector3[] centers =
        {
            new Vector3(-RingCenterOffset, 0f, -RingCenterOffset),
            new Vector3( RingCenterOffset, 0f, -RingCenterOffset),
            new Vector3(-RingCenterOffset, 0f,  RingCenterOffset),
            new Vector3( RingCenterOffset, 0f,  RingCenterOffset),
        };

        string[] names = { "Ring_SW", "Ring_SE", "Ring_NW", "Ring_NE" };

        for (int i = 0; i < 4; i++)
            BuildRing(root.transform, names[i], centers[i]);

        // --- Bridges (edges of the 2x2) ---
        BuildBridge(root.transform, "Bridge_S", centers[0], centers[1]); // SW-SE
        BuildBridge(root.transform, "Bridge_N", centers[2], centers[3]); // NW-NE
        BuildBridge(root.transform, "Bridge_W", centers[0], centers[2]); // SW-NW
        BuildBridge(root.transform, "Bridge_E", centers[1], centers[3]); // SE-NE

        // --- Spawn on SW ring walkway ---
        float midRadius = RingOuterRadius - RingWalkWidth * 0.5f;
        var spawnPos = centers[0] + new Vector3(midRadius, FloorHeight + 0.05f, 0f);
        var spawnGo = new GameObject("Spawn_BattleRing");
        spawnGo.transform.SetParent(root.transform, false);
        spawnGo.transform.position = spawnPos;
        spawnGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        var spawn = spawnGo.AddComponent<SpawnPoint>();
        spawn.spawnId = "new_game";

        // --- Preview camera ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 28f, -18f);
        cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Game/Scenes");
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        if (!saved)
            Debug.LogError($"[BattleRing] Failed to save {ScenePath}");
        else
            Debug.Log($"[BattleRing] Saved {ScenePath}");
    }

    static void BuildRing(Transform parent, string name, Vector3 center)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.position = center;

        // Walkable hollow floor
        var floor = ShapeGenerator.GeneratePipe(
            PivotLocation.Center,
            RingOuterRadius,
            FloorHeight,
            RingWalkWidth,
            RingSides,
            1);
        floor.gameObject.name = "Floor";
        floor.transform.SetParent(group.transform, false);
        floor.transform.localPosition = new Vector3(0f, FloorHeight * 0.5f, 0f);
        FinalizePb(floor, new Color(0.35f, 0.38f, 0.42f));

        float holeRadius = Mathf.Max(0.5f, RingOuterRadius - RingWalkWidth);

        // Outer rim — don't fall off the outside
        var outerWall = ShapeGenerator.GeneratePipe(
            PivotLocation.Center,
            RingOuterRadius + WallThickness,
            WallHeight,
            WallThickness,
            RingSides,
            1);
        outerWall.gameObject.name = "Wall_Outer";
        outerWall.transform.SetParent(group.transform, false);
        outerWall.transform.localPosition = new Vector3(0f, FloorHeight + WallHeight * 0.5f, 0f);
        FinalizePb(outerWall, new Color(0.55f, 0.25f, 0.22f));

        // Inner rim — don't fall into the empty center
        var innerWall = ShapeGenerator.GeneratePipe(
            PivotLocation.Center,
            holeRadius + WallThickness,
            WallHeight,
            WallThickness,
            RingSides,
            1);
        innerWall.gameObject.name = "Wall_Inner";
        innerWall.transform.SetParent(group.transform, false);
        innerWall.transform.localPosition = new Vector3(0f, FloorHeight + WallHeight * 0.5f, 0f);
        FinalizePb(innerWall, new Color(0.55f, 0.25f, 0.22f));
    }

    static void BuildBridge(Transform parent, string name, Vector3 a, Vector3 b)
    {
        Vector3 mid = (a + b) * 0.5f;
        Vector3 delta = b - a;
        float span = delta.magnitude;
        float length = Mathf.Max(1f, span - 2f * RingOuterRadius + 2f * BridgeOverlap);

        var bridge = ShapeGenerator.GenerateCube(
            PivotLocation.Center,
            new Vector3(length, FloorHeight, BridgeWidth));
        bridge.gameObject.name = name;
        bridge.transform.SetParent(parent, false);
        bridge.transform.position = mid + Vector3.up * (FloorHeight * 0.5f);
        // Cube local Z is depth; align length along the span axis (X after yaw).
        bridge.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up)
                                    * Quaternion.Euler(0f, 90f, 0f);
        FinalizePb(bridge, new Color(0.4f, 0.42f, 0.46f));

        float railY = FloorHeight + WallHeight * 0.5f;
        Vector3 right = Vector3.Cross(Vector3.up, delta.normalized).normalized;
        for (int side = -1; side <= 1; side += 2)
        {
            var rail = ShapeGenerator.GenerateCube(
                PivotLocation.Center,
                new Vector3(length, WallHeight, WallThickness));
            rail.gameObject.name = name + (side < 0 ? "_RailL" : "_RailR");
            rail.transform.SetParent(parent, false);
            rail.transform.position = mid + right * (side * (BridgeWidth * 0.5f)) + Vector3.up * railY;
            rail.transform.rotation = bridge.transform.rotation;
            FinalizePb(rail, new Color(0.55f, 0.25f, 0.22f));
        }
    }

    static void FinalizePb(ProBuilderMesh pb, Color tint)
    {
        pb.ToMesh();
        pb.Refresh();

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader != null)
        {
            var mat = new Material(shader) { name = pb.gameObject.name + "_Mat" };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            else
                mat.color = tint;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        var col = pb.GetComponent<MeshCollider>();
        if (col == null)
            col = pb.gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = null;
        col.sharedMesh = pb.GetComponent<MeshFilter>().sharedMesh;
        col.convex = false;
    }
}
