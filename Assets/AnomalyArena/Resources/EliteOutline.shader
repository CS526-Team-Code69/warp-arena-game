Shader "AnomalyArena/EliteOutline"
{
    // Owner M116 (2026-10-22): an elite wears a bright white outline. Drawn as one more material on the elite's model renderers: the
    // back faces, pushed out along the screen-space normal by a fixed number of pixels, so a white rim shows round the body whatever the
    // model's size. Loaded from Resources so the web build always carries it (V2ModelPresentation).
    Properties
    {
        _OutlineColor ("Outline colour", Color) = (1,1,1,1)
        _OutlineWidth ("Outline width, pixels on a 1080-pixel-high screen", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "EliteOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float2 normalCS = mul((float2x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(v.normalOS));
                float2 push = normalCS / max(length(normalCS), 1e-5) * (_OutlineWidth * 2.0 / 1080.0);
                push.x *= _ScreenParams.y / _ScreenParams.x;
                o.positionCS.xy += push * o.positionCS.w;
                return o;
            }
            half4 Fragment(Varyings v) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
