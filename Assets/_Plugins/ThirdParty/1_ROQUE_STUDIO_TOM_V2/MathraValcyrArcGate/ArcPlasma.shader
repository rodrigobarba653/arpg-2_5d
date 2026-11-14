Shader "Mathra/Valcyr/Arc Plasma"
{
    Properties { [HDR] _Tint ("Plasma tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            Cull Back
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            CBUFFER_END
            struct Input { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; float4 color : COLOR; };
            Varyings Vert(Input i)
            {
                Varyings o;
                o.world = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(i.normal);
                o.color = i.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float face = saturate(dot(normalize(i.normal), GetWorldSpaceNormalizeViewDir(i.world)));
                return half4(i.color.rgb * _Tint.rgb * i.color.a * pow(face, 1.35), 0);
            }
            ENDHLSL
        }
    }
}
