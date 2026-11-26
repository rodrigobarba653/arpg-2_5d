Shader "Hidden/Mathra/AX Visibility Pose"
{
    Properties { [HideInInspector] _ZTest ("Depth", Float) = 8 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float _DepthOnly;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Look,_Tint,_ActorColor;
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(float3 pos:POSITION,float2 uv:TEXCOORD0)
            {
                V o; o.pos=TransformObjectToHClip(pos); o.uv=uv; return o;
            }
            half4 Frag(V i):SV_Target
            {
                if (_DepthOnly>.5)
                {
                    float depth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.pos));
                    #if UNITY_REVERSED_Z
                        clip(depth-i.pos.z-.000001);
                    #else
                        clip(i.pos.z-depth-.000001);
                    #endif
                }
                half4 s=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_ActorColor;
                clip(s.a-max(.001,_Look.z));
                return half4(lerp(_Tint.rgb,s.rgb,_Look.y),s.a*_Look.x*_Tint.a);
            }
            ENDHLSL
        }
    }
}
