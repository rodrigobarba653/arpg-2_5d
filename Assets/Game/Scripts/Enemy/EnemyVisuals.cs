using UnityEngine;

/// <summary>
/// Base for whatever component drives an enemy's damage/death visuals.
/// EnemyHealth delegates flash-on-hit and death-fade to this so the same
/// combat logic works for both 2D sprite enemies and 3D mesh enemies.
///
/// Subclasses:
///   - <see cref="EnemySpriteVisuals"/> — flashes SpriteRenderers via
///     the sprite shader's _FlashColor / _ClipThreshold / _Alpha props.
///   - <see cref="EnemyMeshVisuals"/> — flashes 3D renderers via
///     MaterialPropertyBlock (emission boost + optional dissolve).
///
/// EnemyHealth auto-adds EnemySpriteVisuals in Awake if no EnemyVisuals is
/// present, so existing 2D enemy prefabs keep working with no changes.
/// </summary>
public abstract class EnemyVisuals : MonoBehaviour
{
    /// <summary>
    /// Per-frame flash color override. Passed Color.black to reset.
    /// EnemyHealth calls this with white*intensity on hit, gray*2 on block,
    /// and black to clear.
    /// </summary>
    public abstract void SetFlashColor(Color color);

    /// <summary>
    /// Applied every death-fade frame.
    ///   alpha: 0-1 opacity of the enemy.
    ///   dither: shader dissolve threshold — sprites use _ClipThreshold; meshes
    ///           can map this to an alpha-clip cutoff (0 = fully visible, negative
    ///           = fully dissolved). Ignore if the visual style doesn't dissolve.
    ///   whiteBoost: intensity of the death white flash.
    /// </summary>
    public abstract void SetDeathVisual(float alpha, float dither, float whiteBoost);
}
