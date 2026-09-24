using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

/// <summary>
/// Builds the 4-ring battle arena (ProBuilder pipes + bridges + rim walls).
/// Drop on an empty GameObject in BattleRing.unity — it auto-builds if empty.
/// Or right-click the component → Rebuild Arena.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class BattleRingArena : MonoBehaviour
{
    [Header("Ring Floor")]
    [SerializeField] float ringOuterRadius = 10f;
    [SerializeField] float ringWalkWidth = 3.5f;
    [SerializeField] float floorHeight = 0.45f;
    [SerializeField] int ringSides = 32;

    [Header("Layout")]
    [SerializeField] float ringCenterOffset = 13f;

    [Header("Safety Walls")]
    [SerializeField] float wallHeight = 2.2f;
    [SerializeField] float wallThickness = 0.35f;

    [Header("Bridges")]
    [SerializeField] float bridgeWidth = 3.2f;
    [SerializeField] float bridgeOverlap = 1.25f;

    [Header("Spawn")]
    [SerializeField] bool createSpawnPoint = true;
    [SerializeField] string spawnId = "new_game";

    [Header("Colors")]
    [SerializeField] Color floorColor = new Color(0.35f, 0.38f, 0.42f);
    [SerializeField] Color wallColor = new Color(0.55f, 0.25f, 0.22f);
    [SerializeField] Color bridgeColor = new Color(0.4f, 0.42f, 0.46f);

    bool building;

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (transform.childCount == 0)
                Rebuild();
            return;
        }

#if UNITY_EDITOR
        // Edit mode: build once when the scene opens with an empty root.
        if (transform.childCount == 0)
            UnityEditor.EditorApplication.delayCall += DeferredRebuildIfEmpty;
#endif
    }

#if UNITY_EDITOR
    void OnDisable()
    {
        UnityEditor.EditorApplication.delayCall -= DeferredRebuildIfEmpty;
    }

    void DeferredRebuildIfEmpty()
    {
        if (this == null) return;
        if (transform.childCount == 0)
            Rebuild();
    }
#endif

    [ContextMenu("Rebuild Arena")]
    public void Rebuild()
    {
        if (building) return;
        building = true;

        try
        {
            ClearChildren();
            BuildRingsAndBridges();
        }
        finally
        {
            building = false;
        }
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child);
            else
#endif
                Destroy(child);
        }
    }

    void BuildRingsAndBridges()
    {
        Vector3[] centers =
        {
            new Vector3(-ringCenterOffset, 0f, -ringCenterOffset),
            new Vector3( ringCenterOffset, 0f, -ringCenterOffset),
            new Vector3(-ringCenterOffset, 0f,  ringCenterOffset),
            new Vector3( ringCenterOffset, 0f,  ringCenterOffset),
        };

        string[] names = { "Ring_SW", "Ring_SE", "Ring_NW", "Ring_NE" };

        for (int i = 0; i < 4; i++)
            BuildRing(names[i], centers[i]);

        BuildBridge("Bridge_S", centers[0], centers[1]);
        BuildBridge("Bridge_N", centers[2], centers[3]);
        BuildBridge("Bridge_W", centers[0], centers[2]);
        BuildBridge("Bridge_E", centers[1], centers[3]);

        if (createSpawnPoint)
        {
            float midRadius = ringOuterRadius - ringWalkWidth * 0.5f;
            var spawnPos = centers[0] + new Vector3(midRadius, floorHeight + 0.05f, 0f);
            var spawnGo = new GameObject("Spawn_BattleRing");
            spawnGo.transform.SetParent(transform, false);
            spawnGo.transform.position = spawnPos;
            spawnGo.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            var spawn = spawnGo.AddComponent<SpawnPoint>();
            spawn.spawnId = spawnId;
        }
    }

    void BuildRing(string name, Vector3 center)
    {
        var group = new GameObject(name);
        group.transform.SetParent(transform, false);
        group.transform.position = center;

        var floor = ShapeGenerator.GeneratePipe(
            PivotLocation.Center, ringOuterRadius, floorHeight, ringWalkWidth, ringSides, 1);
        floor.gameObject.name = "Floor";
        floor.transform.SetParent(group.transform, false);
        floor.transform.localPosition = new Vector3(0f, floorHeight * 0.5f, 0f);
        FinalizePb(floor, floorColor);

        float holeRadius = Mathf.Max(0.5f, ringOuterRadius - ringWalkWidth);

        var outerWall = ShapeGenerator.GeneratePipe(
            PivotLocation.Center, ringOuterRadius + wallThickness, wallHeight, wallThickness, ringSides, 1);
        outerWall.gameObject.name = "Wall_Outer";
        outerWall.transform.SetParent(group.transform, false);
        outerWall.transform.localPosition = new Vector3(0f, floorHeight + wallHeight * 0.5f, 0f);
        FinalizePb(outerWall, wallColor);

        var innerWall = ShapeGenerator.GeneratePipe(
            PivotLocation.Center, holeRadius + wallThickness, wallHeight, wallThickness, ringSides, 1);
        innerWall.gameObject.name = "Wall_Inner";
        innerWall.transform.SetParent(group.transform, false);
        innerWall.transform.localPosition = new Vector3(0f, floorHeight + wallHeight * 0.5f, 0f);
        FinalizePb(innerWall, wallColor);
    }

    void BuildBridge(string name, Vector3 a, Vector3 b)
    {
        Vector3 mid = (a + b) * 0.5f;
        Vector3 delta = b - a;
        float span = delta.magnitude;
        float length = Mathf.Max(1f, span - 2f * ringOuterRadius + 2f * bridgeOverlap);

        var bridge = ShapeGenerator.GenerateCube(
            PivotLocation.Center, new Vector3(length, floorHeight, bridgeWidth));
        bridge.gameObject.name = name;
        bridge.transform.SetParent(transform, false);
        bridge.transform.position = mid + Vector3.up * (floorHeight * 0.5f);
        bridge.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up)
                                    * Quaternion.Euler(0f, 90f, 0f);
        FinalizePb(bridge, bridgeColor);

        float railY = floorHeight + wallHeight * 0.5f;
        Vector3 right = Vector3.Cross(Vector3.up, delta.normalized).normalized;
        for (int side = -1; side <= 1; side += 2)
        {
            var rail = ShapeGenerator.GenerateCube(
                PivotLocation.Center, new Vector3(length, wallHeight, wallThickness));
            rail.gameObject.name = name + (side < 0 ? "_RailL" : "_RailR");
            rail.transform.SetParent(transform, false);
            rail.transform.position = mid + right * (side * (bridgeWidth * 0.5f)) + Vector3.up * railY;
            rail.transform.rotation = bridge.transform.rotation;
            FinalizePb(rail, wallColor);
        }
    }

    void FinalizePb(ProBuilderMesh pb, Color tint)
    {
        pb.ToMesh();
        pb.Refresh();

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader != null)
        {
            var mat = new Material(shader) { name = pb.gameObject.name + "_Mat", hideFlags = HideFlags.DontSave };
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
