Shader "Custom/URP_SpriteLit"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor]   _Color   ("Tint", Color) = (1,1,1,1)

        [Header(Lighting)]
        _LightingStrength ("Lighting Strength", Range(0,2)) = 1.0
        _AmbientStrength  ("Ambient Strength",  Range(0,2)) = 1.0
        _MinLight         ("Min Light (floor)", Range(0,1)) = 0.15
        _Smoothness       ("Smoothness",        Range(0,1)) = 0.0
        _Metallic         ("Metallic",          Range(0,1)) = 0.0
        [Toggle(_USE_UP_NORMAL)] _UseUpNormal ("Use Up Normal (top-down sprite)", Float) = 1

        [Header(Flash)]
        _FlashColor ("Flash Color", Color) = (0,0,0,0)

        [Header(Dither Fade)]
        _DitherFade  ("Dither Fade", Range(0,1)) = 0
        _DitherScale ("Dither Scale", Float) = 1

        [Header(Alpha)]
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // Main light + shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            // Additional lights
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // Forward+ rendering path (CRITICAL for URP 13+)
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // Soft shadows + reflection probes + light cookies + layers
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS

            #pragma shader_feature_local _USE_UP_NORMAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                float4 color      : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _FlashColor;
                float  _LightingStrength;
                float  _AmbientStrength;
                float  _MinLight;
                float  _Smoothness;
                float  _Metallic;
                float  _DitherFade;
                float  _DitherScale;
                float  _AlphaCutoff;
            CBUFFER_END

            float Dither4x4(float2 pixelPos)
            {
                int x = (int)pixelPos.x & 3;
                int y = (int)pixelPos.y & 3;
                int idx = x + y * 4;

                float dither[16] =
                {
                    0.0,    0.5,    0.125,  0.625,
                    0.75,   0.25,   0.875,  0.375,
                    0.1875, 0.6875, 0.0625, 0.5625,
                    0.9375, 0.4375, 0.8125, 0.3125
                };

                return dither[idx];
            }

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos  = ComputeScreenPos(o.positionCS);
                o.color      = v.color;

                #ifdef _USE_UP_NORMAL
                    // For top-down 2.5D, treat the sprite as if its surface faces up.
                    // This makes overhead directional lights actually illuminate it.
                    o.normalWS = float3(0, 1, 0);
                #else
                    o.normalWS = normalize(normInputs.normalWS);
                #endif

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 baseCol = tex * _Color * i.color;

                // Alpha cutoff – discard fully transparent pixels (sprite mask).
                if (baseCol.a < _AlphaCutoff)
                    discard;

                // Dither fade – discard pixels based on screen-space dither pattern.
                if (_DitherFade > 0.001)
                {
                    float2 pixelPos = i.screenPos.xy / i.screenPos.w;
                    pixelPos *= _ScreenParams.xy * _DitherScale;

                    float noise = Dither4x4(pixelPos);
                    if (noise < _DitherFade)
                        discard;
                }

                float3 normalWS  = normalize(i.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));

                // BRDF setup
                BRDFData brdfData;
                InitializeBRDFData(
                    baseCol.rgb,
                    _Metallic,
                    half3(0.04, 0.04, 0.04),
                    _Smoothness,
                    baseCol.a,
                    brdfData
                );

                // ----- Environment / ambient -----
                half3 bakedGI = SampleSH(normalWS) * _AmbientStrength;
                half3 color = GlobalIllumination(brdfData, bakedGI, 1.0, normalWS, viewDirWS);

                // ----- Main directional light -----
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                color += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirWS)
                         * _LightingStrength;

                // ----- Additional lights (Forward + Forward+ compatible) -----
                InputData inputData = (InputData)0;
                inputData.positionWS              = i.positionWS;
                inputData.normalWS                = normalWS;
                inputData.viewDirectionWS         = viewDirWS;
                inputData.shadowCoord             = shadowCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.positionCS              = i.positionCS;

                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, i.positionWS);
                    color += LightingPhysicallyBased(brdfData, addLight, normalWS, viewDirWS)
                             * _LightingStrength;
                LIGHT_LOOP_END

                // Floor so the sprite never goes pitch black even in deep shadow.
                half luminance = dot(color, half3(0.299, 0.587, 0.114));
                if (luminance < _MinLight)
                {
                    half lift = (_MinLight - luminance);
                    color += baseCol.rgb * lift;
                }

                // Hit flash (additive, drives feedback from existing EnemyHealth flash).
                color += _FlashColor.rgb * _FlashColor.a;

                return half4(color, baseCol.a);
            }

            ENDHLSL
        }

        // Lightweight shadow caster so the sprite can drop a real shadow
        // (in addition to your ProjectedSpriteShadow). Respects alpha cutoff.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _FlashColor;
                float  _LightingStrength;
                float  _AmbientStrength;
                float  _MinLight;
                float  _Smoothness;
                float  _Metallic;
                float  _DitherFade;
                float  _DitherScale;
                float  _AlphaCutoff;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = GetShadowPositionHClip(v);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * _Color.a;
                if (a < _AlphaCutoff)
                    discard;
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
