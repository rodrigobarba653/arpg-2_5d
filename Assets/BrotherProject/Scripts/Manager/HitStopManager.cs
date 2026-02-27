using UnityEngine;
using System.Collections;

public class HitStopManager : MonoBehaviour
{
    bool isStopping = false;

    public void DoHitStop(float duration)
    {
        if (!isStopping)
            StartCoroutine(HitStop(duration));
    }

    IEnumerator HitStop(float duration)
    {
        isStopping = true;

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;

        isStopping = false;
    }
}
