using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that builds an enemy GameObject with all the components,
/// child sprite, animator, stats, and drops set up.
///
/// Designer needs to provide BEFORE using:
///   1. An Animator Controller (or AnimatorOverrideController) with the
///      blend trees / states the EnemyTopDownAnimDriver expects.
///   2. (optional) A default sprite for the SpriteRenderer initial frame.
/// </summary>
public class EnemyCreatorWindow : EditorWindow
{
    enum EnemyClass { MobileMelee, MobileRanged, FixedMelee, FixedRanged, FixedDefender }

    string enemyName = "NewEnemy";
    EnemyClass enemyClass = EnemyClass.MobileMelee;

    // Visuals
    RuntimeAnimatorController animatorController;
    Sprite defaultSprite;
    string spriteLayerName = "Default";
    int spriteOrderInLayer = 0;
    Vector3 spriteOffset = new Vector3(0f, 0.75f, 0f);

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

    // Rewards
    int variumMin = 5;
    int variumMax = 15;

    // Item drops (set up in the window)
    List<EnemyHealth.ItemDropEntry> itemDrops = new List<EnemyHealth.ItemDropEntry>();

    Vector2 scroll;

    [MenuItem("Tools/ARPG/Level Design/Enemy Creator", false, 101)]
    public static void Open()
    {
        var w = GetWindow<EnemyCreatorWindow>("Enemy Creator");
        w.minSize = new Vector2(420, 600);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Enemy Creator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Builds an enemy GameObject with sprite + animator + AI + combat.",
                                   EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        // --- Identity ---
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        enemyName = EditorGUILayout.TextField("Name", enemyName);
        enemyClass = (EnemyClass)EditorGUILayout.EnumPopup("Class", enemyClass);
        EditorGUILayout.Space();

        // --- Visuals ---
        EditorGUILayout.LabelField("Sprite & Animator", EditorStyles.boldLabel);
        animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller", animatorController, typeof(RuntimeAnimatorController), false);
        defaultSprite = (Sprite)EditorGUILayout.ObjectField(
            "Default Sprite", defaultSprite, typeof(Sprite), false);
        spriteLayerName = EditorGUILayout.TextField("Sprite Sorting Layer", spriteLayerName);
        spriteOrderInLayer = EditorGUILayout.IntField("Order In Layer", spriteOrderInLayer);
        spriteOffset = EditorGUILayout.Vector3Field("Sprite Local Offset", spriteOffset);
        EditorGUILayout.LabelField("Animator Controller is required. Clone a template if you have one.",
                                   EditorStyles.miniLabel);
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
        if (GUILayout.Button("Create Enemy", GUILayout.Height(36)))
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
            if (GUILayout.Button("−", GUILayout.Width(22)))
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
        if (animatorController == null) return "Animator Controller is required.";
        return "";
    }

    // ============================================================
    // BUILDER
    // ============================================================
    void CreateEnemy()
    {
        var root = new GameObject(enemyName);
        Undo.RegisterCreatedObjectUndo(root, "Create Enemy");

        // Position at scene view camera pivot
        var sv = SceneView.lastActiveSceneView;
        if (sv != null && sv.camera != null) root.transform.position = sv.pivot;

        TrySetTag(root, "Enemy");
        TrySetLayer(root, "Enemy");

        // CharacterController
        var cc = Undo.AddComponent<CharacterController>(root);
        cc.height = 1.5f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.75f, 0f);

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
        if (IsMelee())
        {
            var melee = Undo.AddComponent<EnemyCombatController>(root);
            melee.attackDistance = attackDistance;
            melee.attackCooldown = attackCooldown;
        }
        else if (IsRanged())
        {
            var ranged = Undo.AddComponent<EnemyRangedCombatController>(root);
            ranged.attackDistance = attackDistance;
            ranged.attackCooldown = attackCooldown;
        }

        // Push apart (so the enemy interacts properly with the player and others)
        Undo.AddComponent<CharacterPushApart>(root);

        // Sprite child
        var spriteGO = new GameObject("Sprite");
        Undo.RegisterCreatedObjectUndo(spriteGO, "Sprite child");
        spriteGO.transform.SetParent(root.transform, worldPositionStays: false);
        spriteGO.transform.localPosition = spriteOffset;

        var sr = Undo.AddComponent<SpriteRenderer>(spriteGO);
        sr.sprite = defaultSprite;
        sr.sortingLayerName = spriteLayerName;
        sr.sortingOrder = spriteOrderInLayer;

        var animator = Undo.AddComponent<Animator>(spriteGO);
        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;

        Undo.AddComponent<EnemyTopDownAnimDriver>(spriteGO);

        // Auto-fix the hitbox damage = meleeDamage (only for melee).
        // The MeleeHitbox lives on a child; we don't auto-create one here, the
        // designer can drop one later from the animation event chain if needed.

        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log($"[EnemyCreator] Created '{enemyName}'. Tune from Inspector — " +
                  $"hitbox, anim events, defense, ranged projectile, etc.");
    }

    static void TrySetTag(GameObject go, string tag)
    {
#if UNITY_EDITOR
        // Only set if the tag actually exists in TagManager
        var allTags = UnityEditorInternal.InternalEditorUtility.tags;
        for (int i = 0; i < allTags.Length; i++)
            if (allTags[i] == tag) { go.tag = tag; return; }
#endif
    }

    static void TrySetLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) go.layer = layer;
    }

    // ============================================================
    // Helpers to write into private [SerializeField] fields via SerializedObject
    // ============================================================
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
