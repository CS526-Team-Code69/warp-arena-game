// The Bulwark's earth wall: hard faces lit from above in a fixed light, stone mapped in world space on all three axes, darker where the rock comes out of the ground. No scene
// light, which is strongly blue. The vertex colour varies each face a little.
Shader "AnomalyArena/V5/Rock"
{
    Properties
    {
        _BaseMap ("Stone", 2D) = "gray" {}
        _Tint ("Earth colour", Color) = (1.06, .86, .68, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "V5Rock"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; half3 tint : TEXCOORD2; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                o.tint = v.color.rgb * _Tint.rgb;
                return o;
            }
            half4 Fragment(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normal);
                float3 w = pow(abs(n), 4.);
                w /= w.x + w.y + w.z;
                half3 stone = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.world.zy * .3).rgb * w.x
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.world.xz * .3).rgb * w.y
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.world.xy * .3).rgb * w.z;
                half light = .24 + 1.02 * saturate(dot(n, normalize(float3(.5, .78, -.42))));
                half foot = lerp(.4, 1., saturate(i.world.y / 1.2));
                return half4(stone * i.tint * light * foot, 1.);
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
