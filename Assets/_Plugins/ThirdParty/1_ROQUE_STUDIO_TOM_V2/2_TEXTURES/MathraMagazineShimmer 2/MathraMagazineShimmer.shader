Shader "Mathra/Collectibles/Magazine Shimmer URP"
{
    Properties
    {
        [HDR] _ShimmerColor ("Shimmer Color", Color) = (0.55, 0.83, 1.0, 1)
        _Brightness ("Brightness", Range(0, 8)) = 2.0
        _BandWidth ("Band Width (fraction)", Range(0.02, 0.8)) = 0.25
        _CoreSharpness ("Core Sharpness", Range(2, 24)) = 12
        _VeilStrength ("Soft Veil", Range(0, 1)) = 0.4
        _SurfaceLift ("Surface Lift (world units)", Range(0, 0.01)) = 0.0006
        [HideInInspector] _SweepStart ("Sweep Start", Float) = 0
        [HideInInspector] _SweepDuration ("Sweep Duration", Float) = 1.2
        [HideInInspector] _Axis ("Sweep Axis", Vector) = (1, 0, 1, 0)
        [HideInInspector] _Range ("Sweep Range", Vector) = (-1, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "SurfaceSweep"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShimmerColor;
                float _Brightness;
                float _BandWidth;
                float _CoreSharpness;
                float _VeilStrength;
                float _SurfaceLift;
                float _SweepStart;
                float _SweepDuration;
                float4 _Axis;
                float4 _Range;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float projection : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 worldPosition = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                worldPosition += normalWS * _SurfaceLift;
                o.positionCS = TransformWorldToHClip(worldPosition);
                o.projection = dot(v.positionOS.xyz, _Axis.xyz);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float p = saturate((_Time.y - _SweepStart) / max(0.05, _SweepDuration));
                float span = max(0.00001, _Range.y - _Range.x);
                // The entire luminous veil enters from one side and exits the other.
                float width = max(0.00001, span * _BandWidth);
                float center = lerp(_Range.x - width, _Range.y + width, p);
                float distance = abs(i.projection - center) / width;
                float veil = pow(saturate(1.0 - distance), 1.35);
                float core = pow(saturate(1.0 - distance), _CoreSharpness);
                float fade = smoothstep(0.0, 0.10, p) * (1.0 - smoothstep(0.90, 1.0, p));
                float strength = (veil * _VeilStrength + core) * _Brightness * fade;
                return half4(_ShimmerColor.rgb * strength, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
