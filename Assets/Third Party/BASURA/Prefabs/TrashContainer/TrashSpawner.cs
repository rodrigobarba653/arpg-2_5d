using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class TrashSpawner : MonoBehaviour
{
    public GameObject trashPrefab;
    public float spawnRate = 3f;

    [Tooltip("Seconds before each spawned BasuraCayendo is destroyed.")]
    public float lifetime = 3f;

    [Tooltip("How long the barrel fades out at the end of its lifetime.")]
    public float fadeDuration = 0.6f;

    [Tooltip("Spawned trash passes through this collider and still hits everything else.")]
    public Collider ignoreCollider;

    [Header("Water Splash")]
    [Tooltip("Played once when a spawned barrel touches Water. Separate from the player's splash.")]
    public GameObject splashVfx;

    [Tooltip("Size of the barrel splash. 1 is the prefab's own size.")]
    public float splashScale = 0.5f;

    [Tooltip("World-space shift from the splash point on the water. Y moves it up or down.")]
    public Vector3 splashOffset = Vector3.zero;

    public float splashLifetime = 1.5f;

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    static readonly int DitherFadeId = Shader.PropertyToID("_DitherFade");

    [Tooltip("Opaque barrels ignore alpha. This dither shader is what makes the fade visible.")]
    public Shader fadeShader;

    void Start()
    {
        InvokeRepeating(nameof(SpawnTrash), 0f, spawnRate);
    }

    void SpawnTrash()
    {
        if (trashPrefab == null) return;

        var trash = Instantiate(trashPrefab, transform.position, Quaternion.identity);
        IgnoreCollider(trash);
        ArmWaterSplash(trash);
        if (lifetime > 0f)
            StartCoroutine(FadeAndDestroy(trash));
    }

    IEnumerator FadeAndDestroy(GameObject trash)
    {
        float fade = Mathf.Clamp(fadeDuration, 0f, lifetime);
        float wait = lifetime - fade;
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        if (trash == null)
            yield break;

        Renderer[] renderers = trash.GetComponentsInChildren<Renderer>();
        Material[] materials = BeginFade(renderers);

        Collider[] colliders = trash.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        float elapsed = 0f;
        while (elapsed < fade)
        {
            if (trash == null)
                yield break;

            elapsed += Time.deltaTime;
            float fadeAmount = fade <= 0f ? 1f : Mathf.Clamp01(elapsed / fade);
            SetFade(materials, fadeAmount);
            yield return null;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                Destroy(materials[i]);
        }

        if (trash != null)
            Destroy(trash);
    }

    Material[] BeginFade(Renderer[] renderers)
    {
        Shader shader = fadeShader != null ? fadeShader : Shader.Find("Custom/URP_DitherFade_Lit");
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                count++;
        }

        var materials = new Material[count];
        int index = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Material source = renderer.sharedMaterial;
            Material fadeMat = shader != null ? new Material(shader) : new Material(source);
            if (source != null)
            {
                if (source.HasProperty(BaseMapId))
                    fadeMat.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
                if (source.HasProperty(BumpMapId))
                    fadeMat.SetTexture(NormalMapId, source.GetTexture(BumpMapId));
            }

            fadeMat.SetFloat(DitherFadeId, 0f);
            renderer.material = fadeMat;
            materials[index++] = fadeMat;
        }

        return materials;
    }

    static void SetFade(Material[] materials, float fadeAmount)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                materials[i].SetFloat(DitherFadeId, fadeAmount);
        }
    }

    void ArmWaterSplash(GameObject trash)
    {
        if (splashVfx == null)
            return;

        TrashWaterSplash splash = trash.AddComponent<TrashWaterSplash>();
        splash.splashVfx = splashVfx;
        splash.splashScale = splashScale;
        splash.splashOffset = splashOffset;
        splash.splashLifetime = splashLifetime;
    }

    void IgnoreCollider(GameObject trash)
    {
        if (ignoreCollider == null)
            return;

        Collider[] trashColliders = trash.GetComponentsInChildren<Collider>();
        for (int i = 0; i < trashColliders.Length; i++)
        {
            if (trashColliders[i] != null)
                Physics.IgnoreCollision(trashColliders[i], ignoreCollider, true);
        }
    }
}

public class TrashWaterSplash : MonoBehaviour
{
    public GameObject splashVfx;
    public float splashScale = 0.5f;
    public Vector3 splashOffset;
    public float splashLifetime = 1.5f;

    bool played;

    void OnTriggerEnter(Collider other)
    {
        if (played || splashVfx == null || !other.CompareTag("Water"))
            return;

        played = true;

        Vector3 pos = transform.position;
        pos.y = other.bounds.max.y;
        pos += splashOffset;

        float scale = Mathf.Max(0.05f, splashScale);
        var holder = new GameObject("TrashSplash");
        holder.SetActive(false);
        holder.transform.SetPositionAndRotation(pos, Quaternion.identity);
        holder.transform.localScale = Vector3.one * scale;

        GameObject splash = Instantiate(splashVfx, holder.transform);
        splash.transform.localPosition = Vector3.zero;
        splash.transform.localRotation = splashVfx.transform.localRotation;

        ParticleSystem[] systems = splash.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        holder.SetActive(true);
        if (splashLifetime > 0f)
            Destroy(holder, splashLifetime);
    }
}
