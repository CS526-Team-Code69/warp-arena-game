// Effect shader for the V5 art: unlit, vertex colour times texture, with blend mode, brightness and a push toward the camera set per material.
// Colours above 1 reach the bloom pass, so "bright" effects really glow.
Shader "AnomalyArena/V5/Particle"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [HideInInspector] _MainTex ("A SpriteRenderer's sprite", 2D) = "white" {}
        [HideInInspector] _UseSprite ("Draw _MainTex (a SpriteRenderer's sprite) instead of _BaseMap", Float) = 0
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Brightness", Float) = 1
        _Glow ("Extra brightness on the hot parts", Float) = 0
        _Bias ("Push toward the camera, units", Float) = 0
        _LumaAlpha ("Alpha from brightness (fire on black); 0 keeps the texture's alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        Pass
        {
            Name "V5Particle"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Intensity, _Glow, _LumaAlpha;
                float _Bias, _UseSprite;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                world += normalize(_WorldSpaceCameraPos - world) * _Bias;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.color = v.color * _BaseColor;
                return o;
            }
            half4 Fragment(Varyings i) : SV_Target
            {
                half4 t = _UseSprite > .5 ? SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) : SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 c = t * i.color;
                half peak = max(c.r, max(c.g, c.b));
                c.rgb *= _Intensity + _Glow * peak * peak;
                if (_LumaAlpha > 0.)
                {
                    // Light on black, premultiplied: the bright parts cover what is behind them, so fire stays fire on pale sand too. Alpha follows
                    // luminance, not the red channel: the red rim stays see-through and glows instead of cutting a hard outline.
                    c.a = saturate(dot(t.rgb, half3(.35, .55, .1)) * _LumaAlpha) * i.color.a;
                    c.rgb *= i.color.a;
                }
                return c;
            }
            ENDHLSL
        }
    }
}
