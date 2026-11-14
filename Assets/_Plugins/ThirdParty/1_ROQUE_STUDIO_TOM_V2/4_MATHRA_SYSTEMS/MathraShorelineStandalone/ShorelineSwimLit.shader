Shader "Mathra/Standalone/Shoreline Swim Lit"
{
    Properties
    {
        [HideInInspector] _Detail("Surface detail", 2D) = "gray" {}
        [HideInInspector] _Wake("Wake", 2D) = "black" {}
        [HideInInspector] _Shore("Distance", 2D) = "white" {}
        _Sludge("Sludge", Color) = (.3,.23,.105,1)
        _Froth("Froth", Color) = (.79,.69,.4,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Shore); SAMPLER(sampler_Shore);
            TEXTURE2D(_Wake); SAMPLER(sampler_Wake);
            TEXTURE2D(_Detail); SAMPLER(sampler_Detail);
            CBUFFER_START(UnityPerMaterial)
            float4 _Sludge, _Froth, _Shape, _Motion;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half3 vertexLight:TEXCOORD2; };
            Varyings Vert(Attributes a)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.uv=a.uv;
                o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.vertexLight=VertexLighting(o.world,TransformObjectToWorldNormal(float3(0,1,0)));
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float dist=SAMPLE_TEXTURE2D(_Shore,sampler_Shore,i.uv).r*12;
                clip(_Shape.x*1.7+.2-dist);
                float2 p=(i.uv-.5)*_Motion.z;
                float t=_Time.y*_Motion.y;
                float2 drift=_Motion.x*float2(sin(t+p.y*.8),cos(t*.71+p.x*.63));
                float wake=SAMPLE_TEXTURE2D(_Wake,sampler_Wake,i.uv).r;
                float2 q=p+drift;
                float broad=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,q*.073).g;
                float islands=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,q*.31+float2(.28,.04)).g;
                float4 grains=SAMPLE_TEXTURE2D(_Detail,sampler_Detail,q/(2.4*_Shape.z));
                float field=(.08+.95*broad+.8*(islands-.5)+.26*(grains.b-.5)
                            +(_Shape.y-.5)*1.35)-dist/_Shape.x;
                // Raised wet lip where the parting wake pushes froth aside.
                float lip=4*wake*(1-wake);
                field+=lip*.15-wake*.96;
                float aa=max(.035,fwidth(field));
                float alpha=smoothstep(-aa,aa,field)*(1-smoothstep(.10,.78,wake))*_Shape.w;
                clip(alpha-.005);
                float relief=saturate(.16+grains.r*.83+(grains.b-.5)*.32+lip*.16);
                float3 color=lerp(_Sludge.rgb,_Froth.rgb,relief);
                color+=max(0,grains.r-.78)*.18;
                InputData lighting=(InputData)0;
                lighting.positionWS=i.world;
                lighting.positionCS=i.positionCS;
                lighting.normalWS=TransformObjectToWorldNormal(float3(0,1,0));
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                lighting.shadowCoord=TransformWorldToShadowCoord(i.world);
                lighting.shadowMask=half4(1,1,1,1);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.vertexLighting=i.vertexLight;
                SurfaceData foam=(SurfaceData)0;
                foam.albedo=color;
                foam.normalTS=half3(0,0,1);
                foam.occlusion=1;
                foam.alpha=alpha;
                return UniversalFragmentBlinnPhong(lighting,foam);
            }
            ENDHLSL
        }
    }
}
