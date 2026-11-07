using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Enemy gameplay clock. Stays still during hit stop so attacks, reactions,
/// and movement resume where they paused instead of skipping ahead.
/// </summary>
public static class EnemyClock
{
    static float frozenDebt;

    public static float deltaTime =>
        HitStopperManager.Frozen ? 0f : Time.deltaTime;

    public static float time => Time.time - frozenDebt;

    public static void Tick()
    {
        if (HitStopperManager.Frozen)
            frozenDebt += Time.deltaTime;
    }
}

[DefaultExecutionOrder(-1000)]
public class HitStopperManager : MonoBehaviour
{
    public static HitStopperManager Instance { get; private set; }

    public static bool Frozen => Instance != null && Instance.isStopping;

    [SerializeField] private bool allowExtend = true;

    private bool isStopping = false;
    private float stopEndRealtime = 0f;
    private float heldShaderTime;

    private List<Animator> anims = new List<Animator>();
    private List<float> savedSpeeds = new List<float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Persist across scene loads so the hit-stop pause still works after
        // the player teleports to a new scene. Requires this GameObject to be
        // at the root of its scene.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            CartoonFX.HitStopGate.Frozen = false;
        }
    }

    void Update()
    {
        EnemyClock.Tick();
        PushEffectTime();
    }

    void PushEffectTime()
    {
        if (!isStopping)
            heldShaderTime = Time.time;

        Shader.SetGlobalFloat("_CFXR_EffectTime", heldShaderTime);
        Shader.SetGlobalFloat("_CFXR_EffectTimeActive", 1f);
    }

    public void DoHitStop(float duration)
    {
        if (duration <= 0f) return;

        float now = Time.unscaledTime;
        float newEnd = now + duration;

        if (isStopping)
        {
            if (!allowExtend) return;

            if (newEnd > stopEndRealtime)
                stopEndRealtime = newEnd;

            return;
        }

        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isStopping = true;
        CartoonFX.HitStopGate.Frozen = true;
        heldShaderTime = Time.time;
        PushEffectTime();

        stopEndRealtime = Time.unscaledTime + duration;

        FreezeAnimators();

        while (Time.unscaledTime < stopEndRealtime)
            yield return null;

        UnfreezeAnimators();

        isStopping = false;
        CartoonFX.HitStopGate.Frozen = false;
    }

    void FreezeAnimators()
    {
        anims.Clear();
        savedSpeeds.Clear();

        Animator[] found = FindObjectsOfType<Animator>();

        foreach (var a in found)
        {
            anims.Add(a);
            savedSpeeds.Add(a.speed);
            a.speed = 0f;
        }
    }

    void UnfreezeAnimators()
    {
        for (int i = 0; i < anims.Count; i++)
        {
            if (anims[i])
                anims[i].speed = savedSpeeds[i];
        }

        anims.Clear();
        savedSpeeds.Clear();
    }
}