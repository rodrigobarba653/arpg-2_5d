Shader "Custom/URP_DitherFade_Lit"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("Z Write", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Render Face)", Float) = 2

        [ToggleUI] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        [ToggleUI] _ReceiveShadows ("Receive Shadows", Float) = 1

        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1

        _MetallicMap ("Metallic Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.2

        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1

        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionMap ("Emission Map", 2D) = "white" {}

        _DitherFade ("Dither Fade", Range(0,1)) = 0
        _DitherScale ("Dither Scale", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _NormalStrength;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;
                float _AlphaClip;
                float _Cutoff;
                float _ReceiveShadows;
                float _DitherFade;
                float _DitherScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                float fogCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Dither4x4(float2 pixelPos)
            {
                int x = (int)pixelPos.x & 3;
                int y = (int)pixelPos.y & 3;
                int idx = x + y * 4;

                float dither[16] =
                {
                    0.0, 0.5, 0.125, 0.625,
                    0.75, 0.25, 0.875, 0.375,
                    0.1875, 0.6875, 0.0625, 0.5625,
                    0.9375, 0.4375, 0.8125, 0.3125
                };

                return dither[idx];
            }

            void ApplyDither(float4 screenPos)
            {
                if (_DitherFade > 0.001)
                {
                    float2 pixelPos = screenPos.xy / screenPos.w;
                    pixelPos *= _ScreenParams.xy * _DitherScale;

                    float noise = Dither4x4(pixelPos);
                    if (noise < _DitherFade)
                        discard;
                }
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.screenPos = ComputeScreenPos(o.positionCS);

                o.normalWS = normalize(normInputs.normalWS);
                float sign = v.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = float4(normalize(normInputs.tangentWS), sign);

                o.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 baseCol = baseSample * _BaseColor;

                if (_AlphaClip > 0.5 && baseCol.a < _Cutoff)
                    discard;

                ApplyDither(i.screenPos);

                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3 bitangentWS = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
                float3 normalWS = normalize(
                    normalTS.x * i.tangentWS.xyz +
                    normalTS.y * bitangentWS +
                    normalTS.z * i.normalWS
                );

                half4 metalSample = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, i.uv);
                half metallic = metalSample.r * _Metallic;
                half smoothness = metalSample.a * _Smoothness;
                if (metalSample.a < 0.01) smoothness = _Smoothness;

                half occlusion = lerp(
                    1.0,
                    SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).g,
                    _OcclusionStrength
                );

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));

                BRDFData brdfData;
                InitializeBRDFData(
                    baseCol.rgb,
                    metallic,
                    half3(0.04, 0.04, 0.04),
                    smoothness,
                    baseCol.a,
                    brdfData
                );

                half3 bakedGI = SampleSH(normalWS);
                half3 color = GlobalIllumination(brdfData, bakedGI, occlusion, normalWS, viewDirWS);

                float4 shadowCoord = (_ReceiveShadows > 0.5)
                    ? TransformWorldToShadowCoord(i.positionWS)
                    : float4(0,0,0,0);

                Light mainLight = GetMainLight(shadowCoord);
                if (_ReceiveShadows < 0.5) mainLight.shadowAttenuation = 1.0;

                color += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = shadowCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.positionCS = i.positionCS;

                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, i.positionWS);
                    if (_ReceiveShadows < 0.5) addLight.shadowAttenuation = 1.0;
                    color += LightingPhysicallyBased(brdfData, addLight, normalWS, viewDirWS);
                LIGHT_LOOP_END

                color += emission;
                color = MixFog(color, i.fogCoord);

                return half4(color, baseCol.a);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _NormalStrength;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;
                float _AlphaClip;
                float _Cutoff;
                float _ReceiveShadows;
                float _DitherFade;
                float _DitherScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attrib
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Vary
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float Dither4x4(float2 pixelPos)
            {
                int x = (int)pixelPos.x & 3;
                int y = (int)pixelPos.y & 3;
                int idx = x + y * 4;

                float dither[16] =
                {
                    0.0, 0.5, 0.125, 0.625,
                    0.75, 0.25, 0.875, 0.375,
                    0.1875, 0.6875, 0.0625, 0.5625,
                    0.9375, 0.4375, 0.8125, 0.3125
                };

                return dither[idx];
            }

            void ApplyDither(float4 screenPos)
            {
                if (_DitherFade > 0.001)
                {
                    float2 pixelPos = screenPos.xy / screenPos.w;
                    pixelPos *= _ScreenParams.xy * _DitherScale;

                    float noise = Dither4x4(pixelPos);
                    if (noise < _DitherFade)
                        discard;
                }
            }

            float4 GetShadowPositionHClip(Attrib v)
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Vary ShadowPassVertex(Attrib v)
            {
                Vary o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = GetShadowPositionHClip(v);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 ShadowPassFragment(Vary i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;

                if (_AlphaClip > 0.5 && a < _Cutoff)
                    discard;

                ApplyDither(i.screenPos);

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM

            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _NormalStrength;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;
                float _AlphaClip;
                float _Cutoff;
                float _ReceiveShadows;
                float _DitherFade;
                float _DitherScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct A
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct V
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float Dither4x4(float2 pixelPos)
            {
                int x = (int)pixelPos.x & 3;
                int y = (int)pixelPos.y & 3;
                int idx = x + y * 4;

                float dither[16] =
                {
                    0.0, 0.5, 0.125, 0.625,
                    0.75, 0.25, 0.875, 0.375,
                    0.1875, 0.6875, 0.0625, 0.5625,
                    0.9375, 0.4375, 0.8125, 0.3125
                };

                return dither[idx];
            }

            void ApplyDither(float4 screenPos)
            {
                if (_DitherFade > 0.001)
                {
                    float2 pixelPos = screenPos.xy / screenPos.w;
                    pixelPos *= _ScreenParams.xy * _DitherScale;

                    float noise = Dither4x4(pixelPos);
                    if (noise < _DitherFade)
                        discard;
                }
            }

            V DepthVertex(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 DepthFragment(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;

                if (_AlphaClip > 0.5 && a < _Cutoff)
                    discard;

                ApplyDither(i.screenPos);

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _NormalStrength;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;
                float _AlphaClip;
                float _Cutoff;
                float _ReceiveShadows;
                float _DitherFade;
                float _DitherScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct A
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct V
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
            };

            float Dither4x4(float2 pixelPos)
            {
                int x = (int)pixelPos.x & 3;
                int y = (int)pixelPos.y & 3;
                int idx = x + y * 4;

                float dither[16] =
                {
                    0.0, 0.5, 0.125, 0.625,
                    0.75, 0.25, 0.875, 0.375,
                    0.1875, 0.6875, 0.0625, 0.5625,
                    0.9375, 0.4375, 0.8125, 0.3125
                };

                return dither[idx];
            }

            void ApplyDither(float4 screenPos)
            {
                if (_DitherFade > 0.001)
                {
                    float2 pixelPos = screenPos.xy / screenPos.w;
                    pixelPos *= _ScreenParams.xy * _DitherScale;

                    float noise = Dither4x4(pixelPos);
                    if (noise < _DitherFade)
                        discard;
                }
            }

            V DepthNormalsVertex(A v)
            {
                V o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionCS = posInputs.positionCS;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                o.normalWS = normalize(normInputs.normalWS);
                float sign = v.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = float4(normalize(normInputs.tangentWS), sign);

                return o;
            }

            half4 DepthNormalsFragment(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;

                if (_AlphaClip > 0.5 && a < _Cutoff)
                    discard;

                ApplyDither(i.screenPos);

                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3 bitangentWS = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
                float3 normalWS = normalize(
                    normalTS.x * i.tangentWS.xyz +
                    normalTS.y * bitangentWS +
                    normalTS.z * i.normalWS
                );

                return half4(normalWS * 0.5 + 0.5, 1.0);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}