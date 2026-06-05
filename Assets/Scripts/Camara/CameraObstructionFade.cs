using UnityEngine;
using System.Collections.Generic;

public class CameraObstructionFade : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;
    public LayerMask obstructionMask;

    [Header("Height Filter")]
    public float heightOffset = 0.5f;

    [Header("Cast")]
    public float sphereRadius = 0.35f;

    [Header("URP Lit Fade")]
    [Range(0f, 1f)] public float fadeAlpha = 0.3f;

    [Header("Alpha Clip Fade")]
    [Range(0f, 1f)] public float alphaClipHiddenValue = 0.99f;

    [Header("Fade Speed")]
    public float fadeInSpeed = 4f;
    public float fadeOutSpeed = 3f;
    [Range(0f, 1f)] public float ditherMaxFade = 0.8f;

    [Header("Dither Settings")]
    public float ditherScale = 5f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int AlphaClipID = Shader.PropertyToID("_AlphaClipThreshold");
    static readonly int AlphaCutoffID = Shader.PropertyToID("_AlphaCutoff");
    static readonly int DitherFadeID = Shader.PropertyToID("_DitherFade");
    static readonly int DitherScaleID = Shader.PropertyToID("_DitherScale");

    struct MatKey
    {
        public Renderer renderer;
        public int index;

        public MatKey(Renderer r, int i)
        {
            renderer = r;
            index = i;
        }
    }

    class MatKeyComparer : IEqualityComparer<MatKey>
    {
        public bool Equals(MatKey a, MatKey b)
        {
            return a.renderer == b.renderer && a.index == b.index;
        }

        public int GetHashCode(MatKey key)
        {
            int h1 = key.renderer != null ? key.renderer.GetHashCode() : 0;
            int h2 = key.index.GetHashCode();
            return h1 ^ (h2 << 2);
        }
    }

    MaterialPropertyBlock mpb;

    HashSet<MatKey> currentHits = new HashSet<MatKey>(new MatKeyComparer());

    Dictionary<MatKey, float> originalAlphaClip = new Dictionary<MatKey, float>(new MatKeyComparer());
    Dictionary<MatKey, float> currentAlphaClip = new Dictionary<MatKey, float>(new MatKeyComparer());

    Dictionary<MatKey, Color> originalColors = new Dictionary<MatKey, Color>(new MatKeyComparer());
    Dictionary<MatKey, Color> currentColors = new Dictionary<MatKey, Color>(new MatKeyComparer());

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (!target) return;

        currentHits.Clear();

        Vector3 dir = target.position - transform.position;
        float dist = dir.magnitude;

        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            sphereRadius,
            dir.normalized,
            dist,
            obstructionMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            // 🔥 ignorar cosas debajo
            if (hit.collider.bounds.max.y < target.position.y - heightOffset)
                continue;

            // 🔥 IGNORAR SUELO
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.6f)
                continue;

            Renderer[] renderers = hit.collider.GetComponentsInChildren<Renderer>();

            foreach (Renderer r in renderers)
            {
                if (!r) continue;
                FadeRenderer(r);
            }
        }

        RestoreRenderers();
    }

    void FadeRenderer(Renderer r)
    {
        Material[] mats = r.sharedMaterials;

        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = mats[i];
            if (!mat) continue;

            MatKey key = new MatKey(r, i);
            currentHits.Add(key);

            bool hasAlphaClip = mat.HasProperty(AlphaClipID) || mat.HasProperty(AlphaCutoffID);

            r.GetPropertyBlock(mpb, i);

            if (hasAlphaClip)
            {
                if (!originalAlphaClip.ContainsKey(key))
                {
                    float original = GetAlphaClipValue(mat);
                    originalAlphaClip[key] = original;
                    currentAlphaClip[key] = original;
                }

                currentAlphaClip[key] = Mathf.Lerp(
                    currentAlphaClip[key],
                    alphaClipHiddenValue,
                    Time.deltaTime * fadeInSpeed
                );

                SetAlphaClipOnBlock(mat, currentAlphaClip[key]);
                r.SetPropertyBlock(mpb, i);
            }
            // 🎭 DITHER FADE (nuevo)
            else if (mat.HasProperty(DitherFadeID))
            {
                if (!originalColors.ContainsKey(key))
                {
                    originalColors[key] = Color.black;
                    currentColors[key] = Color.black;
                }

                float current = currentColors[key].r;

                current = Mathf.Lerp(current, ditherMaxFade, Time.deltaTime * fadeInSpeed);

                currentColors[key] = new Color(current, current, current, current);

                mpb.SetFloat(DitherFadeID, current);
                mpb.SetFloat(DitherScaleID, ditherScale);
                

                r.SetPropertyBlock(mpb, i);
            }

            else if (mat.HasProperty(BaseColorID))
            {
                if (!originalColors.ContainsKey(key))
                {
                    Color original = mat.GetColor(BaseColorID);
                    originalColors[key] = original;
                    currentColors[key] = original;
                }

                Color c = currentColors[key];

                // 🔥 en vez de alpha → OSCURECER
                Color target = originalColors[key] * 0.3f;

                c = Color.Lerp(c, target, Time.deltaTime * fadeInSpeed);

                currentColors[key] = c;

                mpb.SetColor(BaseColorID, c);
                r.SetPropertyBlock(mpb, i);
            }
        }
    }

    void RestoreRenderers()
    {
        List<MatKey> removeAlpha = new List<MatKey>();
        List<MatKey> removeColor = new List<MatKey>();

        // =========================
        // RESTORE ALPHA CLIP
        // =========================
        foreach (var pair in originalAlphaClip)
        {
            MatKey key = pair.Key;

            if (key.renderer == null)
            {
                removeAlpha.Add(key);
                continue;
            }

            if (currentHits.Contains(key))
                continue;

            Material[] mats = key.renderer.sharedMaterials;

            if (key.index < 0 || key.index >= mats.Length || mats[key.index] == null)
            {
                removeAlpha.Add(key);
                continue;
            }

            Material mat = mats[key.index];

            currentAlphaClip[key] = Mathf.Lerp(
                currentAlphaClip[key],
                pair.Value,
                Time.deltaTime * fadeOutSpeed
            );

            key.renderer.GetPropertyBlock(mpb, key.index);
            SetAlphaClipOnBlock(mat, currentAlphaClip[key]);
            key.renderer.SetPropertyBlock(mpb, key.index);

            if (Mathf.Abs(currentAlphaClip[key] - pair.Value) < 0.01f)
            {
                key.renderer.GetPropertyBlock(mpb, key.index);
                SetAlphaClipOnBlock(mat, pair.Value);
                key.renderer.SetPropertyBlock(mpb, key.index);

                removeAlpha.Add(key);
            }
        }

        // =========================
        // RESTORE DITHER / BASE COLOR
        // =========================
        foreach (var pair in originalColors)
        {
            MatKey key = pair.Key;

            if (key.renderer == null)
            {
                removeColor.Add(key);
                continue;
            }

            if (currentHits.Contains(key))
                continue;

            Material[] mats = key.renderer.sharedMaterials;

            if (key.index < 0 || key.index >= mats.Length || mats[key.index] == null)
            {
                removeColor.Add(key);
                continue;
            }

            Material mat = mats[key.index];

            bool hasDither = mat.HasProperty(DitherFadeID);
            bool hasBaseColor = mat.HasProperty(BaseColorID);

            if (!hasDither && !hasBaseColor)
            {
                removeColor.Add(key);
                continue;
            }

            // 🎭 RESTORE DITHER
            if (hasDither)
            {
                float current = currentColors[key].r;

                current = Mathf.Lerp(
                    current,
                    0f,
                    Time.deltaTime * fadeOutSpeed
                );

                currentColors[key] = new Color(current, current, current, current);

                key.renderer.GetPropertyBlock(mpb, key.index);
                mpb.SetFloat(DitherFadeID, current);
                key.renderer.SetPropertyBlock(mpb, key.index);

                if (Mathf.Abs(current) < 0.01f)
                {
                    key.renderer.GetPropertyBlock(mpb, key.index);
                    mpb.SetFloat(DitherFadeID, 0f);
                    key.renderer.SetPropertyBlock(mpb, key.index);

                    removeColor.Add(key);
                }

                continue;
            }

            // 🧱 RESTORE BASE COLOR
            if (hasBaseColor)
            {
                Color c = currentColors[key];

                c = Color.Lerp(
                    c,
                    pair.Value,
                    Time.deltaTime * fadeOutSpeed
                );

                currentColors[key] = c;

                key.renderer.GetPropertyBlock(mpb, key.index);
                mpb.SetColor(BaseColorID, c);
                key.renderer.SetPropertyBlock(mpb, key.index);

                float colorDistance =
                    Mathf.Abs(c.r - pair.Value.r) +
                    Mathf.Abs(c.g - pair.Value.g) +
                    Mathf.Abs(c.b - pair.Value.b) +
                    Mathf.Abs(c.a - pair.Value.a);

                if (colorDistance < 0.03f)
                {
                    key.renderer.GetPropertyBlock(mpb, key.index);
                    mpb.SetColor(BaseColorID, pair.Value);
                    key.renderer.SetPropertyBlock(mpb, key.index);

                    removeColor.Add(key);
                }
            }
        }

        foreach (MatKey key in removeAlpha)
        {
            originalAlphaClip.Remove(key);
            currentAlphaClip.Remove(key);
        }

        foreach (MatKey key in removeColor)
        {
            originalColors.Remove(key);
            currentColors.Remove(key);
        }
    }

    float GetAlphaClipValue(Material mat)
    {
        if (mat.HasProperty(AlphaClipID))
            return mat.GetFloat(AlphaClipID);

        if (mat.HasProperty(AlphaCutoffID))
            return mat.GetFloat(AlphaCutoffID);

        return 0f;
    }

    void SetAlphaClipOnBlock(Material mat, float value)
    {
        if (mat.HasProperty(AlphaClipID))
            mpb.SetFloat(AlphaClipID, value);

        if (mat.HasProperty(AlphaCutoffID))
            mpb.SetFloat(AlphaCutoffID, value);
    }
}