Shader "AnomalyArena/V2/SkillGround"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Mode ("0 Ember bed, 1 Flame, 2 Gravity, 3 Storm, 4 Bolt", Float) = 0
        _EffectTime ("Gameplay time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Mode, _EffectTime;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color * _BaseColor; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Fragment(Varyings v) : SV_Target
            {
                float2 p = v.uv * 2 - 1;
                float r = length(p), t = _EffectTime;
                float alpha; half3 color;
                if (_Mode < .5)
                {
                    float n = Noise(p*5 + float2(t*.4,-t*.8));
                    alpha = (1-smoothstep(.65,1,r)) * (.5 + .5*n);
                    color = lerp(half3(.65,.055,.01),half3(1,.42,.035),n);
                }
                else if (_Mode < 1.5)
                {
                    float y = v.uv.y;
                    float n = Noise(float2(p.x*4, y*5-t*3));
                    float sway = sin(y*7-t*4)*.13*y;
                    float width = (.7-.65*y) + (n-.5)*.5;
                    float heat = saturate((width-abs(p.x+sway))*6);
                    alpha = heat * smoothstep(0,.12,y) * (1-smoothstep(.82,1,y));
                    color = lerp(half3(1,.12,.015),half3(1,.85,.22),saturate(heat*(1-y)*1.8));
                }
                else if (_Mode < 3.5)
                {
                    float edge = (1-smoothstep(.98,1,r)) * smoothstep(.91,.95,r);
                    float angle = atan2(p.y,p.x);
                    float motion = _Mode < 2.5 ? pow(saturate(sin(angle*3+r*18+t*5)),6)
                        : pow(saturate(sin(angle*10-r*5+t*9)),14);
                    alpha = max(edge*.65, motion*.28*(1-smoothstep(.8,.95,r))) * smoothstep(.05,.22,r);
                    color = half3(1,1,1);
                }
                else { alpha = 1; color = half3(1,1,1); }
                return half4(color*v.color.rgb,alpha*v.color.a);
            }
            ENDHLSL
        }
    }
}
