// Stone walls built at runtime by V5TerrainWalls. The vertex colour holds each stone's tint (RGB) and how much light reaches that corner (A: the dark mortar lines and
// the foot of the wall), so the masonry reads without any extra map. One texture sample and vertex lighting: every face is flat and the light does not move.
Shader "AnomalyArena/V5/TerrainWall"
{
    Properties
    {
        _BaseMap ("Stone surface", 2D) = "white" {}
        _Grade ("Colour grade: the scene's light is strongly blue, this balances it (multiplier)", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Grade;
            float4 _BaseMap_ST;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 tint : TEXCOORD1; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                half3 normal = TransformObjectToWorldNormal(v.normalOS);
                Light light = GetMainLight();
                half3 lit = SampleSH(normal) + light.color * light.distanceAttenuation * saturate(dot(normal, light.direction));
                o.tint = v.color.rgb * v.color.a * lit * _Grade.rgb;
                return o;
            }

            half4 Fragment(Varyings v) : SV_Target
            {
                return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, v.uv).rgb * v.tint, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            float4 Vertex(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half Fragment(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }
}
