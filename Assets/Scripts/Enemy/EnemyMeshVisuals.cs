using UnityEngine;

/// <summary>
/// 3D mesh implementation of EnemyVisuals. Iterates MeshRenderer and
/// SkinnedMeshRenderer children and drives their material color / emission
/// via MaterialPropertyBlock so the same material asset can be shared across
/// enemies without allocating instances.
///
/// Flash-on-hit boosts the emission (default: URP Lit / Standard). Death fade
/// dims the base color alpha; if the material is opaque, the mesh visibly
/// shrinks/fades until destroyed. If you want a true dissolve, use a shader
/// with an _AlphaClipThreshold property and set <see cref="dissolveProperty"/>.
/// </summary>
public class EnemyMeshVisuals : EnemyVisuals
{
    [Header("Shader Properties")]
    [Tooltip("Base color property. URP Lit uses '_BaseColor', Standard uses " +
             "'_Color'. Leave blank to skip color/alpha modulation.")]
    public string baseColorProperty = "_BaseColor";

    [Tooltip("Emission color property, boosted during flash. URP Lit uses " +
             "'_EmissionColor'. Leave blank to skip emission flash.")]
    public string emissionProperty = "_EmissionColor";

    [Tooltip("Optional dissolve / alpha-clip cutoff property. Set to " +
             "'_AlphaClipThreshold' for a shader-graph dissolve, or leave " +
             "blank to just fade base color alpha.")]
    public string dissolveProperty = "";

    [Header("Behaviour")]
    [Tooltip("If true, the mesh is hidden completely once the death fade " +
             "reaches its end (alpha < 0.02).")]
    public bool hideOnFullFade = true;

    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    Color[] originalBaseColors;
    Color[] originalEmissionColors;

    int baseColorId, emissionId, dissolveId;
    bool hasBaseColor, hasEmission, hasDissolve;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        mpb = new MaterialPropertyBlock();

        hasBaseColor = !string.IsNullOrEmpty(baseColorProperty);
        hasEmission = !string.IsNullOrEmpty(emissionProperty);
        hasDissolve = !string.IsNullOrEmpty(dissolveProperty);

        if (hasBaseColor) baseColorId = Shader.PropertyToID(baseColorProperty);
        if (hasEmission) emissionId = Shader.PropertyToID(emissionProperty);
        if (hasDissolve) dissolveId = Shader.PropertyToID(dissolveProperty);

        // Snapshot original colors so we can restore on flash clear.
        originalBaseColors = new Color[renderers.Length];
        originalEmissionColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r.sharedMaterial == null) continue;

            if (hasBaseColor && r.sharedMaterial.HasProperty(baseColorId))
                originalBaseColors[i] = r.sharedMaterial.GetColor(baseColorId);
            else
                originalBaseColors[i] = Color.white;

            if (hasEmission && r.sharedMaterial.HasProperty(emissionId))
                originalEmissionColors[i] = r.sharedMaterial.GetColor(emissionId);
            else
                originalEmissionColors[i] = Color.black;
        }
    }

    public override void SetFlashColor(Color color)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);

            if (hasEmission)
            {
                // color.maxColorComponent gives us "is this a flash or a clear"
                if (color.maxColorComponent > 0.001f)
                    mpb.SetColor(emissionId, color);
                else
                    mpb.SetColor(emissionId, originalEmissionColors[i]);
            }

            r.SetPropertyBlock(mpb);
        }
    }

    public override void SetDeathVisual(float alpha, float dither, float whiteBoost)
    {
        if (renderers == null) return;

        // If fully faded, optionally just hide.
        if (hideOnFullFade && alpha <= 0.02f)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = false;
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);

            if (hasBaseColor)
            {
                Color c = originalBaseColors[i];
                c.a = alpha;
                mpb.SetColor(baseColorId, c);
            }

            if (hasEmission && whiteBoost > 0f)
                mpb.SetColor(emissionId, Color.white * whiteBoost);

            if (hasDissolve)
                mpb.SetFloat(dissolveId, dither);

            r.SetPropertyBlock(mpb);
        }
    }
}
