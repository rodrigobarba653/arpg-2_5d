Shader "Custom/URP_DitherFade_Normal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        // 🔥 NORMAL MAP
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1

        _DitherFade ("Dither Fade", Range(0,1)) = 0
        _DitherScale ("Dither Scale", Float) = 1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        // =========================
        // MAIN PASS
        // =========================
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;

                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            float4 _BaseColor;
            float _NormalStrength;

            float _DitherFade;
            float _DitherScale;

            // =========================
            // DITHER
            // =========================
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

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);

                // 🔥 BASE VECTORS
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);

                float tangentSign = v.tangentOS.w;
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * tangentSign;

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // =========================
                // NORMAL MAP
                // =========================
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                normalTS.xy *= _NormalStrength;

                float3 normalWS = normalize(
                    normalTS.x * i.tangentWS +
                    normalTS.y * i.bitangentWS +
                    normalTS.z * i.normalWS
                );

                // =========================
                // SIMPLE LIGHTING
                // =========================
                float3 lightDir = normalize(float3(0.3, 1, 0.2));

                float NdotL = saturate(dot(normalWS, lightDir));

                col.rgb *= lerp(0.5, 1.0, NdotL);

                // =========================
                // DITHER
                // =========================
                float2 pixelPos = i.screenPos.xy / i.screenPos.w;
                pixelPos *= _ScreenParams.xy * _DitherScale;

                float noise = Dither4x4(pixelPos);

                if (noise < _DitherFade)
                    discard;

                return col;
            }
            ENDHLSL
        }

        // =========================
        // SHADOW PASS (igual)
        // =========================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}