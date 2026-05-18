Shader "Custom/URP_DitherFade_Lit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1

        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0

        _DitherFade ("Dither Fade", Range(0,1)) = 0
        _DitherScale ("Dither Scale", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Back
            Blend One Zero

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;

                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            float4 _BaseColor;
            float _NormalStrength;
            float _Smoothness;
            float _Metallic;

            float _DitherFade;
            float _DitherScale;

            float Dither4x4(float2 pixelPos)
            {
                int x = (int)pixelPos.x & 3;
                int y = (int)pixelPos.y & 3;

                int index = x + y * 4;

                float dither[16] =
                {
                    0.0, 0.5, 0.125, 0.625,
                    0.75, 0.25, 0.875, 0.375,
                    0.1875, 0.6875, 0.0625, 0.5625,
                    0.9375, 0.4375, 0.8125, 0.3125
                };

                return dither[index];
            }

            Varyings vert (Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;

                o.normalWS = normalize(normalInputs.normalWS);
                o.tangentWS = normalize(normalInputs.tangentWS);

                float tangentSign = v.tangentOS.w * GetOddNegativeScale();
                o.bitangentWS = normalize(cross(o.normalWS, o.tangentWS) * tangentSign);

                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // DITHER
                float2 pixelPos = i.screenPos.xy / i.screenPos.w;
                pixelPos *= _ScreenParams.xy * _DitherScale;

                float noise = Dither4x4(pixelPos);

                if (noise < _DitherFade)
                    discard;

                // NORMAL MAP
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3 normalWS = normalize(
                    normalTS.x * i.tangentWS +
                    normalTS.y * i.bitangentWS +
                    normalTS.z * i.normalWS
                );

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));

                // BRDF SETUP - URP LIT STYLE
                BRDFData brdfData;
                half alpha = baseCol.a;
                half3 specular = half3(0.04, 0.04, 0.04);

                InitializeBRDFData(
                    baseCol.rgb,
                    _Metallic,
                    specular,
                    _Smoothness,
                    alpha,
                    brdfData
                );

                // AMBIENT / ENVIRONMENT
                half3 bakedGI = SampleSH(normalWS);
                half3 color = GlobalIllumination(
                    brdfData,
                    bakedGI,
                    1.0,
                    normalWS,
                    viewDirWS
                );

                // MAIN DIRECTIONAL LIGHT
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                color += LightingPhysicallyBased(
                    brdfData,
                    mainLight,
                    normalWS,
                    viewDirWS
                );

                // ADDITIONAL LIGHTS - POINT / SPOT
                #ifdef _ADDITIONAL_LIGHTS
                uint additionalLightsCount = GetAdditionalLightsCount();

                for (uint lightIndex = 0; lightIndex < additionalLightsCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, i.positionWS);

                    color += LightingPhysicallyBased(
                        brdfData,
                        light,
                        normalWS,
                        viewDirWS
                    );
                }
                #endif

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}