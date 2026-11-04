using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLightingController : MonoBehaviour
{
    [Header("TARGET")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("MASTER LIGHTING")]
    [Range(0f, 3f)] public float masterExposure = 1f;
    [Range(0f, 3f)] public float directionalResponse = 1f;
    [Range(0f, 3f)] public float pointAndSpotResponse = 0.65f;
    [Range(0f, 2f)] public float ambientStrength = 0.35f;
    [Range(0f, 4f)] public float lightCompression = 0f;
    [Range(0.1f, 8f)] public float maximumBrightness = 8f;

    [Header("SPRITE LIGHTING")]
    [Range(0f, 1f)] public float virtualNormalWrap = 0.35f;
    [Range(0f, 1f)] public float receiveShadows = 1f;

    [Header("BODY LIGHTING V5")]
    [Range(0f, 4f)] public float bodyLightStrength = 0.54f;
    [Range(0f, 2f)] public float bodyColorResponse = 0.61f;
    [Range(0f, 1f)] public float bodyLightWrap = 0.441f;

    [Header("DYNAMIC RGB RIM")]
    public bool enableRimLight = true;
    public Color rimColor = Color.white;
    [Range(0f, 8f)] public float rimStrength = 2.42f;
    [Range(1f, 4f)] public float rimWidth = 1.47f;
    [Range(0f, 8f)] public float rimExposure = 4.18f;
    [Range(0f, 4f)] public float rimLightResponse = 0.28f;
    [Range(0f, 1f)] public float rimDirectionality = 1f;
    [Range(0f, 2f)] public float rimMinimumLight = 2f;
    [Range(0f, 2f)] public float rimBodyGate = 0.65f;

    [Header("RIM OUTPUT LIMITER V10")]
    [Tooltip(
        "Limits the final combined rim after all lights, " +
        "rim color, strength and exposure.")]
    public bool enableRimLimiter = true;

    [Tooltip(
        "Maximum rim RGB channel value before post-processing. " +
        "Lower values give a more restrained rim. " +
        "0 removes the rim while the limiter is enabled.")]
    [Range(0f, 16f)]
    public float rimMaximumBrightness = 6.21f;

    [Tooltip(
        "0 = hard ceiling. Higher values begin compression earlier " +
        "and approach the ceiling more gently. " +
        "0.25 leaves values below 75% of the maximum unchanged.")]
    [Range(0f, 1f)]
    public float rimLimiterSoftness = 0.253f;

    [Header("VERTICAL FADE")]
    [Range(0f, 1f)] public float masterVerticalFade = 1f;
    [Range(0f, 1f)] public float fadeStart = 0.604f;
    [Range(0f, 1f)] public float fadeEnd = 1f;
    public bool fadeBodyLighting = false;

    [Header("ORIENTATION V6")]
    [Range(0f, 1f)] public float orientationAwareness = 1f;
    [Range(0f, 4f)] public float frontBodyBoost = 0.68f;
    [Range(0f, 4f)] public float backlightRimBias = 4f;
    [Range(0.01f, 1f)] public float orientationSoftness = 0.483f;
    public bool flipVisibleNormal = false;

    [Header("SPRITE FACING V7")]
    [Range(0f, 1f)] public float spriteFacingAwareness = 0f;
    [Range(0f, 4f)] public float profileBodyResponse = 1f;
    [Range(0f, 1f)] public float profileRimSuppression = 0.85f;
    [Range(0f, 1f)] public float profileNormalTurn = 0.7f;
    [Range(0f, 1f)] public float profileBlendStart = 0.35f;
    [Range(0f, 1f)] public float profileBlendEnd = 0.95f;

    [Header("PROFILE RIM PLACEMENT V9")]
    [Tooltip(
        "0 = corrected projected light direction. " +
        "Higher values focus profile rims horizontally. " +
        "Works independently of Sprite Facing Awareness.")]
    [Range(0f, 1f)]
    public float profileRimPlacement = 0f;

    [Tooltip("How strongly the chosen profile edge is isolated.")]
    [Range(0.5f, 6f)]
    public float profileEdgeFocus = 2f;

    [Tooltip("Minimum horizontal light direction for profile placement.")]
    [Range(0f, 1f)]
    public float profileLightThreshold = 0.12f;

    [Tooltip(
        "Reverses the horizontal profile-placement direction. " +
        "Leave off for normal light-following behavior.")]
    public bool invertProfileRim = false;

    [Header("ANIMATOR FACING SOURCE")]
    [SerializeField] private Animator facingAnimator;
    public bool automaticFacing = true;
    public Vector2 manualFacing = Vector2.down;

    [Header("FACING DEBUG")]
    [SerializeField] private Vector2 currentFacing = Vector2.down;
    [SerializeField] private float currentProfileFactor;

    [Header("SPRITE APPEARANCE")]
    public Color tintColor = new Color32(215, 255, 255, 255);
    [Range(0f, 1f)] public float alphaCutoff = 0.27f;

    [Header("FLASH EFFECT")]
    public Color flashColor = Color.white;
    [Range(0f, 1f)] public float flashStrength = 0f;

    [Header("UPDATES")]
    public bool liveUpdate = true;

    private MaterialPropertyBlock properties;
    private Sprite lastSprite;
    private Vector4 cachedUVRect = new Vector4(0, 0, 1, 1);
    private bool hasFacingParameters;

    private static readonly int MoveXID =
        Animator.StringToHash("MoveX");

    private static readonly int MoveYID =
        Animator.StringToHash("MoveY");

    private static readonly int AxFacingID =
        Shader.PropertyToID("_AxFacing");

    private static readonly int SpriteUVRectID =
        Shader.PropertyToID("_SpriteUVRect");

    private static int ID(string propertyName)
    {
        return Shader.PropertyToID(propertyName);
    }

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        facingAnimator = GetComponent<Animator>();

        Initialize();
        RefreshAnimatorParameters();
        UpdateFacing();
        ApplySettings();
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        lastSprite = null;

        RefreshAnimatorParameters();
        UpdateFacing();
        ApplySettings();
    }

    private void OnValidate()
    {
        Initialize();

        fadeEnd = Mathf.Clamp01(
            Mathf.Max(fadeEnd, fadeStart + 0.001f));

        profileBlendEnd = Mathf.Clamp01(
            Mathf.Max(
                profileBlendEnd,
                profileBlendStart + 0.001f));

        rimMaximumBrightness =
            Mathf.Clamp(rimMaximumBrightness, 0f, 16f);

        rimLimiterSoftness =
            Mathf.Clamp01(rimLimiterSoftness);

        RefreshAnimatorParameters();
        UpdateFacing();
        ApplySettings();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && !liveUpdate)
            return;

        UpdateFacing();
        ApplySettings();
    }

    private void Initialize()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (facingAnimator == null)
            facingAnimator = GetComponent<Animator>();

        if (properties == null)
            properties = new MaterialPropertyBlock();
    }

    private void RefreshAnimatorParameters()
    {
        hasFacingParameters = false;

        if (facingAnimator == null ||
            facingAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        bool foundX = false;
        bool foundY = false;

        foreach (AnimatorControllerParameter parameter
                 in facingAnimator.parameters)
        {
            if (parameter.nameHash == MoveXID &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                foundX = true;
            }

            if (parameter.nameHash == MoveYID &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                foundY = true;
            }
        }

        hasFacingParameters = foundX && foundY;
    }

    private void UpdateFacing()
    {
        Vector2 facing = manualFacing;

        if (automaticFacing && facingAnimator != null)
        {
            if (!hasFacingParameters)
                RefreshAnimatorParameters();

            if (hasFacingParameters)
            {
                facing = new Vector2(
                    facingAnimator.GetFloat(MoveXID),
                    facingAnimator.GetFloat(MoveYID));
            }
        }

        if (facing.sqrMagnitude < 0.0001f)
            facing = currentFacing;

        if (facing.sqrMagnitude < 0.0001f)
            facing = Vector2.down;

        currentFacing = facing.normalized;

        float x = Mathf.Abs(currentFacing.x);

        float start = Mathf.Min(
            profileBlendStart,
            profileBlendEnd);

        float end = Mathf.Max(
            profileBlendEnd,
            start + 0.001f);

        currentProfileFactor = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01((x - start) / (end - start)));
    }

    private void SetFloat(string name, float value)
    {
        properties.SetFloat(ID(name), value);
    }

    private void SetColor(string name, Color value)
    {
        properties.SetColor(ID(name), value);
    }

    public void ApplySettings()
    {
        Initialize();

        if (targetRenderer == null)
            return;

        Material material = targetRenderer.sharedMaterial;

        if (material == null ||
            !material.HasProperty(ID("_MasterExposure")))
        {
            return;
        }

        targetRenderer.GetPropertyBlock(properties);

        // Master lighting.
        SetFloat("_MasterExposure", masterExposure);
        SetFloat("_DirectionalResponse", directionalResponse);
        SetFloat("_LocalLightResponse", pointAndSpotResponse);
        SetFloat("_AmbientStrength", ambientStrength);
        SetFloat("_LightCompression", lightCompression);
        SetFloat("_MaximumBrightness", maximumBrightness);
        SetFloat("_NormalWrap", virtualNormalWrap);
        SetFloat("_ReceiveShadows", receiveShadows);

        // Body lighting.
        SetFloat("_BodyLightStrength", bodyLightStrength);
        SetFloat("_BodyColorResponse", bodyColorResponse);
        SetFloat("_BodyLightWrap", bodyLightWrap);

        // Rim lighting.
        SetFloat("_RimEnabled", enableRimLight ? 1f : 0f);
        SetColor("_RimColor", rimColor);
        SetFloat("_RimStrength", rimStrength);
        SetFloat("_RimWidth", rimWidth);
        SetFloat("_RimExposure", rimExposure);
        SetFloat("_RimLightResponse", rimLightResponse);
        SetFloat("_RimDirectionalBias", rimDirectionality);
        SetFloat("_RimMinLight", rimMinimumLight);
        SetFloat("_RimBodyGate", rimBodyGate);

        // Final rim output limiter.
        SetFloat("_RimLimiterEnabled", enableRimLimiter ? 1f : 0f);
        SetFloat("_RimMaxBrightness", rimMaximumBrightness);
        SetFloat("_RimLimiterSoftness", rimLimiterSoftness);

        // Vertical fade.
        SetFloat("_MasterVerticalFade", masterVerticalFade);
        SetFloat("_FadeStart", fadeStart);
        SetFloat("_FadeEnd", fadeEnd);
        SetFloat("_FadeBodyLighting", fadeBodyLighting ? 1f : 0f);

        // Orientation.
        SetFloat("_OrientationAwareness", orientationAwareness);
        SetFloat("_FrontBodyBoost", frontBodyBoost);
        SetFloat("_BacklightRimBias", backlightRimBias);
        SetFloat("_OrientationSoftness", orientationSoftness);
        SetFloat("_FlipVisibleNormal", flipVisibleNormal ? 1f : 0f);

        // Sprite facing.
        SetFloat("_SpriteFacingAwareness", spriteFacingAwareness);
        SetFloat("_ProfileBodyResponse", profileBodyResponse);
        SetFloat("_ProfileRimSuppression", profileRimSuppression);
        SetFloat("_ProfileNormalTurn", profileNormalTurn);
        SetFloat("_ProfileBlendStart", profileBlendStart);
        SetFloat("_ProfileBlendEnd", profileBlendEnd);

        // Profile rim placement.
        SetFloat("_ProfileRimPlacement", profileRimPlacement);
        SetFloat("_ProfileEdgeFocus", profileEdgeFocus);
        SetFloat("_ProfileLightThreshold", profileLightThreshold);
        SetFloat("_InvertProfileRim", invertProfileRim ? 1f : 0f);

        properties.SetVector(
            AxFacingID,
            new Vector4(
                currentFacing.x,
                currentFacing.y,
                0f,
                0f));

        // Tint, cutoff and flash stay on the material. Combat writes those
        // directly, and a property block here would erase the hit flash,
        // the red tint and the death dissolve every frame.

        UpdateSpriteUVRect();
        targetRenderer.SetPropertyBlock(properties);
    }

    private void UpdateSpriteUVRect()
    {
        Sprite sprite = targetRenderer.sprite;

        if (sprite == null)
        {
            cachedUVRect = new Vector4(0, 0, 1, 1);
            lastSprite = null;
            properties.SetVector(SpriteUVRectID, cachedUVRect);
            return;
        }

        if (sprite != lastSprite)
        {
            lastSprite = sprite;
            cachedUVRect = new Vector4(0, 0, 1, 1);

            Texture2D texture = sprite.texture;

            if (texture != null)
            {
                try
                {
                    Rect rect = sprite.textureRect;

                    float insetX = 0.5f / texture.width;
                    float insetY = 0.5f / texture.height;

                    cachedUVRect = new Vector4(
                        rect.xMin / texture.width + insetX,
                        rect.yMin / texture.height + insetY,
                        rect.xMax / texture.width - insetX,
                        rect.yMax / texture.height - insetY);
                }
                catch (UnityException)
                {
                    // Unsupported packed sprite:
                    // retain the full UV rectangle.
                }
            }
        }

        properties.SetVector(SpriteUVRectID, cachedUVRect);
    }

    public void SetRimEnabled(bool enabled)
    {
        enableRimLight = enabled;
        ApplySettings();
    }

    public void SetExposure(float value)
    {
        masterExposure = Mathf.Clamp(value, 0f, 3f);
        ApplySettings();
    }

    public void SetRimStrength(float value)
    {
        rimStrength = Mathf.Clamp(value, 0f, 8f);
        ApplySettings();
    }

    public void SetRimLimiterEnabled(bool enabled)
    {
        enableRimLimiter = enabled;
        ApplySettings();
    }

    public void SetRimMaximumBrightness(float value)
    {
        rimMaximumBrightness = Mathf.Clamp(value, 0f, 16f);
        ApplySettings();
    }

    public void SetRimLimiterSoftness(float value)
    {
        rimLimiterSoftness = Mathf.Clamp01(value);
        ApplySettings();
    }

    public void SetVerticalFade(float value)
    {
        masterVerticalFade = Mathf.Clamp01(value);
        ApplySettings();
    }

    public void SetFadeBodyLighting(bool enabled)
    {
        fadeBodyLighting = enabled;
        ApplySettings();
    }

    public void SetBodyLightStrength(float value)
    {
        bodyLightStrength = Mathf.Clamp(value, 0f, 4f);
        ApplySettings();
    }

    public void SetBodyColorResponse(float value)
    {
        bodyColorResponse = Mathf.Clamp(value, 0f, 2f);
        ApplySettings();
    }

    public void SetOrientationAwareness(float value)
    {
        orientationAwareness = Mathf.Clamp01(value);
        ApplySettings();
    }

    public void SetFrontBodyBoost(float value)
    {
        frontBodyBoost = Mathf.Clamp(value, 0f, 4f);
        ApplySettings();
    }

    public void SetBacklightRimBias(float value)
    {
        backlightRimBias = Mathf.Clamp(value, 0f, 4f);
        ApplySettings();
    }

    public void SetSpriteFacingAwareness(float value)
    {
        spriteFacingAwareness = Mathf.Clamp01(value);
        ApplySettings();
    }

    public void SetProfileBodyResponse(float value)
    {
        profileBodyResponse = Mathf.Clamp(value, 0f, 4f);
        ApplySettings();
    }

    public void SetProfileRimSuppression(float value)
    {
        profileRimSuppression = Mathf.Clamp01(value);
        ApplySettings();
    }

    public void SetProfileRimPlacement(float value)
    {
        profileRimPlacement = Mathf.Clamp01(value);
        ApplySettings();
    }

    public void TriggerFlash(Color color, float strength)
    {
        flashColor = color;
        flashStrength = Mathf.Clamp01(strength);
        WriteFlashToMaterial();
    }

    public void ClearFlash()
    {
        flashColor = Color.black;
        flashStrength = 0f;
        WriteFlashToMaterial();
    }

    void WriteFlashToMaterial()
    {
        Initialize();
        if (targetRenderer == null)
            return;

        Material material = targetRenderer.material;
        material.SetColor(ID("_FlashColor"), flashColor);
        material.SetFloat(ID("_FlashStrength"), flashStrength);

        // Property blocks override material values. Charge tint and ApplySettings
        // keep a block on the renderer, so hit flash must live there too or it
        // never shows.
        targetRenderer.GetPropertyBlock(properties);
        properties.SetColor(ID("_FlashColor"), flashColor);
        properties.SetFloat(ID("_FlashStrength"), flashStrength);
        targetRenderer.SetPropertyBlock(properties);
    }

    [ContextMenu("Apply Lighting Settings")]
    private void ApplyFromContextMenu()
    {
        UpdateFacing();
        ApplySettings();
    }
}
