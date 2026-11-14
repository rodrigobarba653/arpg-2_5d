Shader "Valcyr/Glass and Polycarbonate"
{
 Properties
 {
  [Header(Transmission)] _Tint("Body tint",Color)=(0.4,0.72,0.75,1)
  _Body("Body opacity",Range(0,0.8))=0.045
  _Thickness("Optical thickness",Range(0,3))=0.6
  _Grazing("Grazing angle density",Range(0,1))=0.3
  [Header(Reflections)] _Reflect("Reflection intensity",Range(0,3))=1
  _Face("Face reflectance",Range(0,0.45))=0.065
  _Rough("Base roughness",Range(0,1))=0.12
  _Highlight("Main light glint",Range(0,3))=0.6
  [Header(Surface wear)] [NoScaleOffset] _Wear("Packed wear (RG slopes B scratches A smudges)",2D)="gray"{}
  _Tile("Detail repeats XY",Vector)=(1,1,0,0)
  _Offset("Detail offset XY",Vector)=(0,0,0,0)
  _Bump("Surface distortion",Range(0,1))=0.12
  _Scratch("Scratch visibility",Range(0,1))=0.18
  _Smudge("Smudge visibility",Range(0,1))=0.12
  _WearRough("Wear roughness",Range(0,1))=0.45
  [Header(Optics)] _Film("Iridescent reflection",Range(0,1))=0
  _Bands("Film color bands",Range(0.1,5))=1.2
  [Toggle(_FACETS_ON)] _Facets("Flat facets from mesh triangles",Float)=0
  [KeywordEnum(Off,Bend,Prism)] _Refraction("Opaque scene refraction",Float)=0
  _Bend("Bend at 1080p (pixels)",Range(0,32))=8
  _Split("RGB separation",Range(0,1))=0.3
  _Mix("Refracted scene mix",Range(0,1))=0.55
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull faces",Float)=2
 }
 SubShader
 {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
 Pass
 {
  Name "Glass"
  Tags { "LightMode"="UniversalForwardOnly" }
  Blend One OneMinusSrcAlpha
  ZWrite Off
  Cull [_Cull]
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex Vert
  #pragma fragment Frag
  #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
  #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
  #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
  #pragma shader_feature_local_fragment _ _FACETS_ON
  #pragma shader_feature_local_fragment _REFRACTION_OFF _REFRACTION_BEND _REFRACTION_PRISM
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #if defined(_REFRACTION_BEND) || defined(_REFRACTION_PRISM)
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
  #endif
  TEXTURE2D(_Wear); SAMPLER(sampler_Wear);
  CBUFFER_START(UnityPerMaterial)
  float4 _Tint, _Tile, _Offset;
  float _Body, _Thickness, _Grazing, _Reflect, _Face, _Rough, _Highlight;
  float _Bump, _Scratch, _Smudge, _WearRough, _Film, _Bands;
  float _Facets, _Refraction, _Bend, _Split, _Mix, _Cull;
  CBUFFER_END
  struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
  struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 tangent:TEXCOORD2; float2 uv:TEXCOORD3; };
  Varyings Vert(Attributes v)
  {
   Varyings o;
   o.world=TransformObjectToWorld(v.positionOS.xyz);
   o.pos=TransformWorldToHClip(o.world);
   o.normal=TransformObjectToWorldNormal(v.normalOS);
   o.tangent=half4(TransformObjectToWorldDir(v.tangentOS.xyz,false),v.tangentOS.w*GetOddNegativeScale());
   o.uv=v.uv*_Tile.xy+_Offset.xy;
   return o;
  }
  half4 Frag(Varyings i):SV_Target
  {
   half4 wear=SAMPLE_TEXTURE2D(_Wear,sampler_Wear,i.uv);
   half3 view=GetWorldSpaceNormalizeViewDir(i.world);
   half3 n=normalize(i.normal);
   #if defined(_FACETS_ON)
   float3 plane=cross(ddx(i.world),ddy(i.world));
   float area=dot(plane,plane);
   if(area>1e-20) n=plane*rsqrt(area);
   #endif
   n*=dot(n,view)<0?-1:1;
   half3 t=i.tangent.xyz;
   half3 b=cross(n,t)*i.tangent.w;
   n=normalize(n+((wear.r*2-1)*t+(wear.g*2-1)*b)*_Bump);
   half rim=1-saturate(dot(n,view));
   half rim2=rim*rim;
   half fresnel=lerp(_Face,1,rim2*rim2*rim);
   half scratch=wear.b*_Scratch;
   half smear=wear.a*_Smudge;
   half rough=saturate(_Rough+_WearRough*(scratch+smear));
   float2 uv=GetNormalizedScreenSpaceUV(i.pos);
   half3 reflection=GlossyEnvironmentReflection(reflect(-view,n),i.world,rough,1,uv);
   half phase=(rim+wear.a*0.22)*_Bands;
   half3 film=saturate(abs(frac(phase+half3(0,0.333,0.667))*6-3)-1);
   reflection*=lerp(half3(1,1,1),0.5+film,_Film);
   Light key=GetMainLight();
   half3 h=SafeNormalize(view+key.direction);
   half glint=pow(saturate(dot(n,h)),exp2(lerp(9,3,rough)))*saturate(dot(n,key.direction));
   half density=saturate(_Body*_Thickness*(1+3*rim2*_Grazing)+scratch*0.30+smear*0.25);
   half alpha=density+(1-density)*fresnel;
   half3 color=_Tint.rgb*density*(1-fresnel)+reflection*fresnel*_Reflect;
   color+=key.color*key.distanceAttenuation*glint*_Highlight*(0.12+scratch)*(1-rough*0.7);
   #if defined(_REFRACTION_BEND) || defined(_REFRACTION_PRISM)
   float2 delta=TransformWorldToViewDir(n).xy*(_Bend*_Thickness/1080.0)*float2(_ScaledScreenParams.y/_ScaledScreenParams.x,1);
   half3 scene=SampleSceneColor(saturate(uv+delta));
   #if defined(_REFRACTION_PRISM)
   scene.r=SampleSceneColor(saturate(uv+delta*(1+_Split))).r;
   scene.b=SampleSceneColor(saturate(uv+delta*(1-_Split))).b;
   #endif
   half transmission=(1-alpha)*_Mix;
   color+=scene*transmission;
   alpha+=transmission;
   #endif
   return half4(color,alpha);
  }
  ENDHLSL
 }
 }
}
