using UnityEngine;

/// <summary>
/// 2D sprite implementation of EnemyVisuals. Iterates all SpriteRenderers in
/// this hierarchy and drives their sprite shader properties (_FlashColor,
/// _ClipThreshold, and the sprite color alpha) to render hit flashes and the
/// death fade.
///
/// Requires the sprite material to expose _FlashColor (custom sprite shader).
/// Works with the existing sprite-lit / custom sprite shaders used by the 2D
/// enemies in this project.
/// </summary>
public class EnemySpriteVisuals : EnemyVisuals
{
    [Tooltip("Shader property used for the death dissolve. Default 'ClipThreshold' " +
             "matches the sprite shader in use.")]
    public string ditherProperty = "_ClipThreshold";

    [Tooltip("Shader property used for the death alpha ramp. Optional.")]
    public string alphaProperty = "_Alpha";

    SpriteRenderer[] srs;

    void Awake()
    {
        srs = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public override void SetFlashColor(Color color)
    {
        if (srs == null) return;
        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            if (r == null) continue;
            WriteFlash(r, color);
        }
    }

    public override void SetDeathVisual(float alpha, float dither, float whiteBoost)
    {
        if (srs == null) return;
        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            if (r == null) continue;

            var col = r.color;
            col.a = alpha;
            r.color = col;

            WriteFlash(r, Color.white * whiteBoost);

            var mat = r.material;

            if (!string.IsNullOrEmpty(alphaProperty) && mat.HasProperty(alphaProperty))
                mat.SetFloat(alphaProperty, alpha);

            if (!string.IsNullOrEmpty(ditherProperty) && mat.HasProperty(ditherProperty))
                mat.SetFloat(ditherProperty, dither);
        }
    }

    static void WriteFlash(SpriteRenderer renderer, Color color)
    {
        // Prefer the lighting controller: it keeps flash on both the material
        // and the property block, so LateUpdate lighting cannot hide it.
        var lighting = renderer.GetComponent<SpriteLightingController>();
        if (lighting != null)
        {
            if (color.maxColorComponent <= 0.001f)
                lighting.ClearFlash();
            else
                lighting.TriggerFlash(color, 0f);
            return;
        }

        var mat = renderer.material;
        if (mat.HasProperty("_FlashColor"))
            mat.SetColor("_FlashColor", color);

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        if (mat.HasProperty("_FlashColor"))
            block.SetColor("_FlashColor", color);
        renderer.SetPropertyBlock(block);
    }
}
