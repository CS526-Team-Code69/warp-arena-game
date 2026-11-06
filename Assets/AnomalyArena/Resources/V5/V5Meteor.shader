// The Tyrant's meteor in flight: stone that rolls with the rock (mapped in its own space, so it does not slide), lit from above, with molten cracks and a burning side
// facing away from its flight. _Back is the world direction it came from, set each frame.
Shader "AnomalyArena/V5/Meteor"
{
    Properties
    {
        _BaseMap ("Stone", 2D) = "gray" {}
        _NoiseMap ("Tileable noise", 2D) = "gray" {}
        _StoneTint ("Stone tint", Color) = (.5, .44, .4, 1)
        _Hot ("Molten light", Color) = (2.6, .95, .25, 1)
        _Back ("Direction it came from, world", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "V5Meteor"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 local : TEXCOORD0; float3 localNormal : TEXCOORD1; float3 worldNormal : TEXCOORD2; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _NoiseMap_ST;
                half4 _StoneTint, _Hot;
                float4 _Back;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.local = v.positionOS.xyz;
                o.localNormal = v.normalOS;
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half4 Fragment(Varyings i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 w = pow(abs(normalize(i.localNormal)), 4.);
                w /= w.x + w.y + w.z;
                float3 p = i.local;
                half3 stone = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.yz * .55).rgb * w.x + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zx * .55).rgb * w.y + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy * .55).rgb * w.z;
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.yz * .28).r * w.x + SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.zx * .28).r * w.y + SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.xy * .28).r * w.z;
                half light = .38 + .72 * saturate(dot(n, normalize(float3(.25, 1., -.35))));
                half3 c = stone * _StoneTint.rgb * light;
                half burn = saturate(dot(n, normalize(_Back.xyz)));
                float crack = smoothstep(.05, .0, abs(noise - .5));
                c += _Hot.rgb * crack * (.35 + burn * .9);
                // The burning side glows in patches, so a rock falling away from the camera stays a rock and not a ball of light.
                c += _Hot.rgb * burn * burn * smoothstep(.42, .7, noise) * .34;
                return half4(c, 1.);
            }
            ENDHLSL
        }
    }
}
