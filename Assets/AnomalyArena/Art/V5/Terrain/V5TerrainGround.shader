// Ground of the Ruins and the Dunes (WreckNaturalGround.mat, DustNaturalGround.mat). Everything is sampled in world coordinates, so the look does not depend on
// the floor's size. The picture never repeats: two differently placed copies of the surface are mixed by large soft noise, patches of rubble hide where the copies
// meet (the Ruins) and a low-frequency brightness runs over the whole map. V5TerrainGround draws the walls' shadows into _ShadowMask.
Shader "AnomalyArena/V5/TerrainGround"
{
    Properties
    {
        [HideInInspector] _BaseColor ("Tint, set by V2BiomeArt", Color) = (1, 1, 1, 1)
        _BaseMap ("Surface", 2D) = "white" {}
        _RubbleMap ("Rubble", 2D) = "white" {}
        _NoiseMap ("Noise: R large, G patches, B grit", 2D) = "gray" {}
        [NoScaleOffset] _ShadowMask ("Wall shadows from V5TerrainGround: R cast, G contact, B rubble", 2D) = "black" {}
        _ShadowRect ("Mask UV from world x z: scale x, scale z, offset x, offset z", Vector) = (.005556, .008333, .5, .5)
        _SurfaceTile ("Surface: cells per tile x z", Vector) = (20, 10, 0, 0)
        _SecondTile ("Second copy: cells per tile x z, UV offset x y", Vector) = (20, 10, .37, .61)
        _SecondTurn ("Second copy: cos and sin of its turn", Vector) = (0, 1, 0, 0)
        _RegionSoft ("Width of the mix between the copies", Range(.005, .5)) = .02
        _NoiseSize ("Noise tile in cells: large, patches, grit", Vector) = (150, 55, 17, 0)
        _RubbleTile ("Rubble: cells per tile", Float) = 11
        _RubbleTint ("Rubble tint (multiplier)", Vector) = (1, 1, 1, 1)
        _SeamRubble ("Rubble over the seam between the copies", Range(0, 2)) = 1
        _SeamWidth ("Width of that rubble", Range(.001, .3)) = .05
        _Patches ("Rubble patches: noise from, noise to", Vector) = (.72, .86, 0, 0)
        _RubbleFill ("How much the rubble keeps to the low parts of the surface", Range(0, 2)) = .7
        _WallRubble ("Rubble along the walls", Range(0, 3)) = 1
        _MacroDark ("Low-frequency tint, dark side (multiplier)", Vector) = (.88, .87, .86, 1)
        _MacroLight ("Low-frequency tint, light side (multiplier)", Vector) = (1.06, 1.05, 1.03, 1)
        _SurfaceMean ("Average linear colour of the surface; local contrast scales round it", Vector) = (.2, .2, .2, 1)
        _LocalContrast ("Local contrast: 1 as painted, .5 half. The combat effects are glows, so the ground must stay calm under them", Range(0, 1)) = 1
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Grade ("Colour grade: the scene's light is strongly blue, this balances it (multiplier)", Vector) = (1, 1, 1, 1)
        _ShadowDark ("How much a wall's cast shadow takes from the sunlight", Range(0, 1)) = .62
        _ContactDark ("Darkening right at a wall's foot", Range(0, 1)) = .5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _RubbleTint, _MacroDark, _MacroLight, _SurfaceMean, _Grade;
            float4 _BaseMap_ST, _RubbleMap_ST, _NoiseMap_ST;
            float4 _ShadowRect, _SurfaceTile, _SecondTile, _SecondTurn, _NoiseSize, _Patches;
            float _RegionSoft, _RubbleTile, _SeamRubble, _SeamWidth, _RubbleFill, _WallRubble, _LocalContrast, _Saturation, _ShadowDark, _ContactDark;
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
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world : TEXCOORD0;
                half3 ambient : TEXCOORD1;
                half3 sun : TEXCOORD2;
            };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RubbleMap); SAMPLER(sampler_RubbleMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_ShadowMask); SAMPLER(sampler_ShadowMask);

            Varyings Vertex(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.world = world.xz;
                Light light = GetMainLight();
                o.ambient = SampleSH(half3(0, 1, 0));
                o.sun = light.color * light.distanceAttenuation * saturate(light.direction.y);
                return o;
            }

            half4 Fragment(Varyings v) : SV_Target
            {
                float2 w = v.world;
                half large = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, w / _NoiseSize.x).r;
                half blotch = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, w.yx / _NoiseSize.y + float2(.31, .17)).g;
                half grit = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, w / _NoiseSize.z).b;

                // Two copies of the surface; the mix follows the noise, not a grid.
                float2 turned = float2(w.x * _SecondTurn.x - w.y * _SecondTurn.y, w.x * _SecondTurn.y + w.y * _SecondTurn.x);
                half3 first = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, w / _SurfaceTile.xy).rgb;
                half3 second = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, turned / _SecondTile.xy + _SecondTile.zw).rgb;
                half region = large * .72 + blotch * .28;
                half pick = smoothstep(.5 - _RegionSoft, .5 + _RegionSoft, region);
                half3 surface = lerp(first, second, pick);

                // Rubble fills the low parts first: patches, the seam between the copies and the strip along each wall.
                half4 mask = SAMPLE_TEXTURE2D(_ShadowMask, sampler_ShadowMask, w * _ShadowRect.xy + _ShadowRect.zw);
                half seam = (1 - smoothstep(0, _SeamWidth, abs(region - .5))) * _SeamRubble;
                half patches = smoothstep(_Patches.x, _Patches.y, blotch * .7 + large * .3);
                half wall = mask.b * _WallRubble * (.35 + grit);
                half high = dot(surface, half3(.3, .6, .1)) * 2;
                half want = max(max(seam, patches), wall) * 1.4 + (grit - .5) * .5 - high * _RubbleFill * .5;
                half rubble = smoothstep(.25, .55, want);
                half3 stones = SAMPLE_TEXTURE2D(_RubbleMap, sampler_RubbleMap, w / _RubbleTile).rgb * _RubbleTint.rgb;
                surface = lerp(surface, stones, rubble);

                // Calm the picture: less difference between slab and rubble, less colour. Glowing effects read better on this.
                surface = lerp(_SurfaceMean.rgb, surface, _LocalContrast);
                surface = lerp(dot(surface, half3(.3, .6, .1)).xxx, surface, _Saturation);

                // Light and dark drift over the whole map.
                half macro = smoothstep(.15, .85, large * .55 + SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, w.yx / (_NoiseSize.x * .37)).r * .45);
                surface *= lerp(_MacroDark.rgb, _MacroLight.rgb, macro);

                half cast = smoothstep(.2, .8, mask.r);
                half3 lit = v.ambient + v.sun * (1 - cast * _ShadowDark);
                lit *= 1 - mask.g * _ContactDark;
                return half4(surface * _BaseColor.rgb * lit * _Grade.rgb, 1);
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
