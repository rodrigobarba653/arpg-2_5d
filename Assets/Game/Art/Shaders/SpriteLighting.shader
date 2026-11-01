Shader "Game/Sprite Lighting"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _ClipThreshold ("Alpha Cutoff", Range(0,1)) = 0.15

        [Header(Master Lighting)]
        _MasterExposure ("Master Exposure", Range(0,3)) = 1
        _DirectionalResponse ("Directional Response", Range(0,3)) = 1
        _LocalLightResponse ("Point and Spot Response", Range(0,3)) = 0.65
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.35
        _LightCompression ("Light Compression", Range(0,4)) = 1
        _MaximumBrightness ("Maximum Brightness", Range(0.1,8)) = 1.5
        _NormalWrap ("Virtual Normal Wrap", Range(0,1)) = 0.35
        _ReceiveShadows ("Receive Shadows", Range(0,1)) = 1

        [Header(Body Lighting V5)]
        _BodyLightStrength ("Body Light Strength", Range(0,4)) = 1
        _BodyColorResponse ("Body Color Response", Range(0,2)) = 1
        _BodyLightWrap ("Body Light Wrap", Range(0,1)) = 0.75

        [Header(Dynamic RGB Rim)]
        [Toggle] _RimEnabled ("Enable Rim", Float) = 0
        _RimColor ("Rim Color Multiplier", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,8)) = 2
        _RimWidth ("Rim Width", Range(1,4)) = 1
        _RimExposure ("Rim Exposure", Range(0,8)) = 1
        _RimLightResponse ("Rim Light Response", Range(0,4)) = 1
        _RimDirectionalBias ("Rim Directionality", Range(0,1)) = 1
        _RimMinLight ("Rim Minimum Light", Range(0,2)) = 0.15
        _RimBodyGate ("Rim Body Gate", Range(0,2)) = 0.08

        [Header(Rim Output Limiter V10)]
        [Toggle] _RimLimiterEnabled ("Enable Rim Limiter", Float) = 1
        _RimMaxBrightness ("Rim Maximum Brightness", Range(0,16)) = 2
        _RimLimiterSoftness ("Rim Limiter Softness", Range(0,1)) = 0.25

        [Header(Vertical Fade)]
        _MasterVerticalFade ("Master Vertical Fade", Range(0,1)) = 0
        _FadeStart ("Fade Start", Range(0,1)) = 0.35
        _FadeEnd ("Fade End", Range(0,1)) = 0.65
        [Toggle] _FadeBodyLighting ("Fade Body Lighting", Float) = 0

        [Header(Orientation V6)]
        _OrientationAwareness ("Orientation Awareness", Range(0,1)) = 0
        _FrontBodyBoost ("Front Body Boost", Range(0,4)) = 1
        _BacklightRimBias ("Backlight Rim Bias", Range(0,4)) = 1
        _OrientationSoftness ("Orientation Softness", Range(0.01,1)) = 0.4
        [Toggle] _FlipVisibleNormal ("Flip Visible Normal", Float) = 0

        [Header(Sprite Facing V7)]
        _SpriteFacingAwareness ("Sprite Facing Awareness", Range(0,1)) = 0
        _ProfileBodyResponse ("Profile Body Response", Range(0,4)) = 1
        _ProfileRimSuppression ("Profile Rim Suppression", Range(0,1)) = 0.85
        _ProfileNormalTurn ("Profile Normal Turn", Range(0,1)) = 0.7
        _ProfileBlendStart ("Profile Blend Start", Range(0,1)) = 0.35
        _ProfileBlendEnd ("Profile Blend End", Range(0,1)) = 0.95

        [Header(Profile Rim Placement V9)]
        _ProfileRimPlacement ("Profile Rim Placement", Range(0,1)) = 0
        _ProfileEdgeFocus ("Profile Edge Focus", Range(0.5,6)) = 2
        _ProfileLightThreshold ("Profile Light Threshold", Range(0,1)) = 0.12
        [Toggle] _InvertProfileRim ("Invert Profile Rim", Float) = 0

        [HideInInspector] _AxFacing ("Ax Facing", Vector) = (0,-1,0,0)
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)

        [Header(Effects)]
        _FlashColor ("Flash Color", Color) = (0,0,0,0)
        _FlashStrength ("Flash Strength", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZTest LEqual

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            half4 _TintColor;
            half _ClipThreshold;

            half _MasterExposure;
            half _DirectionalResponse;
            half _LocalLightResponse;
            half _AmbientStrength;
            half _LightCompression;
            half _MaximumBrightness;
            half _NormalWrap;
            half _ReceiveShadows;

            half _BodyLightStrength;
            half _BodyColorResponse;
            half _BodyLightWrap;

            half _RimEnabled;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            half _RimExposure;
            half _RimLightResponse;
            half _RimDirectionalBias;
            half _RimMinLight;
            half _RimBodyGate;

            half _RimLimiterEnabled;
            float _RimMaxBrightness;
            float _RimLimiterSoftness;

            half _MasterVerticalFade;
            half _FadeStart;
            half _FadeEnd;
            half _FadeBodyLighting;

            half _OrientationAwareness;
            half _FrontBodyBoost;
            half _BacklightRimBias;
            half _OrientationSoftness;
            half _FlipVisibleNormal;

            half _SpriteFacingAwareness;
            half _ProfileBodyResponse;
            half _ProfileRimSuppression;
            half _ProfileNormalTurn;
            half _ProfileBlendStart;
            half _ProfileBlendEnd;

            half _ProfileRimPlacement;
            half _ProfileEdgeFocus;
            half _ProfileLightThreshold;
            half _InvertProfileRim;

            float4 _AxFacing;
            float4 _SpriteUVRect;

            half4 _FlashColor;
            half _FlashStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 uv : TEXCOORD1;
            half4 color : COLOR;
            half3 normalWS : TEXCOORD2;
            half3 rightWS : TEXCOORD3;
            half3 upWS : TEXCOORD4;
            float2 localSpriteUV : TEXCOORD5;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;

            VertexPositionInputs p =
                GetVertexPositionInputs(v.positionOS.xyz);

            o.positionCS = p.positionCS;
            o.positionWS = p.positionWS;
            o.uv = v.uv;
            o.color = v.color;

            o.rightWS = normalize(
                TransformObjectToWorldDir(float3(1,0,0)));

            o.upWS = normalize(
                TransformObjectToWorldDir(float3(0,1,0)));

            o.normalWS = normalize(
                TransformObjectToWorldDir(float3(0,0,-1)));

            float2 uvSize = max(
                _SpriteUVRect.zw - _SpriteUVRect.xy,
                float2(0.00001,0.00001));

            o.localSpriteUV =
                (v.uv - _SpriteUVRect.xy) / uvSize;

            return o;
        }

        float DissolveNoise(float2 uv)
        {
            float2 grid = floor(uv * 48.0);
            return frac(sin(dot(grid, float2(12.9898, 78.233))) * 43758.5453);
        }

        // Death drives _ClipThreshold from about 0 down to -1.
        // The living cutoff stays a plain alpha clip. Only that low range dissolves.
        void ClipDeathDither(float alpha, float2 uv)
        {
            if (_ClipThreshold >= 0.02h)
                return;

            float noise = DissolveNoise(uv);
            clip(alpha - (noise - _ClipThreshold));
        }

        half SampleAlpha(float2 uv)
        {
            float2 safeUV = clamp(
                uv,
                _SpriteUVRect.xy,
                _SpriteUVRect.zw);

            return SAMPLE_TEXTURE2D(
                _MainTex,
                sampler_MainTex,
                safeUV).a;
        }

        half GetLuminance(half3 c)
        {
            return dot(c, half3(0.2126h,0.7152h,0.0722h));
        }

        float GetRimLuminance(float3 c)
        {
            return dot(c, float3(0.2126,0.7152,0.0722));
        }

        half3 CompressLighting(half3 value)
        {
            half3 compressed = value /
                (1.0h + _LightCompression * max(value,0.0h));

            return min(compressed,_MaximumBrightness);
        }

        // V10: limit the final combined rim contribution.
        // RGB channels receive the same scale factor,
        // preserving their ratios.
        float3 ApplyRimLimiter(float3 rimRGB)
        {
            if (_RimLimiterEnabled < 0.5h)
                return rimRGB;

            float ceiling = max(_RimMaxBrightness,0.0);

            if (ceiling <= 0.0)
                return float3(0,0,0);

            float peak = max(
                rimRGB.r,
                max(rimRGB.g,rimRGB.b));

            float kneeWidth =
                ceiling * saturate(_RimLimiterSoftness);

            float kneeStart = ceiling-kneeWidth;

            // Exact pass-through below compression onset.
            if (peak <= kneeStart)
                return rimRGB;

            float limitedPeak;

            if (kneeWidth <= 0.00001)
            {
                limitedPeak = min(peak,ceiling);
            }
            else if (peak >= ceiling+kneeWidth)
            {
                limitedPeak = ceiling;
            }
            else
            {
                float t =
                    (peak-kneeStart) / (2.0*kneeWidth);

                // Smooth slope transition:
                // slope 1 at kneeStart, slope 0 at the cap.
                limitedPeak =
                    kneeStart +
                    kneeWidth * (2.0*t-t*t);
            }

            float scale = saturate(limitedPeak/peak);
            return rimRGB*scale;
        }

        half3 LightContribution(
            Light light,
            half3 normalWS,
            half response)
        {
            half ndl = dot(normalWS,light.direction);

            half diffuse = saturate(
                (ndl+_NormalWrap) / (1.0h+_NormalWrap));

            half shadow = lerp(
                1.0h,
                light.shadowAttenuation,
                _ReceiveShadows);

            return light.color *
                diffuse *
                light.distanceAttenuation *
                shadow *
                response;
        }

        half3 BodyLightContribution(
            Light light,
            half3 normalWS,
            half response)
        {
            half ndl = dot(normalWS,light.direction);

            half ordinaryDiffuse = saturate(
                (ndl+_NormalWrap) / (1.0h+_NormalWrap));

            half softDiffuse =
                saturate(ndl*0.5h+0.5h);

            half wrappedDiffuse = lerp(
                ordinaryDiffuse,
                max(0.5h,softDiffuse),
                _BodyLightWrap);

            half shadow = lerp(
                1.0h,
                light.shadowAttenuation,
                _ReceiveShadows);

            return light.color *
                wrappedDiffuse *
                light.distanceAttenuation *
                shadow *
                response *
                _BodyLightStrength;
        }

        half GetProfileFactor()
        {
            half2 facing =
                half2(_AxFacing.x,_AxFacing.y);

            half magnitude = length(facing);

            if (magnitude < 0.0001h)
                return 0.0h;

            facing /= magnitude;

            half startValue = min(
                _ProfileBlendStart,
                _ProfileBlendEnd);

            half endValue = max(
                _ProfileBlendEnd,
                startValue+0.001h);

            return smoothstep(
                startValue,
                endValue,
                abs(facing.x));
        }

        half3 GetProfileNormal(
            half3 visibleNormalWS,
            half3 rightWS)
        {
            half sideSign =
                _AxFacing.x >= 0.0 ? 1.0h : -1.0h;

            half3 sidewaysNormal =
                rightWS*sideSign;

            return normalize(lerp(
                visibleNormalWS,
                sidewaysNormal,
                min(_ProfileNormalTurn,0.999h)));
        }

        void GetOrientationWeights(
            Light light,
            half3 visibleNormalWS,
            half3 profileNormalWS,
            half profileBlend,
            half profilePlacement,
            out half bodyWeight,
            out half rimWeight)
        {
            half softness =
                max(_OrientationSoftness,0.01h);

            half v6Front = smoothstep(
                -softness,
                softness,
                dot(visibleNormalWS,light.direction));

            half v6Body = lerp(
                1.0h,
                v6Front*_FrontBodyBoost,
                _OrientationAwareness);

            half v6Rim = lerp(
                1.0h,
                (1.0h-v6Front)*_BacklightRimBias,
                _OrientationAwareness);

            half profileFront = smoothstep(
                -softness,
                softness,
                dot(profileNormalWS,light.direction));

            half profileBody =
                profileFront *
                _FrontBodyBoost *
                _ProfileBodyResponse;

            half profileRim =
                (1.0h-profileFront)*_BacklightRimBias;

            profileRim *=
                1.0h -
                saturate(_ProfileRimSuppression)*profileFront;

            bodyWeight = lerp(
                v6Body,
                profileBody,
                profileBlend);

            rimWeight = lerp(
                v6Rim,
                profileRim,
                profileBlend);

            rimWeight = lerp(
                rimWeight,
                _BacklightRimBias,
                profilePlacement);
        }

        half2 GetRimLightDirection(
            Light light,
            half3 rightWS,
            half3 upWS,
            half profilePlacement)
        {
            half2 projected = half2(
                dot(light.direction,rightWS),
                dot(light.direction,upWS));

            half projectedMagnitude = length(projected);

            half2 originalDirection =
                projected/max(projectedMagnitude,0.0001h);

            half edgeSign =
                projected.x >= 0.0h ? 1.0h : -1.0h;

            edgeSign *= lerp(
                1.0h,
                -1.0h,
                saturate(_InvertProfileRim));

            half2 profileDirection =
                half2(edgeSign,0.0h);

            half horizontalAmount = abs(projected.x);
            half threshold = saturate(_ProfileLightThreshold);

            half upperThreshold =
                min(1.0h,threshold+0.2h);

            half confidence = saturate(
                (horizontalAmount-threshold) /
                max(upperThreshold-threshold,0.001h));

            confidence =
                confidence*confidence *
                (3.0h-2.0h*confidence);

            half placementBlend =
                profilePlacement*confidence;

            half2 finalDirection = lerp(
                originalDirection,
                profileDirection,
                placementBlend);

            return finalDirection /
                max(length(finalDirection),0.0001h);
        }

        // Full precision for bright local-light accumulation.
        float3 RimContribution(
            Light light,
            half2 edgeDirection,
            half3 rightWS,
            half3 upWS,
            half profilePlacement)
        {
            half2 projected = half2(
                dot(light.direction,rightWS),
                dot(light.direction,upWS));

            if (length(projected) < 0.0001h)
                return float3(0,0,0);

            half2 lightDir = GetRimLightDirection(
                light,
                rightWS,
                upWS,
                profilePlacement);

            half directional = saturate(
                dot(edgeDirection,lightDir));

            half exponent = lerp(
                2.5h,
                0.75h,
                _RimDirectionalBias);

            exponent = lerp(
                exponent,
                _ProfileEdgeFocus,
                profilePlacement);

            directional = pow(directional,exponent);

            float shadow = lerp(
                1.0,
                (float)light.shadowAttenuation,
                (float)_ReceiveShadows);

            float3 incomingColor =
                (float3)light.color *
                light.distanceAttenuation *
                shadow *
                (float)_RimLightResponse;

            float influence =
                (float)directional *
                GetRimLuminance(incomingColor);

            float thresholded = max(
                0.0,
                influence-(float)_RimMinLight);

            if (influence <= 0.0001)
                return float3(0,0,0);

            return incomingColor *
                (float)directional *
                (thresholded/influence);
        }

        half CalculateVerticalMask(float normalizedY)
        {
            half startY = min(_FadeStart,_FadeEnd);
            half endY = max(_FadeEnd,startY+0.001h);

            half upperMask = smoothstep(
                startY,
                endY,
                saturate(normalizedY));

            return lerp(
                1.0h,
                upperMask,
                _MasterVerticalFade);
        }

        float RimSourceEnergy(
            Light light,
            half response,
            half rimWeight)
        {
            float shadow = lerp(
                1.0,
                (float)light.shadowAttenuation,
                (float)_ReceiveShadows);

            float3 incoming =
                (float3)light.color *
                light.distanceAttenuation *
                shadow;

            return GetRimLuminance(incoming) *
                (float)response *
                (float)rimWeight;
        }

        void AccumulateLight(
            Light light,
            half response,
            half3 normalWS,
            half3 visibleNormalWS,
            half3 profileNormalWS,
            half profileBlend,
            half profilePlacement,
            half2 edgeDir,
            half3 rightWS,
            half3 upWS,
            inout half3 directBodyLight,
            inout half3 originalOrientedLight,
            inout half3 enhancedBodyLight,
            inout float3 rimLighting,
            inout float rimEnergy)
        {
            half bodyWeight;
            half rimWeight;

            GetOrientationWeights(
                light,
                visibleNormalWS,
                profileNormalWS,
                profileBlend,
                profilePlacement,
                bodyWeight,
                rimWeight);

            half3 originalContribution =
                LightContribution(light,normalWS,response);

            half3 enhancedContribution =
                BodyLightContribution(light,normalWS,response);

            directBodyLight += originalContribution;

            originalOrientedLight +=
                originalContribution*bodyWeight;

            enhancedBodyLight +=
                enhancedContribution*bodyWeight;

            if (_RimEnabled > 0.5h)
            {
                rimLighting += RimContribution(
                    light,
                    edgeDir,
                    rightWS,
                    upWS,
                    profilePlacement) *
                    (float)response *
                    (float)rimWeight;

                rimEnergy += RimSourceEnergy(
                    light,
                    response,
                    rimWeight);
            }
        }

        float4 Frag(Varyings i) : SV_Target
        {
            half4 sprite = SAMPLE_TEXTURE2D(
                _MainTex,
                sampler_MainTex,
                i.uv);

            half4 baseColor =
                sprite*_TintColor*i.color;

            clip(baseColor.a-_ClipThreshold);
            ClipDeathDither(baseColor.a, i.localSpriteUV);

            half3 normalWS = normalize(i.normalWS);
            half3 rightWS = normalize(i.rightWS);
            half3 upWS = normalize(i.upWS);

            half3 visibleNormalWS =
                normalWS*lerp(1.0h,-1.0h,_FlipVisibleNormal);

            half profileFactor = GetProfileFactor();

            half profileBlend =
                profileFactor*_SpriteFacingAwareness;

            half profilePlacement =
                profileFactor*saturate(_ProfileRimPlacement);

            half3 profileNormalWS = GetProfileNormal(
                visibleNormalWS,
                rightWS);

            float4 shadowCoord =
                TransformWorldToShadowCoord(i.positionWS);

            Light mainLight = GetMainLight(shadowCoord);

            half3 ambientLight =
                SampleSH(normalWS)*_AmbientStrength;

            half3 directBodyLight = half3(0,0,0);
            half3 originalOrientedLight = half3(0,0,0);
            half3 enhancedBodyLight = half3(0,0,0);

            float3 rimLighting = float3(0,0,0);
            float rimEnergy = 0.0;

            half verticalMask =
                CalculateVerticalMask(i.localSpriteUV.y);

            half rimMask = 0.0h;
            half2 edgeDir = half2(0,0);

            if (_RimEnabled > 0.5h)
            {
                float2 stepUV =
                    _MainTex_TexelSize.xy*_RimWidth;

                half aL = SampleAlpha(
                    i.uv-float2(stepUV.x,0));

                half aR = SampleAlpha(
                    i.uv+float2(stepUV.x,0));

                half aD = SampleAlpha(
                    i.uv-float2(0,stepUV.y));

                half aU = SampleAlpha(
                    i.uv+float2(0,stepUV.y));

                half4 edges =
                    1.0h-saturate(half4(aL,aR,aD,aU));

                rimMask = saturate(
                    max(
                        max(edges.x,edges.y),
                        max(edges.z,edges.w)));

                // Correct outward silhouette direction.
                edgeDir = half2(
                    edges.y-edges.x,
                    edges.w-edges.z);

                edgeDir /= max(length(edgeDir),0.0001h);
            }

            AccumulateLight(
                mainLight,
                _DirectionalResponse,
                normalWS,
                visibleNormalWS,
                profileNormalWS,
                profileBlend,
                profilePlacement,
                edgeDir,
                rightWS,
                upWS,
                directBodyLight,
                originalOrientedLight,
                enhancedBodyLight,
                rimLighting,
                rimEnergy);

            #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;

                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(i.positionWS);

                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(i.positionCS);

                inputData.shadowCoord = shadowCoord;

                #if USE_CLUSTER_LIGHT_LOOP

                    UNITY_LOOP for (
                        uint lightIndex = 0;
                        lightIndex < min(
                            URP_FP_DIRECTIONAL_LIGHTS_COUNT,
                            MAX_VISIBLE_LIGHTS);
                        lightIndex++)
                    {
                        Light light = GetAdditionalLight(
                            lightIndex,
                            i.positionWS,
                            half4(1,1,1,1));

                        AccumulateLight(
                            light,
                            _LocalLightResponse,
                            normalWS,
                            visibleNormalWS,
                            profileNormalWS,
                            profileBlend,
                            profilePlacement,
                            edgeDir,
                            rightWS,
                            upWS,
                            directBodyLight,
                            originalOrientedLight,
                            enhancedBodyLight,
                            rimLighting,
                            rimEnergy);
                    }

                #endif

                uint lightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(lightCount)

                    Light light = GetAdditionalLight(
                        lightIndex,
                        i.positionWS,
                        half4(1,1,1,1));

                    AccumulateLight(
                        light,
                        _LocalLightResponse,
                        normalWS,
                        visibleNormalWS,
                        profileNormalWS,
                        profileBlend,
                        profilePlacement,
                        edgeDir,
                        rightWS,
                        upWS,
                        directBodyLight,
                        originalOrientedLight,
                        enhancedBodyLight,
                        rimLighting,
                        rimEnergy);

                LIGHT_LOOP_END

            #endif

            // Existing body-lighting calculations.
            half bodyLightingMask = lerp(
                1.0h,
                verticalMask,
                step(0.5h,_FadeBodyLighting));

            half3 originalLight =
                ambientLight +
                originalOrientedLight*bodyLightingMask;

            half3 newLight =
                ambientLight +
                enhancedBodyLight*bodyLightingMask;

            half3 originalExposure = CompressLighting(
                max(originalLight,0.0h)*_MasterExposure);

            half3 newExposure = CompressLighting(
                max(newLight,0.0h)*_MasterExposure);

            half bodyBlend = saturate(_BodyLightStrength);

            half3 bodyExposure = lerp(
                originalExposure,
                newExposure,
                bodyBlend);

            half3 directColor = max(enhancedBodyLight,0.0h);
            half directLuma = GetLuminance(directColor);

            half3 normalizedColor =
                directColor/max(directLuma,0.0001h);

            normalizedColor = min(
                normalizedColor,
                half3(2.5h,2.5h,2.5h));

            half colorPresence = saturate(directLuma*2.0h);

            half colorBlend = saturate(
                _BodyColorResponse*colorPresence);

            half3 colorizedExposure =
                bodyExposure*normalizedColor;

            bodyExposure = lerp(
                bodyExposure,
                colorizedExposure,
                colorBlend*bodyLightingMask);

            bodyExposure = min(
                bodyExposure,
                _MaximumBrightness);

            float3 finalRGB =
                (float3)baseColor.rgb*(float3)bodyExposure;

            if (_RimEnabled > 0.5h)
            {
                half oldBodyLuma =
                    GetLuminance(directBodyLight);

                half oldBodyGate = smoothstep(
                    _RimBodyGate,
                    _RimBodyGate+0.08h,
                    oldBodyLuma);

                float newRimGate = smoothstep(
                    (float)_RimBodyGate,
                    (float)_RimBodyGate+0.08,
                    rimEnergy);

                half gateBlend = max(
                    _OrientationAwareness,
                    profileBlend);

                float bodyGate = lerp(
                    (float)oldBodyGate,
                    newRimGate,
                    (float)gateBlend);

                float3 rimRGB =
                    rimLighting *
                    (float3)_RimColor.rgb *
                    (float)rimMask *
                    (float)verticalMask *
                    bodyGate *
                    (float)_RimStrength *
                    (float)_RimExposure;

                // Apply once, after every rim multiplier
                // and after all lights have been combined.
                finalRGB += ApplyRimLimiter(rimRGB);
            }

            // Combat writes an HDR white. Paint toward plain white instead of
            // adding it, so the hit does not bloom.
            float3 flashRGB = max((float3)_FlashColor.rgb, 0.0);
            float flashAmount = saturate(max(flashRGB.r, max(flashRGB.g, flashRGB.b)));
            if (_FlashStrength > 0.001)
                flashAmount *= saturate((float)_FlashStrength);

            finalRGB = lerp(finalRGB, float3(1.0, 1.0, 1.0), flashAmount);

            return float4(finalRGB,baseColor.a);
        }

        ENDHLSL

        Pass
        {
            Name "AX Forward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Blend One Zero

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _SHADOWS_SOFT

            ENDHLSL
        }

        Pass
        {
            Name "AX ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            ShadowVaryings ShadowVert(Attributes v)
            {
                ShadowVaryings o;

                float3 positionWS =
                    TransformObjectToWorld(v.positionOS.xyz);

                float3 normalWS = normalize(
                    TransformObjectToWorldDir(float3(0,0,-1)));

                float3 lightDirectionWS = _LightDirection;

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)

                    lightDirectionWS =
                        normalize(_LightPosition-positionWS);

                #endif

                float3 biasedPositionWS = ApplyShadowBias(
                    positionWS,
                    normalWS,
                    lightDirectionWS);

                o.positionCS =
                    TransformWorldToHClip(biasedPositionWS);

                #if UNITY_REVERSED_Z

                    o.positionCS.z = min(
                        o.positionCS.z,
                        UNITY_NEAR_CLIP_VALUE);

                #else

                    o.positionCS.z = max(
                        o.positionCS.z,
                        UNITY_NEAR_CLIP_VALUE);

                #endif

                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 ShadowFrag(ShadowVaryings i) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    i.uv).a;

                alpha *= _TintColor.a*i.color.a;
                clip(alpha-_ClipThreshold);
                ClipDeathDither(alpha, i.uv);

                return 0;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
