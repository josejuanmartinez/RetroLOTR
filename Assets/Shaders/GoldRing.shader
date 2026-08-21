// Golden selection ring for the isometric hex board. Drop on a flat quad/mesh laid on the
// ground plane (same as a hex tile) — the ring is drawn as an ellipse (not a circle) via
// _IsoSquash so it reads as lying flat under the game's 2.5D isometric camera instead of
// looking like a circle floating upright. The orbiting "particles" are procedural comet-trail
// glints computed per-pixel (angle vs. time), not a Particle System, so this is a single
// self-contained material like HexPathFlow.
Shader "RetroLOTR/GoldRing"
{
    Properties
    {
        _ColorEdge ("Ring Edge Color (deep gold)", Color) = (0.55, 0.32, 0.05, 1)
        _ColorCenter ("Ring Center Highlight (bright gold)", Color) = (1, 0.88, 0.45, 1)
        _RingRadius ("Ring Radius (UV, 0-0.5)", Range(0.1, 0.5)) = 0.42
        _RingWidth ("Ring Width", Range(0.01, 0.3)) = 0.09
        _RingSoftness ("Ring Edge Softness", Range(0.001, 0.1)) = 0.015
        _IsoSquash ("Isometric Squash (Y, 1=circle)", Range(0.3, 1)) = 0.55

        _ParticleColor ("Particle Color", Color) = (1, 0.97, 0.75, 1)
        _ParticleCount ("Particle Count", Range(1, 8)) = 3
        _OrbitSpeed ("Orbit Speed (rev/sec, sign = direction)", Float) = 0.35
        _TrailLength ("Trail Length (radians)", Range(0.1, 3.14)) = 1.2
        _ParticleRadialWidth ("Particle Radial Glow Width", Range(0.005, 0.15)) = 0.035
        _ParticleIntensity ("Particle Intensity", Range(0, 5)) = 2.2

        _Alpha ("Overall Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define TAU 6.28318530718

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            fixed4 _ColorEdge;
            fixed4 _ColorCenter;
            float _RingRadius;
            float _RingWidth;
            float _RingSoftness;
            float _IsoSquash;

            fixed4 _ParticleColor;
            float _ParticleCount;
            float _OrbitSpeed;
            float _TrailLength;
            float _ParticleRadialWidth;
            float _ParticleIntensity;

            float _Alpha;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                // Un-squash Y before measuring radius/angle so the visible ring is a flattened
                // ellipse (matches the hex board's isometric footprint) while still using clean
                // circular math underneath.
                float2 pc = float2(p.x, p.y / max(_IsoSquash, 1e-4));
                float r = length(pc);
                float angle01 = atan2(pc.y, pc.x) / TAU + 0.5; // 0..1 around the ring

                // Ring band mask + cross-section gradient (bright center, dark edges).
                float halfW = _RingWidth * 0.5;
                float inner = _RingRadius - halfW;
                float outer = _RingRadius + halfW;
                float band = smoothstep(inner - _RingSoftness, inner + _RingSoftness, r)
                           - smoothstep(outer - _RingSoftness, outer + _RingSoftness, r);
                band = saturate(band);
                float cross = saturate(1.0 - abs(r - _RingRadius) / max(halfW, 1e-4));
                fixed3 baseColor = lerp(_ColorEdge.rgb, _ColorCenter.rgb, cross);

                // Orbiting comet-trail particles: one bright head per particle with an
                // exponential tail fading back along the direction of travel.
                float dir = _OrbitSpeed < 0 ? -1.0 : 1.0;
                float3 sparkle = 0;
                int count = (int)_ParticleCount;
                [unroll(8)]
                for (int idx = 0; idx < 8; idx++)
                {
                    if (idx >= count) break;
                    float head01 = frac(_Time.y * _OrbitSpeed + (float)idx / _ParticleCount);
                    float diff = angle01 - head01;
                    diff -= round(diff); // wrap to [-0.5, 0.5]
                    float behind = -diff * TAU * dir; // radians behind the head, along travel dir
                    float trail = saturate(1.0 - behind / _TrailLength);
                    trail = behind >= 0.0 ? pow(trail, 1.5) : 0.0;

                    float radial = exp(-0.5 * pow((r - _RingRadius) / _ParticleRadialWidth, 2));
                    sparkle += trail * radial * _ParticleColor.rgb;
                }

                fixed3 rgb = (baseColor * band + sparkle * _ParticleIntensity) * i.color.rgb;
                fixed alpha = saturate(band + length(sparkle) * _ParticleIntensity * 0.6) * _Alpha * i.color.a;

                fixed4 result;
                result.rgb = rgb;
                result.a = alpha;
                return result;
            }
            ENDCG
        }
    }
}
