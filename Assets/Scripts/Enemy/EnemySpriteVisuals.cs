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
            var mat = r.material;
            if (mat.HasProperty("_FlashColor"))
                mat.SetColor("_FlashColor", color);
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

            var mat = r.material;

            if (mat.HasProperty("_FlashColor"))
                mat.SetColor("_FlashColor", Color.white * whiteBoost);

            if (!string.IsNullOrEmpty(alphaProperty) && mat.HasProperty(alphaProperty))
                mat.SetFloat(alphaProperty, alpha);

            if (!string.IsNullOrEmpty(ditherProperty) && mat.HasProperty(ditherProperty))
                mat.SetFloat(ditherProperty, dither);
        }
    }
}
