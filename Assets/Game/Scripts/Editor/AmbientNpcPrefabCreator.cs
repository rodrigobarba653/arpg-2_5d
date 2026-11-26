using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds NPC_Ambient prefab: Soldier visual skin + ScriptedNpcMotor + AmbientNpc
/// + NpcDialogueHost. No EnemyAI / combat.
/// Menu: Tools/ARPG/Level Design/Create Ambient NPC Prefab
/// </summary>
public static class AmbientNpcPrefabCreator
{
    const string SoldierPrefabPath = "Assets/Game/Prefabs/Enemy/Soldier-Shooter.prefab";
    const string OutputFolder = "Assets/Game/Prefabs/NPC";
    const string OutputPrefabPath = OutputFolder + "/NPC_Ambient.prefab";
    const string AmbientDialoguePath = "Assets/Game/Dialogue/TestLab/Lab_Ambient_A.asset";
    const string InteractiveDialoguePath = "Assets/Game/Dialogue/TestLab/Lab_Both_Interactive.asset";

    [MenuItem("Tools/ARPG/Level Design/Create Ambient NPC Prefab", false, 121)]
    public static void CreatePrefabMenu()
    {
        var prefab = BuildPrefabAsset();
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }

    public static GameObject BuildPrefabAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs/NPC"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Game", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Game/Prefabs", "NPC");
        }

        var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefabPath);
        if (soldier == null)
        {
            Debug.LogError("[AmbientNpc] Missing " + SoldierPrefabPath);
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(soldier);
        PrefabUtility.UnpackPrefabInstance(
            instance,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);

        instance.name = "NPC_Ambient";
        instance.tag = "Untagged";
        // Keep layer as-is or move off Enemy if tagged layer 7 — use Default.
        instance.layer = 0;

        StripCombatStack(instance);
        KeepVisualSkin(instance);

        var cc = instance.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = instance.AddComponent<CharacterController>();
            cc.height = 0.5f;
            cc.radius = 0.14f;
            cc.center = new Vector3(0f, 0.5f, 0f);
            cc.skinWidth = 0.08f;
        }

        // Remove NavMeshAgent — narrative NPC walks straight-line.
        var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            Object.DestroyImmediate(agent);

        var spriteBody = instance.transform.Find("SpriteBody");
        Animator spriteAnimator = spriteBody != null
            ? spriteBody.GetComponent<Animator>()
            : instance.GetComponentInChildren<Animator>(true);

        // EnemyTopDownAnimDriver needs EnemyMotor — remove so ScriptedNpcMotor owns params.
        if (spriteBody != null)
        {
            foreach (var mb in spriteBody.GetComponents<MonoBehaviour>())
            {
                if (mb == null)
                    continue;
                string n = mb.GetType().Name;
                if (n == "EnemyTopDownAnimDriver" || n == "CombatAnimEventsRelay")
                    Object.DestroyImmediate(mb);
            }
        }

        var motor = instance.GetComponent<ScriptedNpcMotor>();
        if (motor == null)
            motor = instance.AddComponent<ScriptedNpcMotor>();
        motor.controller = cc;
        motor.animator = spriteAnimator;
        motor.moveSpeed = 3.2f;

        var ambient = instance.GetComponent<AmbientNpc>();
        if (ambient == null)
            ambient = instance.AddComponent<AmbientNpc>();
        ambient.motor = motor;
        ambient.patrolSpeed = 2f;
        ambient.waitAtWaypoint = 1.25f;
        ambient.arriveDistance = 0.4f;
        ambient.suspendDialogueWhileSeized = true;

        var host = instance.GetComponent<NpcDialogueHost>();
        if (host == null)
            host = instance.AddComponent<NpcDialogueHost>();
        host.mode = NpcDialogueMode.Both;
        host.ambientDialogue = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AmbientDialoguePath);
        host.interactiveDialogue = AssetDatabase.LoadAssetAtPath<DialogueAsset>(InteractiveDialoguePath);
        host.ambientProximityRadius = 3.5f;
        host.ambientRepeat = NpcDialogueHost.AmbientRepeatMode.Repeat;
        host.discoveryRadius = 4.5f;
        host.interactionRadius = 2.5f;
        host.promptMode = InteractPromptMode.InteractionOnly;
        host.npcSpeakerId = "npc_ambient";
        ambient.dialogue = host;

        // Default route children (local offsets) — designers rewire in scene.
        var route = instance.transform.Find("PatrolRoute");
        if (route == null)
        {
            var routeGo = new GameObject("PatrolRoute");
            routeGo.transform.SetParent(instance.transform, false);
            route = routeGo.transform;
        }

        Vector3[] offsets =
        {
            new Vector3( 2f, 0f,  0f),
            new Vector3( 0f, 0f,  2f),
            new Vector3(-2f, 0f,  0f),
            new Vector3( 0f, 0f, -2f),
        };

        var wps = new Transform[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            Transform existing = route.Find($"WP_{i}");
            GameObject wpGo;
            if (existing != null)
            {
                wpGo = existing.gameObject;
            }
            else
            {
                wpGo = new GameObject($"WP_{i}");
                wpGo.transform.SetParent(route, false);
            }

            wpGo.transform.localPosition = offsets[i];
            wps[i] = wpGo.transform;
        }

        ambient.waypoints = wps;

        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, OutputPrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        Debug.Log("[AmbientNpc] Prefab saved → " + OutputPrefabPath);
        return prefab;
    }

    static void StripCombatStack(GameObject root)
    {
        // Destroy known combat / AI children.
        DestroyNamedChild(root.transform, "MeleeHitbox");
        DestroyNamedChild(root.transform, "Hurtbox");
        DestroyNamedChild(root.transform, "ShootPoint");
        DestroyNamedChild(root.transform, "EnemyDetection");

        // Remove all root MonoBehaviours except CharacterController (not MB).
        var behaviours = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                Object.DestroyImmediate(behaviours[i]);
        }

        // CharacterController / Rigidbody / Capsule stay; strip non-trigger extras later.
        var rb = root.GetComponent<Rigidbody>();
        if (rb != null)
            Object.DestroyImmediate(rb);
    }

    static void KeepVisualSkin(GameObject root)
    {
        // SpriteBody + FeetBody stay. Scale often 2.5 on Soldier — keep.
        var t = root.transform;
        // Ensure SpriteBody exists.
        if (t.Find("SpriteBody") == null)
            Debug.LogWarning("[AmbientNpc] Soldier prefab had no SpriteBody — visual may be empty.");
    }

    static void DestroyNamedChild(Transform root, string childName)
    {
        var child = root.Find(childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }
}
