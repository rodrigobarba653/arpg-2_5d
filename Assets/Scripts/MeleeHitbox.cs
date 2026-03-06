using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class MeleeHitbox : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int baseDamage = 10;

    [Header("Hit Stop")]
    [SerializeField] private bool enableHitStop = true;
    [SerializeField] private float hitStopStep1 = 0.04f;
    [SerializeField] private float hitStopStep2 = 0.06f;
    [SerializeField] private float hitStopStep3 = 0.08f;

    [Header("Safety")]
    [Tooltip("Auto-disable collider after X seconds (failsafe). 0 = off")]
    [SerializeField] private float autoDisableAfter = 0.20f;

    [Header("Debug")]
    [Tooltip("Print debug logs to Console")]
    [SerializeField] private bool logDebug = false;

    [Tooltip("Always show hitbox visually in Game view")]
    [SerializeField] private bool alwaysShowDebug = true;

    [SerializeField] private Color debugColor = new Color(1f, 0f, 1f, 0.25f);

    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    private float disableAtTime = -1f;

    private GameObject debugCube;
    private BoxCollider box;

    private int attackStep = 1;

    public void SetAttackStep(int step)
    {
        attackStep = Mathf.Clamp(step, 1, 99);
    }

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        if (alwaysShowDebug)
            CreateDebugCube();

        box.enabled = false;
    }

    void OnEnable()
    {
        if (logDebug) Debug.Log($"[Hitbox] ENABLED (step {attackStep})", this);

        hitEnemies.Clear();

        box.enabled = true;

        disableAtTime = (autoDisableAfter > 0f) ? Time.time + autoDisableAfter : -1f;

        UpdateDebugCubeActive();
    }

    void OnDisable()
    {
        if (box) box.enabled = false;
        disableAtTime = -1f;

        UpdateDebugCubeActive();
    }

    void Update()
    {
        if (disableAtTime > 0f && Time.time >= disableAtTime)
        {
            box.enabled = false;
            disableAtTime = -1f;

            if (logDebug) Debug.Log("[Hitbox] AUTO DISABLED collider (failsafe)", this);
            UpdateDebugCubeActive();
        }

        if (debugCube && box)
        {
            debugCube.transform.localPosition = box.center;
            debugCube.transform.localScale = box.size;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (logDebug) Debug.Log($"[Hitbox] TRIGGER with: {other.name} | step {attackStep}", this);

        var enemy = other.GetComponentInParent<EnemyHealth>();
        if (!enemy) return;

        // Only hit each enemy once per swing
        if (!hitEnemies.Add(enemy)) return;

        if (enableHitStop)
        {
            float dur =
                attackStep == 3 ? hitStopStep3 :
                attackStep == 2 ? hitStopStep2 :
                                  hitStopStep1;

            HitStopperManager.Instance?.DoHitStop(dur);
        }

        enemy.TakeDamage(baseDamage);
    }

    private void CreateDebugCube()
    {
        debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debugCube.name = "Hitbox_Debug";

        // Remove collider from debug cube
        var c = debugCube.GetComponent<Collider>();
        if (c) Destroy(c);

        debugCube.transform.SetParent(transform);
        debugCube.transform.localPosition = Vector3.zero;
        debugCube.transform.localRotation = Quaternion.identity;
        debugCube.transform.localScale = Vector3.one;

        var renderer = debugCube.GetComponent<MeshRenderer>();

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        mat.color = debugColor;
        renderer.material = mat;

        UpdateDebugCubeActive();
    }

    private void UpdateDebugCubeActive()
    {
        if (!debugCube) return;

        // Show always OR only when this object is active
        debugCube.SetActive(alwaysShowDebug || gameObject.activeSelf);
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (!col) return;

        Gizmos.color = Color.magenta;

        var old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(col.center, col.size);
        Gizmos.matrix = old;
    }
}