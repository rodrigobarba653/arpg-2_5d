Shader "Custom/URP_FoliageDither"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        _WindStrength ("Wind Strength", Float) = 0.1
        _WindSpeed ("Wind Speed", Float) = 1.0

        _DitherFade ("Dither Fade", Range(0,1)) = 0
        _DitherScale ("Dither Scale", Float) = 2

        _LightInfluence ("Light Influence", Range(0,1)) = 0.6
        _Darkness ("Shadow Strength", Range(0,1)) = 0.3
        _BackLight ("Back Light", Range(0,1)) = 0.5
        _ColorVariation ("Color Variation", Range(0,1)) = 0.1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        // =========================
        // 🌿 MAIN PASS
        // =========================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _BaseColor;
            float _Cutoff;

            float _WindStrength;
            float _WindSpeed;

            float _DitherFade;
            float _DitherScale;

            float _LightInfluence;
            float _Darkness;
            float _BackLight;
            float _ColorVariation;

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

                float3 pos = v.positionOS.xyz;

                // 🌬️ WIND
                float wave = sin(_Time.y * _WindSpeed + pos.x * 2.0) * _WindStrength;
                pos.x += wave;
                pos.z += wave * 0.5;

                o.positionCS = TransformObjectToHClip(pos);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);

                o.normalWS = TransformObjectToWorldNormal(v.normalOS);

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                clip(col.a - _Cutoff);

                // 🔥 FAKE LIGHTING
                float3 lightDir = normalize(float3(0.3, 1, 0.2));
                float NdotL = saturate(dot(i.normalWS, lightDir));

                float lighting = lerp(1.0 - _Darkness, 1.0, NdotL * _LightInfluence);
                col.rgb *= lighting;

                float variation = sin(i.uv.x * 10.0) * _ColorVariation;
                col.rgb += variation;

                float back = saturate(dot(-lightDir, i.normalWS));
                col.rgb += back * _BackLight;

                // 🎭 DITHER
                float2 pixelPos = i.screenPos.xy / i.screenPos.w;
                pixelPos *= _ScreenParams.xy * _DitherScale;

                float noise = Dither4x4(pixelPos);
                float ditherAlpha = saturate(1.0 - _DitherFade);

                clip(ditherAlpha - noise);

                return col;
            }
            ENDHLSL
        }

        // =========================
        // 🌑 SHADOW PASS (CLAVE)
        // =========================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _BaseColor;
            float _Cutoff;

            float _WindStrength;
            float _WindSpeed;

            Varyings vert (Attributes v)
            {
                Varyings o;

                float3 pos = v.positionOS.xyz;

                // 🌬️ MISMO VIENTO (IMPORTANTE)
                float wave = sin(_Time.y * _WindSpeed + pos.x * 2.0) * _WindStrength;
                pos.x += wave;
                pos.z += wave * 0.5;

                o.positionCS = TransformObjectToHClip(pos);
                o.uv = v.uv;

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // 🌿 SOMBRA RESPETA HOJAS
                clip(col.a - _Cutoff);

                return 0;
            }
            ENDHLSL
        }
    }
}