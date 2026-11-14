using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CorridorLampBlink : MonoBehaviour
{
    [Header("Drag the glowing strip's MeshRenderer here")]
    public Renderer emissionRenderer;
    [Min(0)] public int materialIndex = 0;

    [Header("Drag the lamp's Point Light here")]
    public Light pointLight;

    [Header("Blink")]
    [Min(0.05f)] public float onSeconds = 1f;
    [Min(0.05f)] public float offSeconds = 1f;
    [ColorUsage(true, true)] public Color emissionColor = new Color(1f, 0.06f, 0.02f, 1f);
    [Min(0f)] public float emissionBrightness = 4f;

    [Header("Sound (plays only when turning ON)")]
    public AudioClip beepClip;
    [Range(0f, 1f)] public float beepVolume = 0.25f;

    Material lampMaterial;
    AudioSource audioSource;
    float originalLightIntensity;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // positional, 3D sound

        if (pointLight != null)
            originalLightIntensity = pointLight.intensity;

        if (emissionRenderer != null)
        {
            // Once at startup: individual material, so other lamps are unaffected.
            Material[] materials = emissionRenderer.materials;
            if (materialIndex >= 0 && materialIndex < materials.Length)
            {
                lampMaterial = materials[materialIndex];
                if (lampMaterial.HasProperty("_EmissionColor"))
                    lampMaterial.EnableKeyword("_EMISSION");
                else
                {
                    Debug.LogWarning("Lamp material has no _EmissionColor property.", this);
                    lampMaterial = null;
                }
            }
        }
    }

    IEnumerator Start()
    {
        // One second ON, one second OFF, repeat.
        WaitForSeconds onWait = new WaitForSeconds(Mathf.Max(0.05f, onSeconds));
        WaitForSeconds offWait = new WaitForSeconds(Mathf.Max(0.05f, offSeconds));

        while (true)
        {
            SetLamp(true);
            yield return onWait;
            SetLamp(false);
            yield return offWait;
        }
    }

    void SetLamp(bool isOn)
    {
        if (lampMaterial != null)
            lampMaterial.SetColor("_EmissionColor",
                isOn ? emissionColor * emissionBrightness : Color.black);

        if (pointLight != null)
        {
           pointLight.enabled = isOn;
        }

        if (isOn && beepClip != null && beepVolume > 0f)
            audioSource.PlayOneShot(beepClip, beepVolume);
    }
}
