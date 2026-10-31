// Black hole and portal, drawn on one flat quad whose half width is the outer radius. Everything is computed from the distance to the centre, so the kill edge
// stays exactly where the rule says it is: _KillShare is the kill radius as a share of the quad's half width.
Shader "AnomalyArena/V5/Hole"
{
    Properties
    {
        _NoiseMap ("Tileable noise", 2D) = "gray" {}
        _KillShare ("Kill radius share", Range(0, 1)) = .5
        _DiskColor ("Disk colour, inner", Color) = (1, .55, 1.6, 1)
        _DiskColor2 ("Disk colour, outer", Color) = (.25, .2, 1.1, 1)
        _RimColor ("Rim colour", Color) = (1.5, .85, 2.3, 1)
        _EdgeColor ("Pull edge colour", Color) = (.8, .45, 1.3, 1)
        _Spin ("Spin", Float) = 1
        _Portal ("0 black hole, 1 portal", Range(0, 1)) = 0
        _Clock ("Clock", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "V5Hole"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; };
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float _KillShare, _Spin, _Portal, _Clock, _Fade;
                half4 _DiskColor, _DiskColor2, _RimColor, _EdgeColor;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.p = v.uv * 2. - 1.;
                return o;
            }
            float Noise(float2 uv) { return SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv).r; }
            half4 Fragment(Varyings i) : SV_Target
            {
                float r = length(i.p);
                if (r >= 1.) return 0;
                float kill = max(_KillShare, .001);
                float angle = atan2(i.p.y, i.p.x) / 6.28318;
                // Matter falling in: a log spiral, faster near the centre.
                float t = _Clock * _Spin;
                float fall = log(max(r, .02)) * .55;
                float2 swirlA = float2(angle * 3. + fall * 1.6 - t * .16, fall * 1.3 + t * .05);
                float2 swirlB = float2(angle * 5. + fall * 2.8 - t * .27, fall * .9 + t * .09);
                float streaks = Noise(swirlA) * .65 + Noise(swirlB) * .55;
                streaks = saturate((streaks - .42) * 2.6);
                // Share of the way from the kill edge (0) to the outer edge (1).
                float out01 = saturate((r - kill) / max(1. - kill, .001));
                float inner = pow(1. - out01, 2.2);
                half3 disk = lerp(_DiskColor2.rgb, _DiskColor.rgb, inner);
                float diskAlpha = streaks * (.12 + .95 * inner) * smoothstep(1., .72, out01);
                half3 c = disk * diskAlpha;
                float a = diskAlpha * .55;
                // Soft dark vignette, so the ground sinks toward the hole.
                float sink = pow(1. - out01, 1.6) * .6;
                a = max(a, sink);
                // Thin edge of the pull: this is where the danger starts, so it stays readable.
                float edge = smoothstep(.965, .985, r) * smoothstep(1., .992, r);
                float pulse = .6 + .4 * sin(_Clock * 3.2);
                c += _EdgeColor.rgb * edge * pulse;
                a = max(a, edge * .6);
                // The kill edge: pure black inside, a thin burning rim right on the line.
                float rimWidth = .012 + .018 * kill;
                float rim = exp(-pow((r - kill) / rimWidth, 2.));
                float rimOut = r > kill ? exp(-(r - kill) / (rimWidth * 2.5)) * .22 : 0.;
                c += _RimColor.rgb * (rim + rimOut);
                if (r < kill) { float seam = smoothstep(kill, kill - rimWidth * .6, r); c *= 1. - seam; a = 1.; }
                // A portal has no black centre: a bright standing gate.
                if (_Portal > .5)
                {
                    // A well of blue light with a turning thread round it; a thin bright edge marks where to stand.
                    float gate = saturate(1. - r);
                    float rays = Noise(float2(angle * 3. + log(max(r, .02)) * .9 - t * .25, r * .5 - t * .18));
                    half3 blue = lerp(half3(.08, .16, .85), half3(.45, .85, 1.3), pow(gate, 1.4));
                    c = blue * (.25 + rays * rays * 1.7) * pow(gate, .45);
                    // The bright inner ring is the place to stand (document 11.4: on the door): half a cell from the centre on a disc 1.7 cells wide.
                    float ringP = exp(-pow((r - .95) / .018, 2.)) * .6 + exp(-pow((r - .3) / .022, 2.)) * .9;
                    c += half3(.7, 1.05, 1.5) * ringP;
                    float heart = exp(-r * r / .012);
                    c += half3(1.1, 1.3, 1.7) * heart * .8;
                    a = saturate(pow(gate, .35) * .85 + heart);
                }
                return half4(c, a) * _Fade;
            }
            ENDHLSL
        }
    }
}
