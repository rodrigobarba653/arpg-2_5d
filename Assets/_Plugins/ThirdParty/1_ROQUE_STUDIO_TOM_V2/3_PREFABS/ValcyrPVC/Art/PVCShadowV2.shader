Shader "Valcyr/PVC Strip Shadows"
{
    Properties
    {
        [MainTexture] _BaseMap("PVC dirt and opacity", 2D) = "white" {}
        _ShadowDensity("Shadow density", Range(0, 3)) = 1.35
        _Cutoff("Stable Shadow Cutoff", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _ShadowDensity;
                half _Cutoff;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                float3 pos = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 towardLight = normalize(_LightPosition - pos);
                #else
                    float3 towardLight = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(pos, normal, towardLight));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                half coverage = saturate(alpha * _ShadowDensity);
                // Stable alpha cutout: no screen-space or shadow-map-coordinate noise.
                clip(coverage - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
