using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HitStopperManager : MonoBehaviour
{
    public static HitStopperManager Instance { get; private set; }

    [SerializeField] private bool allowExtend = true;

    private bool isStopping = false;
    private float stopEndRealtime = 0f;

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

        stopEndRealtime = Time.unscaledTime + duration;

        FreezeAnimators();

        while (Time.unscaledTime < stopEndRealtime)
            yield return null;

        UnfreezeAnimators();

        isStopping = false;
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