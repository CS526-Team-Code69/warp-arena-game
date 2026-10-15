Shader "AnomalyArena/V2/PixelCharacter"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelBlock ("Source pixels per displayed pixel", Range(1,16)) = 6
        _HitFlash ("Confirmed hit flash", Range(0,1)) = 0
        _SpriteUVRect ("Sprite frame UV rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _SpriteUVRect;
                float _PixelBlock;
                half _HitFlash;
            CBUFFER_END
            Varyings Vertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float2 frameMin = _SpriteUVRect.xy;
                float2 frameMax = frameMin + _SpriteUVRect.zw;
                float2 stepUv = max(1.0, _PixelBlock) * _MainTex_TexelSize.xy;
                float2 uv = frameMin + (floor((input.uv - frameMin) / stepUv) + .5) * stepUv;
                uv = clamp(uv, frameMin + _MainTex_TexelSize.xy * .5, frameMax - _MainTex_TexelSize.xy * .5);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                c.rgb = lerp(c.rgb, half3(1,1,1), saturate(_HitFlash));
                return c;
            }
            ENDHLSL
        }
    }
}
