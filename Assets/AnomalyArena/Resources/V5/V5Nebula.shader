// What lies below the void trial's platform: slowly turning warp clouds and far stars, in world space.
Shader "AnomalyArena/V5/Nebula"
{
    Properties
    {
        _NoiseMap ("Tileable noise", 2D) = "gray" {}
        _Deep ("Deep colour", Color) = (.012, .008, .035, 1)
        _CloudA ("Cloud colour A", Color) = (.26, .07, .55, 1)
        _CloudB ("Cloud colour B", Color) = (.04, .18, .42, 1)
        _Centre ("Centre, world xz", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off
        Pass
        {
            Name "V5Nebula"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST, _Centre;
                half4 _Deep, _CloudA, _CloudB;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                return o;
            }
            float N(float2 uv) { return SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv).r; }
            half4 Fragment(Varyings i) : SV_Target
            {
                float2 p = i.world.xz - _Centre.xz;
                float dist = length(p);
                // The clouds turn round the platform, more slowly the farther out.
                float turn = _Time.y * .018 + 6. / (dist * .03 + 2.);
                float s = sin(turn), c = cos(turn);
                float2 q = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                float a = N(q * .0065 + _Time.y * .002), b = N(q * .017 + a * .35 - _Time.y * .004), d = N(q * .04 + b * .3);
                float clouds = saturate((a * .55 + b * .35 + d * .2 - .38) * 1.7);
                float veins = pow(saturate(1. - abs(b - .5) * 3.2), 7.) * clouds;
                half3 col = _Deep.rgb + lerp(_CloudB.rgb, _CloudA.rgb, saturate(b * 1.6 - .2)) * clouds * clouds * .75;
                col += half3(.95, .45, 1.7) * veins * .16;
                // Far stars, standing still.
                float star = N(i.world.xz * .23);
                float twinkle = .7 + .3 * sin(_Time.y * 2. + star * 90.);
                col += pow(saturate((star - .74) * 3.9), 18.) * half3(1.5, 1.5, 1.9) * twinkle;
                return half4(col, 1.);
            }
            ENDHLSL
        }
    }
}
