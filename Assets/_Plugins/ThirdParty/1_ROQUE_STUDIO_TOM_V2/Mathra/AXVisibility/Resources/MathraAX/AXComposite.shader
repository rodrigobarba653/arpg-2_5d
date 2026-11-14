Shader "Hidden/Mathra/AX Visibility Composite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURE2D(_Current); SAMPLER(sampler_Current);
            float4 _Field, _Style, _Crop;
            float _Cell;
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(uint id:SV_VertexID)
            {
                Varyings o;
                o.positionCS=GetFullScreenTriangleVertexPosition(id);
                o.uv=o.positionCS.xy*.5+.5;
                if (_ProjectionParams.x<0) o.uv.y=1-o.uv.y;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float radius=length((i.uv-_Field.xy)/max(_Field.zw,.00001));
                float a=(1-smoothstep(1-_Style.x,1,radius))*_Style.y;
                clip(a-.00001);
                float2 uv=(i.uv-_Crop.xy)/_Crop.zw;
                return half4(SAMPLE_TEXTURE2D(_Current,sampler_Current,uv).rgb*a,a);
            }
            ENDHLSL
        }
    }
}
