Shader "Mathra/Velvet Jewel Bushes"
{
 Properties
 {
  _BaseMap("Painted leaf surface",2D)="white"{}
  _PaletteStrength("Pure color palette",Range(0,1))=1
  _BaseColor("Leaf color",Color)=(1,1,1,1)
  _UndersideColor("Underside tint",Color)=(0.68,0.82,0.72,1)
  _VelvetColor("Velvet edge color",Color)=(0.64,0.9,0.78,1)
  _Velvet("Velvet softness",Range(0,1))=0.35
  _Translucency("Light through leaf",Range(0,1))=0.25
  _Ambient("Minimum ambient",Range(0,1))=0.18
  _WindStrength("Wind displacement (metres)",Range(0,0.3))=0.055
  _WindSpeed("Wind speed",Range(0,3))=0.65
  _WindScale("Wind spatial scale",Range(0.05,3))=0.45
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _BaseColor, _UndersideColor, _VelvetColor;
   float _PaletteStrength;
   float _Velvet, _Translucency, _Ambient, _WindStrength, _WindSpeed, _WindScale;
  CBUFFER_END
  struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
  float3 WindPosition(Attributes v)
  {
   float3 p=TransformObjectToWorld(v.positionOS.xyz);
   float phase=_Time.y*_WindSpeed+p.x*_WindScale+p.z*_WindScale*0.63;
   float w=_WindStrength*v.color.r;
   p+=float3(sin(phase)*w, sin(phase*1.43)*w*0.45, cos(phase*.79)*w*.5);
   return p;
  }
  ENDHLSL
  Pass
  {
   Name "VelvetForward"
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma target 3.5
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fog
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;};
   Varyings Vert(Attributes v)
   {
    Varyings o;o.positionWS=WindPosition(v);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
    o.fog=ComputeFogFactor(o.positionCS.z);return o;
   }
   half3 LeafLight(Light light,half3 n,half3 view,half3 albedo)
   {
    half diffuse=saturate((dot(n,light.direction)+.3)/1.3);
    half back=saturate(dot(-n,light.direction))*_Translucency;
    half velvet=pow(1-saturate(dot(n,view)),3)*_Velvet*diffuse;
    return (albedo*(diffuse+back)+_VelvetColor.rgb*velvet)*light.color*light.distanceAttenuation*light.shadowAttenuation;
   }
   half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    half3 n=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
    half3 painted=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    half luminance=dot(painted,half3(.2126,.7152,.0722));
    half3 albedo=lerp(painted,luminance.xxx,_PaletteStrength)*_BaseColor.rgb;
    albedo*=IS_FRONT_VFACE(face,half3(1,1,1),_UndersideColor.rgb);
    half3 view=SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
    half3 result=albedo*max(SampleSH(n),half3(_Ambient,_Ambient,_Ambient));
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    result+=LeafLight(main,n,view,albedo);
    #ifdef _ADDITIONAL_LIGHTS
    InputData inputData=(InputData)0;
    inputData.positionWS=i.positionWS;
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    #if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
    {Light light=GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1));result+=LeafLight(light,n,view,albedo);}
    #endif
    uint count=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
    Light light=GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1));result+=LeafLight(light,n,view,albedo);
    LIGHT_LOOP_END
    #endif
    return half4(MixFog(result,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma target 3.5
   #pragma fragment ShadowFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection;float3 _LightPosition;
   float4 ShadowVert(Attributes v):SV_POSITION
   {
    float3 p=WindPosition(v),n=TransformObjectToWorldNormal(v.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 l=normalize(_LightPosition-p);
    #else
    float3 l=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,l));
    #if UNITY_REVERSED_Z
    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
    #else
    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    return clip;
   }
   half4 ShadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex DepthVert
   #pragma target 3.5
   #pragma fragment DepthFrag
   float4 DepthVert(Attributes v):SV_POSITION{return TransformWorldToHClip(WindPosition(v));}
   half4 DepthFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormalsOnly"
   Tags {"LightMode"="DepthNormalsOnly"}
   ZWrite On
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex NormalsVert
   #pragma fragment NormalsFrag
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   struct NV {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;};
   NV NormalsVert(Attributes v){NV o;o.positionCS=TransformWorldToHClip(WindPosition(v));o.normalWS=TransformObjectToWorldNormal(v.normalOS);return o;}
   half4 NormalsFrag(NV i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    float3 n=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
    #else
    return half4(n,0);
    #endif
   }
   ENDHLSL
  }
 }
}
