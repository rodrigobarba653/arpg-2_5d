Shader "Game/UI/HealthLiquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CausticTex ("Caustic", 2D) = "gray" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Speed ("Flow Speed", Float) = 0.15
        _RepeatSize ("Repeat Size", Float) = 70
        _Slice ("Vertical Slice", Float) = 0.22
        _RippleColor ("Ripple", Color) = (0.113725, 0.533333, 0.572549, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _CausticTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Speed;
            float _RepeatSize;
            float _Slice;
            float4 _RippleColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float repeatSize = max(_RepeatSize, 1.0);
                float2 p = i.worldPosition.xy / repeatSize;
                float t = _Time.y * _Speed;

                float2 uvA = float2(p.x + t, p.y * _Slice + 0.20 + sin(p.x * 3.1 + t) * 0.015);
                float2 uvB = float2(p.x * 0.85 - t * 0.65, p.y * _Slice + 0.62 + sin(p.x * 2.2 - t * 1.3) * 0.02);
                float a = dot(tex2D(_CausticTex, uvA).rgb, float3(0.3, 0.59, 0.11));
                float b = dot(tex2D(_CausticTex, uvB).rgb, float3(0.3, 0.59, 0.11));
                float caustic = max(a, b);

                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                // Bright lines in the caustic texture are the darker ripple color.
                // The rest of the bar stays the vertex color (the teal).
                float lines = saturate((caustic - 0.35) / 0.36);
                lines = pow(lines, 0.78);
                col.rgb = lerp(col.rgb, _RippleColor.rgb, lines);

                float y = saturate(i.texcoord.y);
                float band = smoothstep(0.62, 0.74, y) * (1.0 - smoothstep(0.8, 0.94, y));
                col.rgb += band * 0.18;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
