Shader "Mathra/Venom Roach/Gas"
{
 Properties { _Glow ("Radiance", Float) = 1
 _Style ("Layer", Float) = 0 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half _Glow, _Style;
   CBUFFER_END
   struct Input { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
   struct Output { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
   Output vert(Input v) {
    Output o;
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.color = v.color;
    o.uv = v.uv;
    return o;
   }
   half4 frag(Output i) : SV_Target {
    float2 p = i.uv * 2 - 1;
    float r2=dot(p,p);
    float core=saturate(1-r2);
    float edge;
    float light;
    if (_Style < .5) {
     float t=_Time.y*2.1;
     float folds=sin(p.x*8+p.y*3+t)*sin(p.y*7-p.x*2-t*.7);
     edge=saturate((.88-sqrt(r2)+.12*folds)*3);
     light=.58+.29*core+.13*folds;
    } else if (_Style < 1.5) {
     edge=core*core;
     light=.75+core*core;
    } else if (_Style > 3.5) {
     float shell=abs(sqrt(r2)-.54);
     edge=saturate(1-shell*6)*.68+core*core*.24;
     light=.65+1.3*saturate(1-12*dot(p-float2(-.26,.32),p-float2(-.26,.32)));
    } else if (_Style > 2.5) {
     float spine=p.x+.16*sin(p.y*5);
     float width=.36*(1-.73*abs(p.y));
     edge=saturate((width-abs(spine))*9)*saturate((1-abs(p.y))*6);
     light=.6+1.65*saturate(1-14*abs(spine+.085));
    } else {
     edge=saturate((.83-r2)*8);
     float2 glint=p-float2(-.22,.28);
     light=.42+.46*sqrt(saturate(1-r2))+1.5*saturate(1-14*dot(glint,glint));
    }
    return half4(i.color.rgb*light*_Glow,i.color.a*edge);
   }
   ENDHLSL
  }
 }
}
