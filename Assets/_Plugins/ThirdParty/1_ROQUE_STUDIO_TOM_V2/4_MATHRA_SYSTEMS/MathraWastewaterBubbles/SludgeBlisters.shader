Shader "Mathra/Water/Sludge Blisters"
{
    Properties
    {
        _Membrane ("Membrane", Color) = (.36,.44,.19,1)
        _Glint ("Oily highlight", Color) = (.82,.78,.50,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Membrane, _Glint, _AreaSize, _Diameter;
            float _Height, _Pixels, _Speed, _Idle, _Opacity;
            CBUFFER_END
            struct Input { float4 pos:POSITION; float2 uv:TEXCOORD0; float2 key:TEXCOORD1; };
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 life:TEXCOORD1; };
            float Hash(float2 key)
            {
                float3 p=frac(key.xyx*float3(.1031,.1030,.0973));
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            Output Vert(Input i)
            {
                Output o;
                float duration=lerp(1.7,3.5,Hash(i.key+2));
                float period=duration+_Idle*lerp(.55,1.55,Hash(i.key+8));
                float clock=_Time.y*_Speed+period*Hash(i.key+5);
                float cycle=floor(clock/period);
                float2 key=i.key+float2(cycle,cycle*.27);
                float pick=Hash(key+11);
                // Smaller bubbles appear more often. Allow space for the burst around the dome.
                float extent=min(lerp(_Diameter.x,_Diameter.y,pick*pick)*1.62,min(_AreaSize.x,_AreaSize.y));
                float2 center=(float2(Hash(key+21),Hash(key+39))-.5)*max(0,_AreaSize.xy-extent);
                float2 xz=center+i.pos.xz*extent;
                o.pos=TransformObjectToHClip(float3(xz.x,_Height,xz.y));
                o.uv=i.uv; o.life=float2((clock-cycle*period)/duration,Hash(key+51));
                return o;
            }
            half4 Frag(Output i):SV_Target
            {
                float age=i.life.x, v=i.life.y;
                clip(.999-age);
                float2 q=((floor(i.uv*_Pixels)+.5)/_Pixels-.5)*2;
                float angle=atan2(q.y,q.x+.00001);
                float grow=smoothstep(0,.53,age);
                float rad=lerp(.045,.61,grow);
                rad*=1+.032*sin(angle*3+v*27+age*6)+.022*sin(angle*5-v*8);
                float2 p=q;
                p.x+=.025*sin(age*10+v*23)*grow;
                p.y-=.045*grow;
                p.y*=1.04+.14*v;
                float dist=length(p);
                float d=dist/rad;
                float tear=smoothstep(.60,.79,age);
                float2 hole=p-float2(.12,-.05)*(1-tear);
                float holeDist=length(hole)*(1+.13*sin(angle*4+v*22)+.08*sin(angle*7-age*9));
                float body=step(d,1)*step(tear*.77,holeDist+.001)*(1-step(.80,age));
                // Discrete pigment bands, an uneven lower lip, and a small off-axis specular streak.
                float3 base=_Membrane.rgb*lerp(.76,1.20,v);
                float3 n=normalize(float3(p.x/rad,p.y/rad,sqrt(saturate(1-d*d))));
                float light=dot(n,normalize(float3(-.5,.55,.75)));
                float band=floor(saturate(light)*4)/4;
                float3 pigment=lerp(base*.29,base*1.38,band);
                if(d>.87) pigment=base*(p.y<0?.36:.73);
                float2 h=(p/rad-float2(-.32,.42))*float2(1.45,2.5);
                float streak=step(length(h),.38)*step(.10,length(h+float2(.13,.13)));
                pigment=lerp(pigment,_Glint.rgb,.84*streak);
                float lip=step(abs(holeDist-tear*.77),.046)*step(.015,tear);
                pigment=lerp(pigment,_Glint.rgb,lip*.69);
                float burst=saturate((age-.62)/.38);
                float ring=step(abs(dist-lerp(.48,.91,burst)),lerp(.046,.023,burst));
                ring*=step(.61,age)*step(.12+burst*.62,.56+.34*sin(3*angle+v*30)+.22*sin(7*angle+v));
                float drops=0;
                [unroll] for(int j=0;j<5;j++)
                {
                    float a=v*17+j*1.256+.27*sin(j*19+v);
                    float2 dir=float2(cos(a),sin(a));
                    float2 pos=dir*(.36+burst*(.40+.12*sin(j+v*8)))+float2(0,.18*sin(burst*3.14159));
                    drops=max(drops,step(length((q-pos)*float2(1,1.25)),(.068-.051*burst)*(1+.2*sin(j))));
                }
                drops*=step(.62,age);
                float residue=max(ring*.62,drops*.92)*(1-smoothstep(.79,1,age));
                float alpha=max(body*lerp(.74,.94,band),residue)*_Opacity;
                clip(alpha-.004);
                return half4(body>.5 ? pigment : lerp(base,_Glint.rgb,drops*.55),alpha);
            }
            ENDHLSL
        }
    }
}
