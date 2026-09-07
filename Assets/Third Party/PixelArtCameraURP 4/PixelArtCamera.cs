using UnityEngine;

/// <summary>
/// Camera controls for PixelArtRendererFeature.
/// Unity 6 / URP 17+ / Render Graph.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/Pixel Art Camera")]
public sealed class PixelArtCamera : MonoBehaviour
{
    [Header("Core Pixelation")]

    [Tooltip("Size of each screen-space pixel block. 1 = no block pixelation; 4-8 = obvious; 12+ = very chunky.")]
    [Range(1, 48)]
    public int pixelSize = 3;

    [Tooltip("How strongly the original image is replaced by the block sample. 0 = original interior shading; 1 = hard uniform pixel blocks. Contrast edges are still favored.")]
    [Range(0f, 1f)]
    public float interiorPixelation = 0.85f;

    [Header("Block Shape / Sampling")]

    [Tooltip("Horizontal block-size multiplier. 1 = square pixels. 2 = pixels twice as wide.")]
    [Range(0.25f, 4f)]
    public float pixelAspectX = 1f;

    [Tooltip("Vertical block-size multiplier. 1 = square pixels. 2 = pixels twice as tall.")]
    [Range(0.25f, 4f)]
    public float pixelAspectY = 1f;

    [Tooltip("How much each block uses a wider 3x3 average instead of only its center sample. 0 = hard/graphic; 1 = strongly averaged block color.")]
    [Range(0f, 1f)]
    public float sampleSmoothing = 0.15f;

    [Tooltip("Contrast applied after pixelation. 1 = neutral. Values above 1 make individual pixel blocks read much more strongly.")]
    [Range(0.25f, 2.5f)]
    public float pixelContrast = 1.15f;

    [Header("Edge / Ink")]

    [Tooltip("Dark ink added to detected color boundaries. 0 = off; 1 = very aggressive dark pixel edging.")]
    [Range(0f, 1f)]
    public float edgeDefinition = 0.18f;

    [Tooltip("How far from a pixel block the edge detector looks. Larger values produce broader, more graphic edge regions.")]
    [Range(0.5f, 4f)]
    public float edgeWidth = 1f;

    [Tooltip("Relative color difference required to count as an edge. Lower = many more edges; higher = only major silhouettes.")]
    [Range(0.01f, 0.5f)]
    public float edgeThreshold = 0.12f;

    [Header("Retro Color")]

    [Tooltip("0 = disabled. 2-8 = dramatic palette crushing; 12-24 = moderate; 32+ = subtle. Quantizes RGB while preserving HDR emission above 1.")]
    [Range(0, 64)]
    public int colorSteps = 0;

    [Tooltip("Color saturation after pixelation. 0 = grayscale, 1 = original, 2 = highly saturated.")]
    [Range(0f, 2f)]
    public float saturation = 1f;

    [Tooltip("Darkens and compresses shadows to create clearer pixel-art value groups. 0 = off; 1 = strong shadow crush.")]
    [Range(0f, 1f)]
    public float shadowCrush = 0f;

    [Tooltip("Adds stable cell-based dither before color quantization. Most visible when Color Steps is between 2 and 12.")]
    [Range(0f, 1f)]
    public float ditherStrength = 0f;

    [Tooltip("Size of the dither pattern in pixel blocks. 1 = every block can vary; larger values make coarser clusters.")]
    [Range(1f, 8f)]
    public float ditherScale = 1f;

    [Header("Layer Exclusion")]

    [Tooltip("Layers selected here are redrawn AFTER pixelation and remain crisp.")]
    public LayerMask keepCrispLayers = 0;

    [ContextMenu("Apply HD-2D Starting Preset")]
    public void ApplyHD2DPreset()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Apply HD-2D Starting Preset");
#endif

        pixelSize = 3;
        interiorPixelation = 0.85f;
        pixelAspectX = 1f;
        pixelAspectY = 1f;
        sampleSmoothing = 0.15f;
        pixelContrast = 1.15f;
        edgeDefinition = 0.18f;
        edgeWidth = 1f;
        edgeThreshold = 0.12f;
        colorSteps = 0;
        saturation = 1f;
        shadowCrush = 0f;
        ditherStrength = 0f;
        ditherScale = 1f;

        MarkDirty();
    }

    [ContextMenu("Apply Strong Pixel Art Preset")]
    public void ApplyStrongPixelPreset()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Apply Strong Pixel Art Preset");
#endif

        pixelSize = 6;
        interiorPixelation = 1f;
        pixelAspectX = 1f;
        pixelAspectY = 1f;
        sampleSmoothing = 0.35f;
        pixelContrast = 1.4f;
        edgeDefinition = 0.55f;
        edgeWidth = 1.35f;
        edgeThreshold = 0.08f;
        colorSteps = 12;
        saturation = 1.12f;
        shadowCrush = 0.18f;
        ditherStrength = 0.18f;
        ditherScale = 1f;

        MarkDirty();
    }

    [ContextMenu("Apply Extreme Retro Preset")]
    public void ApplyExtremeRetroPreset()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Apply Extreme Retro Preset");
#endif

        pixelSize = 10;
        interiorPixelation = 1f;
        pixelAspectX = 1f;
        pixelAspectY = 1f;
        sampleSmoothing = 0f;
        pixelContrast = 1.7f;
        edgeDefinition = 0.8f;
        edgeWidth = 1.75f;
        edgeThreshold = 0.055f;
        colorSteps = 6;
        saturation = 1.2f;
        shadowCrush = 0.35f;
        ditherStrength = 0.4f;
        ditherScale = 1f;

        MarkDirty();
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
    }
}
