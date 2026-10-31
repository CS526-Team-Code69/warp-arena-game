// The void trial's ground (document 11.5: a round platform floating in the void): dark cracked stone in world space, so it does not stretch as the platform shrinks in
// the Nash Prophet's phases, with violet light in the cracks and a burning edge exactly where the ground ends. It works on the saved flattened cylinder.
Shader "AnomalyArena/V5/Platform"
{
    Properties
    {
        _NoiseMap ("Tileable noise", 2D) = "gray" {}
        _StoneA ("Stone, dark", Color) = (.26, .25, .33, 1)
        _StoneB ("Stone, light", Color) = (.58, .57, .66, 1)
        _Glow ("Crack and edge light", Color) = (.95, .4, 2, 1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "V5Platform"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float3 local : TEXCOORD1; };
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                half4 _StoneA, _StoneB, _Glow;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                // The saved cylinder has 20 sides: widened by 1 / cos(9 degrees) so a true circle of its real radius fits inside them.
                float3 position = v.positionOS.xyz;
                position.xz *= 1.0125;
                o.local = position;
                o.world = TransformObjectToWorld(position);
                o.positionCS = TransformWorldToHClip(o.world);
                return o;
            }
            float N(float2 uv) { return SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv).r; }
            half4 Fragment(Varyings i) : SV_Target
            {
                float2 p = i.world.xz;
                float large = N(p * .035), mid = N(p * .11 + 3.7), fine = N(p * .42 + 1.3);
                half3 c = lerp(_StoneA.rgb, _StoneB.rgb, saturate(large * .75 + mid * .45 + fine * .25 - .25));
                // Slabs: lines where the noise crosses a level.
                float slab = abs(frac(mid * 5.) - .5);
                float crack = smoothstep(.035, .0, slab);
                c *= 1. - crack * .55;
                // Glowing fractures, broken up so they do not read as the contour lines of the noise.
                float wide = smoothstep(.014, .0, abs(large - .52)) * smoothstep(.42, .62, mid);
                float pulse = .75 + .25 * sin(_Time.y * 1.7 + large * 12.);
                c += _Glow.rgb * wide * .3 * pulse;
                // The rim: where the ground ends, cut as a true circle. The cylinder's side is not drawn.
                float r = length(i.local.xz) * 2.;
                clip(1. - r);
                clip(i.local.y - .9);
                float edge = smoothstep(.962, .995, r);
                c = lerp(c, c * .5, smoothstep(.86, .98, r));
                c += _Glow.rgb * edge * (.8 + .2 * sin(_Time.y * 2.3));
                return half4(c, 1.);
            }
            ENDHLSL
        }
    }
}
