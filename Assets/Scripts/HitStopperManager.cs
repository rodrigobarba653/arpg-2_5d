using UnityEngine;
using System.Collections;

public class HitStopperManager : MonoBehaviour
{
    public static HitStopperManager Instance { get; private set; }

    [Tooltip("If true, another HitStopper can extend the current one (keeps the longest remaining).")]
    [SerializeField] private bool allowExtend = true;

    private float savedTimeScale = 1f;
    private bool isStopping = false;
    private float stopEndRealtime = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Optional across scenes:
        // DontDestroyOnLoad(gameObject);
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

        savedTimeScale = Time.timeScale;
        stopEndRealtime = Time.unscaledTime + duration;

        Time.timeScale = 0f;

        while (Time.unscaledTime < stopEndRealtime)
            yield return null;

        Time.timeScale = savedTimeScale;

        isStopping = false;
    }

    private void OnDisable()
    {
        if (isStopping)
            Time.timeScale = savedTimeScale;
    }

    private void OnDestroy()
    {
        if (isStopping)
            Time.timeScale = savedTimeScale;
    }
}