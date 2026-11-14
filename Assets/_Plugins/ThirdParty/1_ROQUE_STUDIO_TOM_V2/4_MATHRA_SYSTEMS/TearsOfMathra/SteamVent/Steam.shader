Shader "TearsOfMathra/Vent Vapor"
{
 Properties { _Liquid("Liquid bead", Float) = 0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _Liquid;
   CBUFFER_END
   struct Input { float4 pos:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   struct Output { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   Output vert(Input i) { Output o; o.pos=TransformObjectToHClip(i.pos.xyz); o.uv=i.uv; o.color=i.color; return o; }
   float hash(float2 p) { return frac(sin(dot(p,float2(41.3,289.1)))*43758.5); }
   float noise(float2 p)
   {
     float2 b=floor(p), a=frac(p); a=a*a*(3-2*a);
     return lerp(lerp(hash(b),hash(b+float2(1,0)),a.x),lerp(hash(b+float2(0,1)),hash(b+1),a.x),a.y);
   }
   half4 frag(Output i):SV_Target
   {
     float2 p=i.uv*2-1;
     if (_Liquid>.5)
     {
       float d=length(p);
       float spec=1-smoothstep(.06,.38,length(p-float2(-.23,.31)));
       return half4(i.color.rgb * lerp(.60,1.0,saturate(spec+d*.18)),i.color.a*(1-smoothstep(.65,.96,d)));
     }
     float2 q=p*3+float2(_Time.y*.36,-_Time.y*.23);
     float billow=noise(q)*.68+noise(q*2.3)*.32;
     float density=saturate((.81-length(p)+(billow-.5)*.72)*2.15);
     float light=smoothstep(.30,.72,billow-p.y*.12);
     return half4(i.color.rgb*lerp(.66,1.09,light),i.color.a*density*(.46+.54*light));
   }
   ENDHLSL
  }
 }
}
