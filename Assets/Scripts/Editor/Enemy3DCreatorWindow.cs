using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor window that builds a 3D enemy GameObject with all the same combat /
/// AI components as the 2D creator, but wired for a 3D mesh renderer and an
/// AnimatorController template with the parameters the top-down anim driver
/// expects.
///
/// Menu: Tools ▸ ARPG ▸ Level Design ▸ Enemy Creator 3D
///
/// The template AnimatorController can be generated from this window and
/// populated by the designer afterwards (each state exposes a Motion field —
/// drop your animation clips into those states).
/// </summary>
public class Enemy3DCreatorWindow : EditorWindow
{
    enum EnemyClass { MobileMelee, MobileRanged, FixedMelee, FixedRanged, FixedDefender }

    string enemyName = "NewEnemy3D";
    EnemyClass enemyClass = EnemyClass.MobileMelee;

    // Visuals
    RuntimeAnimatorController animatorController;
    GameObject meshPrefab;
    Vector3 meshLocalOffset = Vector3.zero;
    Vector3 meshLocalRotation = Vector3.zero;
    Vector3 meshLocalScale = Vector3.one;

    // Mesh visuals shader properties
    string baseColorProperty = "_BaseColor";
    string emissionProperty = "_EmissionColor";
    string dissolveProperty = "";

    // Stats
    int maxHP = 30;
    float moveSpeed = 3f;
    float detectDistance = 6f;
    float stopDistance = 1.5f;
    float defendDistance = 2f;

    // Combat
    float attackDistance = 1.4f;
    float attackCooldown = 1.2f;
    int meleeDamage = 10;

    // Hitbox
    bool createHitbox = true;
    Vector3 hitboxSize = new Vector3(1.2f, 1f, 1f);
    Vector3 hitboxCenter = Vector3.zero;
    float hitboxForwardDistance = 0.6f;
    float hitboxLocalHeight = 0.9f;
    float hitboxEnableDelay = 0.20f;
    float hitboxDisableDelay = 0.35f;

    // CC dimensions
    float ccHeight = 1.8f;
    float ccRadius = 0.4f;
    Vector3 ccCenter = new Vector3(0f, 0.9f, 0f);

    // Rewards
    int variumMin = 5;
    int variumMax = 15;

    // Item drops
    List<EnemyHealth.ItemDropEntry> itemDrops = new List<EnemyHealth.ItemDropEntry>();

    // Template folder
    string templateSaveFolder = "Assets/Animations/EnemyTemplates";

    Vector2 scroll;

    [MenuItem("Tools/ARPG/Level Design/Enemy Creator 3D", false, 102)]
    public static void Open()
    {
        var w = GetWindow<Enemy3DCreatorWindow>("Enemy Creator 3D");
        w.minSize = new Vector2(440, 720);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Enemy Creator 3D", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Builds a 3D enemy GameObject with mesh + animator + AI + combat.",
                                   EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Uses EnemyMeshVisuals for hit-flash and death-fade via MaterialPropertyBlock.",
                                   EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        // --- Identity ---
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        enemyName = EditorGUILayout.TextField("Name", enemyName);
        enemyClass = (EnemyClass)EditorGUILayout.EnumPopup("Class", enemyClass);
        EditorGUILayout.Space();

        // --- Animator Template ---
        EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
        animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller", animatorController, typeof(RuntimeAnimatorController), false);

        EditorGUILayout.BeginHorizontal();
        templateSaveFolder = EditorGUILayout.TextField("Template Folder", templateSaveFolder);
        if (GUILayout.Button("Generate Template", GUILayout.Width(140)))
            GenerateAnimatorTemplate();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Generate Template creates an AnimatorController asset preconfigured with the states " +
            "(Idle, Walk, Attack, Hurt, Defend, Death) and parameters (IsMoving, MoveX, MoveY, IsInCombat, " +
            "Attack, Hurt, isDefending). Drop your animation clips into each state's Motion field.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();

        // --- Mesh ---
        EditorGUILayout.LabelField("3D Mesh", EditorStyles.boldLabel);
        meshPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Mesh Prefab (FBX)", meshPrefab, typeof(GameObject), false);
        meshLocalOffset = EditorGUILayout.Vector3Field("Local Offset", meshLocalOffset);
        meshLocalRotation = EditorGUILayout.Vector3Field("Local Rotation", meshLocalRotation);
        meshLocalScale = EditorGUILayout.Vector3Field("Local Scale", meshLocalScale);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Mesh Visuals — shader props", EditorStyles.miniBoldLabel);
        baseColorProperty = EditorGUILayout.TextField("Base Color Prop", baseColorProperty);
        emissionProperty = EditorGUILayout.TextField("Emission Prop", emissionProperty);
        dissolveProperty = EditorGUILayout.TextField("Dissolve Prop (opt)", dissolveProperty);
        EditorGUILayout.LabelField(
            "URP Lit uses _BaseColor + _EmissionColor. Standard uses _Color. Dissolve is optional " +
            "and requires an _AlphaClipThreshold-style property on the material.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();

        // --- CharacterController ---
        EditorGUILayout.LabelField("CharacterController", EditorStyles.boldLabel);
        ccHeight = EditorGUILayout.FloatField("Height", ccHeight);
        ccRadius = EditorGUILayout.FloatField("Radius", ccRadius);
        ccCenter = EditorGUILayout.Vector3Field("Center", ccCenter);
        EditorGUILayout.Space();

        // --- Stats ---
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        maxHP = EditorGUILayout.IntField("Max HP", maxHP);
        moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
        detectDistance = EditorGUILayout.FloatField("Detect Distance", detectDistance);
        stopDistance = EditorGUILayout.FloatField("Stop Distance", stopDistance);
        defendDistance = EditorGUILayout.FloatField("Defend Distance", defendDistance);
        EditorGUILayout.Space();

        // --- Combat ---
        EditorGUILayout.LabelField("Combat", EditorStyles.boldLabel);
        attackDistance = EditorGUILayout.FloatField("Attack Distance", attackDistance);
        attackCooldown = EditorGUILayout.FloatField("Attack Cooldown", attackCooldown);
        if (IsMelee())
            meleeDamage = EditorGUILayout.IntField("Melee Damage", meleeDamage);
        EditorGUILayout.Space();

        // --- Hitbox ---
        if (IsMelee())
        {
            EditorGUILayout.LabelField("Melee Hitbox", EditorStyles.boldLabel);
            createHitbox = EditorGUILayout.Toggle("Create Hitbox", createHitbox);
            if (createHitbox)
            {
                hitboxSize = EditorGUILayout.Vector3Field("Size (BoxCollider)", hitboxSize);
                hitboxCenter = EditorGUILayout.Vector3Field("Center (BoxCollider)", hitboxCenter);
                hitboxForwardDistance = EditorGUILayout.FloatField("Forward Distance", hitboxForwardDistance);
                hitboxLocalHeight = EditorGUILayout.FloatField("Local Height (Y)", hitboxLocalHeight);
                hitboxEnableDelay = EditorGUILayout.FloatField("Enable Delay", hitboxEnableDelay);
                hitboxDisableDelay = EditorGUILayout.FloatField("Disable Delay", hitboxDisableDelay);
                EditorGUILayout.LabelField(
                    "Forward Distance = how far ahead of the enemy the hitbox is placed. " +
                    "Local Height = fixed Y for the hitbox (raise to reach a tall player). " +
                    "Enable/Disable Delay = timing within the attack when the hitbox is active.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space();
        }

        // --- Rewards ---
        EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
        variumMin = EditorGUILayout.IntField("Varium Min", variumMin);
        variumMax = EditorGUILayout.IntField("Varium Max", variumMax);
        EditorGUILayout.Space();

        // --- Drops ---
        EditorGUILayout.LabelField("Item Drops", EditorStyles.boldLabel);
        DrawDropList();
        EditorGUILayout.Space(12);

        // --- Validation + Create ---
        string err = Validate();
        if (!string.IsNullOrEmpty(err))
            EditorGUILayout.HelpBox(err, MessageType.Warning);

        GUI.enabled = string.IsNullOrEmpty(err);
        if (GUILayout.Button("Create 3D Enemy", GUILayout.Height(36)))
            CreateEnemy();
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    void DrawDropList()
    {
        for (int i = 0; i < itemDrops.Count; i++)
        {
            if (itemDrops[i] == null) itemDrops[i] = new EnemyHealth.ItemDropEntry();
            var d = itemDrops[i];

            EditorGUILayout.BeginHorizontal();
            d.item = (ItemDefinition)EditorGUILayout.ObjectField(d.item, typeof(ItemDefinition), false, GUILayout.Width(150));
            d.chance = EditorGUILayout.Slider(d.chance, 0f, 1f);
            d.quantity = EditorGUILayout.IntField(d.quantity, GUILayout.Width(40));
            if (GUILayout.Button("-", GUILayout.Width(22)))
            {
                itemDrops.RemoveAt(i);
                i--;
                continue;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Drop"))
            itemDrops.Add(new EnemyHealth.ItemDropEntry());
    }

    bool IsMelee() => enemyClass == EnemyClass.MobileMelee || enemyClass == EnemyClass.FixedMelee;
    bool IsRanged() => enemyClass == EnemyClass.MobileRanged || enemyClass == EnemyClass.FixedRanged;
    bool IsFixed() => enemyClass == EnemyClass.FixedMelee || enemyClass == EnemyClass.FixedRanged || enemyClass == EnemyClass.FixedDefender;

    string Validate()
    {
        if (string.IsNullOrEmpty(enemyName)) return "Name is required.";
        if (animatorController == null) return "Animator Controller is required. Generate the template or assign one.";
        return "";
    }

    // ============================================================
    // ANIMATOR TEMPLATE GENERATION
    // ============================================================
    void GenerateAnimatorTemplate()
    {
        if (!Directory.Exists(templateSaveFolder))
            Directory.CreateDirectory(templateSaveFolder);

        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{templateSaveFolder}/{enemyName}_Animator.controller");

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Parameters — these MUST match what EnemyTopDownAnimDriver / EnemyCombatController
        // / EnemyHealth / EnemyAI look for. If you rename any of these, update the
        // corresponding SetTrigger/SetBool calls in the enemy scripts.
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsInCombat", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("isDefending", AnimatorControllerParameterType.Bool);

        // States on the base layer.
        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle", new Vector3(240, 40, 0));
        var walk = sm.AddState("Walk", new Vector3(480, 40, 0));
        var attack = sm.AddState("Attack", new Vector3(480, 200, 0));
        var hurt = sm.AddState("Hurt", new Vector3(240, 200, 0));
        var defend = sm.AddState("Defend", new Vector3(0, 200, 0));

        sm.defaultState = idle;

        // Idle <-> Walk
        var idleToWalk = idle.AddTransition(walk);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.1f;
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        var walkToIdle = walk.AddTransition(idle);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.1f;
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // Any -> Attack (trigger)
        var anyToAttack = sm.AddAnyStateTransition(attack);
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.05f;
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        // Attack -> Idle (uses exit time)
        var attackToIdle = attack.AddTransition(idle);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;

        // Any -> Hurt (trigger)
        var anyToHurt = sm.AddAnyStateTransition(hurt);
        anyToHurt.hasExitTime = false;
        anyToHurt.duration = 0.05f;
        anyToHurt.AddCondition(AnimatorConditionMode.If, 0, "Hurt");

        // Hurt -> Idle (exit time)
        var hurtToIdle = hurt.AddTransition(idle);
        hurtToIdle.hasExitTime = true;
        hurtToIdle.exitTime = 0.8f;
        hurtToIdle.duration = 0.1f;

        // Idle -> Defend (bool)
        var idleToDefend = idle.AddTransition(defend);
        idleToDefend.hasExitTime = false;
        idleToDefend.duration = 0.1f;
        idleToDefend.AddCondition(AnimatorConditionMode.If, 0, "isDefending");

        // Defend -> Idle (bool false)
        var defendToIdle = defend.AddTransition(idle);
        defendToIdle.hasExitTime = false;
        defendToIdle.duration = 0.1f;
        defendToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isDefending");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        animatorController = controller;
        EditorGUIUtility.PingObject(controller);
        Selection.activeObject = controller;

        Debug.Log($"[Enemy3DCreator] Generated AnimatorController template at '{path}'. " +
                  "Drop your animation clips into each state's Motion field.");
    }

    // ============================================================
    // BUILDER
    // ============================================================
    void CreateEnemy()
    {
        var root = new GameObject(enemyName);
        Undo.RegisterCreatedObjectUndo(root, "Create 3D Enemy");

        var sv = SceneView.lastActiveSceneView;
        if (sv != null && sv.camera != null) root.transform.position = sv.pivot;

        TrySetTag(root, "Enemy");
        TrySetLayer(root, "Enemy");

        // CharacterController
        var cc = Undo.AddComponent<CharacterController>(root);
        cc.height = ccHeight;
        cc.radius = ccRadius;
        cc.center = ccCenter;

        // Motor
        var motor = Undo.AddComponent<EnemyMotor>(root);
        motor.moveSpeed = moveSpeed;
        motor.moveType = IsFixed() ? EnemyMotor.EnemyMoveType.Fixed : EnemyMotor.EnemyMoveType.Mobile;

        // Health
        var health = Undo.AddComponent<EnemyHealth>(root);
        WriteSerializedInt(health, "maxHealth", maxHP);
        WriteSerializedInt(health, "variumMin", variumMin);
        WriteSerializedInt(health, "variumMax", variumMax);
        WriteSerializedList(health, "itemDrops", itemDrops);

        // AI
        var ai = Undo.AddComponent<EnemyAI>(root);
        ai.detectDistance = detectDistance;
        ai.stopDistance = stopDistance;
        ai.defendDistance = defendDistance;

        // Combat
        EnemyCombatController meleeCombat = null;
        if (IsMelee())
        {
            meleeCombat = Undo.AddComponent<EnemyCombatController>(root);
            meleeCombat.attackDistance = attackDistance;
            meleeCombat.attackCooldown = attackCooldown;
            meleeCombat.hitboxForwardDistance = hitboxForwardDistance;
            meleeCombat.hitboxLocalHeight = hitboxLocalHeight;

            WriteSerializedFloat(meleeCombat, "hitboxEnableDelay", hitboxEnableDelay);
            WriteSerializedFloat(meleeCombat, "hitboxDisableDelay", hitboxDisableDelay);
        }
        else if (IsRanged())
        {
            var ranged = Undo.AddComponent<EnemyRangedCombatController>(root);
            ranged.attackDistance = attackDistance;
            ranged.attackCooldown = attackCooldown;
        }

        Undo.AddComponent<CharacterPushApart>(root);

        // 3D visual driver — replaces EnemySpriteVisuals for 3D meshes.
        var visuals = Undo.AddComponent<EnemyMeshVisuals>(root);
        visuals.baseColorProperty = baseColorProperty;
        visuals.emissionProperty = emissionProperty;
        visuals.dissolveProperty = dissolveProperty;

        // Mesh child (from prefab if provided, otherwise a blank container).
        GameObject meshGO;
        if (meshPrefab != null)
        {
            meshGO = (GameObject)PrefabUtility.InstantiatePrefab(meshPrefab, root.transform);
            Undo.RegisterCreatedObjectUndo(meshGO, "Mesh child");
            meshGO.name = "Mesh";
        }
        else
        {
            meshGO = new GameObject("Mesh");
            Undo.RegisterCreatedObjectUndo(meshGO, "Mesh child");
            meshGO.transform.SetParent(root.transform, worldPositionStays: false);
        }

        meshGO.transform.localPosition = meshLocalOffset;
        meshGO.transform.localEulerAngles = meshLocalRotation;
        meshGO.transform.localScale = meshLocalScale;

        // Animator — placed on the mesh so the imported avatar/rig is at the same
        // level as the animation clips. If the FBX prefab already has an Animator,
        // we reuse it; otherwise we add one.
        var animator = meshGO.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(meshGO);

        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;

        // Anim driver — same one used by the 2D enemy. Wires MoveX/MoveY/IsMoving.
        var driver = Undo.AddComponent<EnemyTopDownAnimDriver>(meshGO);
        driver.animator = animator;
        driver.motor = motor;
        driver.ai = ai;

        // Melee hitbox — child GameObject with BoxCollider (trigger) + MeleeHitbox.
        if (IsMelee() && createHitbox && meleeCombat != null)
        {
            var hitboxGO = new GameObject("MeleeHitbox");
            Undo.RegisterCreatedObjectUndo(hitboxGO, "Melee Hitbox");
            hitboxGO.transform.SetParent(root.transform, worldPositionStays: false);
            hitboxGO.transform.localPosition = new Vector3(0f, hitboxLocalHeight, hitboxForwardDistance);

            TrySetLayer(hitboxGO, "EnemyHitbox");

            var box = Undo.AddComponent<BoxCollider>(hitboxGO);
            box.isTrigger = true;
            box.size = hitboxSize;
            box.center = hitboxCenter;

            var meleeHb = Undo.AddComponent<MeleeHitbox>(hitboxGO);
            WriteSerializedInt(meleeHb, "baseDamage", meleeDamage);
            WriteSerializedBool(meleeHb, "useEquippedWeaponDamage", false);

            // Wire the hitbox into the combat controller and start it disabled.
            meleeCombat.meleeHitbox = hitboxGO;
            meleeCombat.meleeHitboxTransform = hitboxGO.transform;
            hitboxGO.SetActive(false);
        }

        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log($"[Enemy3DCreator] Created '{enemyName}'. Tune from Inspector — " +
                  "hitbox, anim events, ranged projectile, etc.");
    }

    static void TrySetTag(GameObject go, string tag)
    {
        var allTags = UnityEditorInternal.InternalEditorUtility.tags;
        for (int i = 0; i < allTags.Length; i++)
            if (allTags[i] == tag) { go.tag = tag; return; }
    }

    static void TrySetLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) go.layer = layer;
    }

    static void WriteSerializedInt(Object target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void WriteSerializedFloat(Object target, string fieldName, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void WriteSerializedBool(Object target, string fieldName, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void WriteSerializedList(Object target, string fieldName, List<EnemyHealth.ItemDropEntry> list)
    {
        if (list == null || list.Count == 0) return;

        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null || !prop.isArray) return;

        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            var el = prop.GetArrayElementAtIndex(i);
            var itemProp = el.FindPropertyRelative("item");
            var chanceProp = el.FindPropertyRelative("chance");
            var qtyProp = el.FindPropertyRelative("quantity");

            if (itemProp != null) itemProp.objectReferenceValue = list[i].item;
            if (chanceProp != null) chanceProp.floatValue = list[i].chance;
            if (qtyProp != null) qtyProp.intValue = list[i].quantity;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
