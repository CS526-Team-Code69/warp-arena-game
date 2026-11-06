// The mirror image (document 7.4): the player's own model as a glowing hologram, brighter at the edges, with slow scan lines. It works on skinned meshes.
Shader "AnomalyArena/V5/Hologram"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (.35, .85, 1.4, 1)
        _Alpha ("Alpha", Range(0, 1)) = .75
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            Name "V5Hologram"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Alpha;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Fragment(Varyings i) : SV_Target
            {
                float3 toCamera = normalize(_WorldSpaceCameraPos - i.world);
                float edge = pow(1. - saturate(dot(normalize(i.normal), toCamera)), 1.8);
                float scan = .72 + .28 * sin(i.world.y * 16. - _Time.y * 7.);
                return half4(_BaseColor.rgb * (.5 + edge * 1.7) * scan, _Alpha * (.42 + edge * .58));
            }
            ENDHLSL
        }
    }
}
