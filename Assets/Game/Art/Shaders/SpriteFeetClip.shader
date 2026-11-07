Shader "TOM/SpriteFeetClip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FeetFraction ("Feet Fraction", Range(0.05, 1)) = 0.35
        _SpriteMinY ("Sprite Min Y", Float) = -0.5
        _SpriteMaxY ("Sprite Max Y", Float) = 0.5
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "FeetClip"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _FeetFraction;
                float _SpriteMinY;
                float _SpriteMaxY;
                half4 _RendererColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float localY : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.localY = input.positionOS.y;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Keep only the bottom band of the sprite mesh (works with atlases).
                float height = max(1e-5, _SpriteMaxY - _SpriteMinY);
                float keepMaxY = _SpriteMinY + height * _FeetFraction;
                clip(keepMaxY - input.localY);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = tex * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
