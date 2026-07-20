using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CameraShakeCinemachine : MonoBehaviour
{
    [SerializeField] private CinemachineCamera vcam;

    private CinemachineBasicMultiChannelPerlin noise;
    private Coroutine shakeRoutine;

    void Awake()
    {
        if (!vcam)
            vcam = GetComponent<CinemachineCamera>();

        noise = vcam.GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    public void Shake(float duration, float amplitude, float frequency = 2f)
    {
        if (noise == null)
        {
            Debug.LogWarning("Missing Perlin Noise component!");
            return;
        }

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, amplitude, frequency));
    }

    IEnumerator ShakeRoutine(float duration, float amplitude, float frequency)
    {
        noise.AmplitudeGain = amplitude;
        noise.FrequencyGain = frequency;

        yield return new WaitForSecondsRealtime(duration);

        noise.AmplitudeGain = 0f;
        noise.FrequencyGain = 0f;

        shakeRoutine = null;
    }
}