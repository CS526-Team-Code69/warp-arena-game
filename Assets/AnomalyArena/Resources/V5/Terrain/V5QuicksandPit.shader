// A quicksand pit on the Dunes, drawn on one flat quad whose half width is exactly the pool's radius (Quicksand reads that radius from MapLayout.SandPool). The sand
// falls away in a bowl from the rim to a dark eye and slowly turns in a spiral. The shape comes from a surface normal worked out from the distance to the centre:
// the rim is the crease where the flat ground starts to drop, so the boundary shows without a drawn line. The pit's light comes from the top left on purpose:
// a bowl lit from below the eye-line reads as a dome, and this one must read as a hole.
Shader "AnomalyArena/V5/QuicksandPit"
{
    Properties
    {
        _SandMap ("Sand, the ground's own", 2D) = "white" {}
        _SandTile ("Cells per tile of the sand, as on the ground", Float) = 24
        _NoiseMap ("Tileable noise", 2D) = "gray" {}
        _Steep ("Slope of the bowl", Range(0, 3)) = 1.15
        _Arms ("Spiral arms", Float) = 4
        _Tight ("Spiral tightness", Float) = 5
        _Ripple ("Spiral relief", Range(0, 2)) = .24
        _Deep ("Light left at the eye", Range(0, 1)) = .24
        _DeepTint ("Tint of the deep sand (multiplier)", Vector) = (.92, .74, .56, 1)
        _SurfaceMean ("The ground's average sand colour", Vector) = (.2, .2, .2, 1)
        _LocalContrast ("The ground's local contrast", Range(0, 1)) = 1
        _Saturation ("The ground's saturation", Range(0, 1)) = 1
        _Grade ("The ground's colour grade (multiplier)", Vector) = (1, 1, 1, 1)
        _Clock ("Turning angle, radians, from Quicksand", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Name "V5QuicksandPit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; float2 world : TEXCOORD1; half3 light : TEXCOORD2; };
            TEXTURE2D(_SandMap); SAMPLER(sampler_SandMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _SandMap_ST, _NoiseMap_ST;
                half4 _DeepTint, _SurfaceMean, _Grade;
                float _SandTile, _Steep, _Arms, _Tight, _Ripple, _Deep, _Clock, _LocalContrast, _Saturation;
            CBUFFER_END

            Varyings Vertex(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.p = v.uv * 2 - 1;
                o.world = world.xz;
                Light light = GetMainLight();
                o.light = SampleSH(half3(0, 1, 0)) + light.color * light.distanceAttenuation * saturate(light.direction.y);
                return o;
            }

            half4 Fragment(Varyings v) : SV_Target
            {
                float r = length(v.p);
                half alpha = smoothstep(1, .975, r);
                if (alpha <= 0) return half4(0, 0, 0, 0);
                float2 outward = v.p / max(r, .001);
                float2 around = float2(-outward.y, outward.x);

                // Bowl: steepest just inside the rim, easing towards the eye. The normal leans towards the centre.
                half slope = _Steep * (.3 + .7 * r * r * r) * smoothstep(.0, .12, r);
                float2 lean = -outward * slope;

                // Spiral ridges wind down into the eye and turn with time; none at the rim, so the rim stays a clean line.
                float angle = atan2(v.p.y, v.p.x);
                float phase = angle * _Arms + log(max(r, .03)) * _Tight * 2 + _Clock * _Arms;
                half wander = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, v.world / 47).r - .5;
                half band = sin(phase + wander * 2.5) + .35 * sin(phase * 3 + 1.3 + wander * 4);
                half reach = smoothstep(.97, .55, r) * smoothstep(.02, .2, r);
                lean += (around * _Arms + outward * _Tight * 2) / (_Arms + _Tight * 2) * band * _Ripple * reach;

                half3 normal = normalize(half3(lean.x, 1, lean.y));
                const half3 fromTopLeft = half3(-.45, .78, .44);
                half shade = saturate(dot(normal, fromTopLeft)) / fromTopLeft.y;

                // The sand itself: the ground's texture at the rim, its ripples smoothed away on the way down, darker and redder towards the eye.
                float2 uv = v.world / _SandTile;
                half down = saturate(1 - r);
                half3 sharp = SAMPLE_TEXTURE2D(_SandMap, sampler_SandMap, uv).rgb;
                half3 soft = SAMPLE_TEXTURE2D_LOD(_SandMap, sampler_SandMap, uv, 5).rgb;
                // The grains stay, the ripples go: what is left of the sharp map after removing its broad light and dark.
                half3 sand = lerp(sharp, soft * (.72 + .28 * sharp / max(soft, .02)), saturate(down * 3.2));
                // The same calming as the ground's, so the rim meets it without a step.
                sand = lerp(_SurfaceMean.rgb, sand, _LocalContrast);
                sand = lerp(dot(sand, half3(.3, .6, .1)).xxx, sand, _Saturation);
                half depth = lerp(1, _Deep, pow(down, 1.1));
                half3 tint = lerp(half3(1, 1, 1), _DeepTint.rgb, saturate(down * 1.7));
                half3 color = sand * tint * v.light * shade * depth * _Grade.rgb;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
