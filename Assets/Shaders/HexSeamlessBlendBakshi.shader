// Bakshi-skin variant of RetroLOTR/HexSeamlessBlend: identical seamless-blend/neon-grid logic
// (kept property-for-property compatible with HexSeamlessTerrain's MaterialPropertyBlock feed —
// see HexSeamlessBlend.shader for how that part works) plus the cel-shaded "80s Cartoon" look
// from UI80sCartoon.shader (RetroLOTR/UI/80s Cartoon), applied to the fully-composited hex pixel
// (blended terrain + neon grid) so tiles pick up the same ink-contour/posterized look as Bakshi UI.
Shader "RetroLOTR/HexSeamlessBlendBakshi"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SpriteUV ("Sprite UV (own tile atlas rect)", Vector) = (0,0,1,1)

        _NeighborTex0 ("Neighbor E", 2D) = "black" {}
        _NeighborTex1 ("Neighbor NE", 2D) = "black" {}
        _NeighborTex2 ("Neighbor NW", 2D) = "black" {}
        _NeighborTex3 ("Neighbor W", 2D) = "black" {}
        _NeighborTex4 ("Neighbor SW", 2D) = "black" {}
        _NeighborTex5 ("Neighbor SE", 2D) = "black" {}

        _NeighborUV0 ("Neighbor UV E", Vector) = (0,0,1,1)
        _NeighborUV1 ("Neighbor UV NE", Vector) = (0,0,1,1)
        _NeighborUV2 ("Neighbor UV NW", Vector) = (0,0,1,1)
        _NeighborUV3 ("Neighbor UV W", Vector) = (0,0,1,1)
        _NeighborUV4 ("Neighbor UV SW", Vector) = (0,0,1,1)
        _NeighborUV5 ("Neighbor UV SE", Vector) = (0,0,1,1)

        _NeighborOffset0 ("Neighbor Offset E", Vector) = (0,0,0,0)
        _NeighborOffset1 ("Neighbor Offset NE", Vector) = (0,0,0,0)
        _NeighborOffset2 ("Neighbor Offset NW", Vector) = (0,0,0,0)
        _NeighborOffset3 ("Neighbor Offset W", Vector) = (0,0,0,0)
        _NeighborOffset4 ("Neighbor Offset SW", Vector) = (0,0,0,0)
        _NeighborOffset5 ("Neighbor Offset SE", Vector) = (0,0,0,0)

        _NeighborValid0 ("Neighbor Valid E", Float) = 0
        _NeighborValid1 ("Neighbor Valid NE", Float) = 0
        _NeighborValid2 ("Neighbor Valid NW", Float) = 0
        _NeighborValid3 ("Neighbor Valid W", Float) = 0
        _NeighborValid4 ("Neighbor Valid SW", Float) = 0
        _NeighborValid5 ("Neighbor Valid SE", Float) = 0

        _AspectY ("Cell Aspect (drawn H / drawn W)", Float) = 1.3
        _BlendStrength ("Blend Strength (0.5 = seamless)", Range(0,0.5)) = 0.5
        _BlendBand ("Blend Band (fraction of center-to-edge)", Range(0.05,1)) = 0.4
        _EdgeTrim ("Edge Trim (feather width past the seam)", Range(0.01,0.1)) = 0.08
        _FogFade ("Fog Fade (band before the seam)", Range(0.05,0.5)) = 0.25
        _GammaOut ("Editor gamma output", Float) = 0
        _EdgeCrop ("Crop Overdraw To Hex", Float) = 1

        _GridOn ("Neon Grid Enabled", Float) = 1
        _GridColor ("Neon Grid Tint", Color) = (1, 1, 1, 1)
        _GridIntensity ("Neon Grid Intensity", Range(0,2)) = 0.2
        _GridWidth ("Neon Grid Core Width (px)", Range(0.5,4)) = 1.2
        _GridGlowWidth ("Neon Grid Glow Width (px)", Range(0.5,12)) = 1
        _GridHueScale ("Neon Hue Cycle (per tile)", Range(0.02,0.5)) = 0.12
        _CellCenter ("Cell Center (map units, set per cell)", Vector) = (0,0,0,0)

        // Bakshi cel-shading (same vocabulary/defaults as RetroLOTR/UI/80s Cartoon).
        _CelBands ("Cel Bands", Range(2, 12)) = 6
        _OutlineStrength ("Ink Contours", Range(0, 2)) = 0.75
        _ColorSimplification ("Color Simplification", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0.5, 1.5)) = 1.08
        _BroadcastTexture ("Broadcast Texture", Range(0, 0.15)) = 0.025
        _EffectIntensity ("Effect Intensity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0 // fwidth, for zoom-independent grid line width
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex         : SV_POSITION;
                fixed4 color          : COLOR;
                float2 uv             : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _SpriteUV;

            sampler2D _NeighborTex0; sampler2D _NeighborTex1; sampler2D _NeighborTex2;
            sampler2D _NeighborTex3; sampler2D _NeighborTex4; sampler2D _NeighborTex5;
            float4 _NeighborUV0; float4 _NeighborUV1; float4 _NeighborUV2;
            float4 _NeighborUV3; float4 _NeighborUV4; float4 _NeighborUV5;
            float4 _NeighborOffset0; float4 _NeighborOffset1; float4 _NeighborOffset2;
            float4 _NeighborOffset3; float4 _NeighborOffset4; float4 _NeighborOffset5;
            float _NeighborValid0; float _NeighborValid1; float _NeighborValid2;
            float _NeighborValid3; float _NeighborValid4; float _NeighborValid5;

            float _AspectY;
            float _BlendStrength;
            float _BlendBand;
            float _EdgeTrim;
            float _FogFade;
            float _GammaOut;
            float _EdgeCrop;

            float _GridOn;
            fixed4 _GridColor;
            float _GridIntensity;
            float _GridWidth;
            float _GridGlowWidth;
            float _GridHueScale;
            float4 _CellCenter;

            float _CelBands;
            float _OutlineStrength;
            float _ColorSimplification;
            float _Saturation;
            float _BroadcastTexture;
            float _EffectIntensity;

            float3 HueToRgb(float h)
            {
                h = frac(h);
                float r = abs(h * 6.0 - 3.0) - 1.0;
                float g = 2.0 - abs(h * 6.0 - 2.0);
                float b = 2.0 - abs(h * 6.0 - 4.0);
                return saturate(float3(r, g, b));
            }

            float LuminanceCartoon(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            // Posterize/ink-contour/broadcast-noise pass ported from RetroLOTR/UI/80s Cartoon's
            // frag(), applied here to the fully-composited hex pixel (blended terrain + neon grid)
            // instead of a single sprite sample.
            float3 ApplyCartoonLook(float3 rgb, float alpha, float2 screenUv)
            {
                float luminance = LuminanceCartoon(rgb);
                float bands = max(2.0, _CelBands);
                float quantizedLuminance = floor(luminance * bands + 0.5) / bands;
                float3 cartoon = quantizedLuminance.xxx + (rgb - luminance.xxx);

                float paletteSteps = lerp(32.0, 8.0, _ColorSimplification);
                cartoon = floor(saturate(cartoon) * paletteSteps + 0.5) / paletteSteps;
                float cartoonLuminance = LuminanceCartoon(cartoon);
                cartoon = cartoonLuminance.xxx + (cartoon - cartoonLuminance.xxx) * _Saturation;

                float contourGradient = abs(ddx(luminance)) + abs(ddy(luminance));
                float alphaGradient = abs(ddx(alpha)) + abs(ddy(alpha));
                float contour = smoothstep(0.025, 0.16, contourGradient + alphaGradient * 0.35);
                cartoon *= 1.0 - contour * saturate(_OutlineStrength) * 0.72;

                float2 broadcastCell = floor(screenUv * _ScreenParams.xy * 0.55);
                float noise = Hash21(broadcastCell + floor(_Time.y * 12.0) * float2(7.0, 13.0)) - 0.5;
                float scanline = sin(screenUv.y * _ScreenParams.y * UNITY_PI) * 0.09;
                cartoon += (noise + scanline) * _BroadcastTexture;

                cartoon = lerp(float3(0.018, 0.021, 0.020),
                    float3(0.965, 0.955, 0.905), saturate(cartoon));

                return lerp(rgb, cartoon, _EffectIntensity);
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                o.screenPosition = ComputeScreenPos(o.vertex);
                return o;
            }

            // t (seam coordinate, 1 == the shared edge) for one direction, with no neighbor/glow
            // logic attached — used to find which of the 6 edges is nearest at a given pixel before
            // any of them draw their line (see tMax in frag()).
            float SeamT(float2 offset, float2 s)
            {
                float dist = length(offset);
                if (dist < 1e-4) return -1e6; // no geometry in this slot; never the nearest edge
                float halfD = 0.5 * dist;
                return dot(s, offset / dist) / halfD;
            }

            // Accumulates one neighbor's contribution for fragment position s (tile-local frame).
            // tMax is the largest of all 6 directions' t at this pixel (see frag()) — a direction's
            // grid line only draws where it IS that nearest edge.
            void AccumulateNeighbor(float valid, sampler2D tex, float4 rect, float2 offset,
                                    float2 s, float tMax, inout float3 rgbSum, inout float weightSum,
                                    inout float alphaMask, inout float glow)
            {
                float dist = length(offset);
                if (dist < 1e-4) return;

                float2 u = offset / dist;
                float dAlong = dot(s, u);
                float halfD = 0.5 * dist; // the shared edge lies halfway to the neighbor's center
                float t = dAlong / halfD; // 1 at the seam

                float seamPx = abs(t - 1.0) / max(fwidth(t), 1e-5);
                float core = saturate(1.0 - seamPx / _GridWidth);
                float halo = exp2(-seamPx / _GridGlowWidth);
                float nearestEdgeMask = smoothstep(-fwidth(t) * 2.0, 0.0, t - tMax);
                glow = max(glow, (core + halo * 0.2) * nearestEdgeMask);

                if (valid < -0.5)
                {
                    alphaMask = min(alphaMask, 1.0 - smoothstep(1.0 - _FogFade, 1.0, t));
                    return;
                }
                if (valid < 0.5) return;

                if (_EdgeCrop > 0.5)
                    alphaMask = min(alphaMask, 1.0 - smoothstep(1.0, 1.0 + _EdgeTrim, t));

                float band = _BlendBand * halfD;
                float w = saturate((dAlong - (halfD - band)) / band);
                if (w <= 0.0) return;

                float2 sn = s - 2.0 * dAlong * u;
                float2 nLocal = saturate(float2(sn.x + 0.5, sn.y / _AspectY + 0.5));
                float2 uv = rect.xy + nLocal * rect.zw;

                float2 texel = rect.zw * 0.02;
                fixed4 c = tex2D(tex, uv);
                c += tex2D(tex, uv + float2(texel.x, 0));
                c += tex2D(tex, uv - float2(texel.x, 0));
                c += tex2D(tex, uv + float2(0, texel.y));
                c += tex2D(tex, uv - float2(0, texel.y));
                float3 rgb = saturate(c.rgb / max(c.a, 1e-3));
                float alpha = c.a / 5.0;

                w *= smoothstep(0.0, 0.35, alpha);

                rgbSum += w * rgb;
                weightSum += w;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 own = tex2D(_MainTex, i.uv);

                float2 localUV = (i.uv - _SpriteUV.xy) / _SpriteUV.zw;
                float2 s = float2(localUV.x - 0.5, (localUV.y - 0.5) * _AspectY);

                float tMax = max(
                    max(max(SeamT(_NeighborOffset0.xy, s), SeamT(_NeighborOffset1.xy, s)),
                        max(SeamT(_NeighborOffset2.xy, s), SeamT(_NeighborOffset3.xy, s))),
                    max(SeamT(_NeighborOffset4.xy, s), SeamT(_NeighborOffset5.xy, s)));

                float3 rgbSum = float3(0, 0, 0);
                float weightSum = 0.0;
                float alphaMask = 1.0;
                float glow = 0.0;
                AccumulateNeighbor(_NeighborValid0, _NeighborTex0, _NeighborUV0, _NeighborOffset0.xy, s, tMax, rgbSum, weightSum, alphaMask, glow);
                AccumulateNeighbor(_NeighborValid1, _NeighborTex1, _NeighborUV1, _NeighborOffset1.xy, s, tMax, rgbSum, weightSum, alphaMask, glow);
                AccumulateNeighbor(_NeighborValid2, _NeighborTex2, _NeighborUV2, _NeighborOffset2.xy, s, tMax, rgbSum, weightSum, alphaMask, glow);
                AccumulateNeighbor(_NeighborValid3, _NeighborTex3, _NeighborUV3, _NeighborOffset3.xy, s, tMax, rgbSum, weightSum, alphaMask, glow);
                AccumulateNeighbor(_NeighborValid4, _NeighborTex4, _NeighborUV4, _NeighborOffset4.xy, s, tMax, rgbSum, weightSum, alphaMask, glow);
                AccumulateNeighbor(_NeighborValid5, _NeighborTex5, _NeighborUV5, _NeighborOffset5.xy, s, tMax, rgbSum, weightSum, alphaMask, glow);

                fixed4 result = own;
                if (weightSum > 1e-4)
                    result.rgb = lerp(own.rgb, rgbSum / weightSum, _BlendStrength * saturate(weightSum));

                result.a *= alphaMask;
                result *= i.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_GammaOut > 0.5) result.rgb = LinearToGammaSpace(result.rgb);
                #endif
                float hue = dot(_CellCenter.xy + s, float2(1.0, 0.8)) * _GridHueScale;
                float3 neon = HueToRgb(hue) * _GridColor.rgb;
                float gridMask = saturate(glow * _GridIntensity * _GridOn);
                result.rgb = lerp(result.rgb, neon, gridMask);

                float2 screenUv = i.screenPosition.xy / max(i.screenPosition.w, 0.0001);
                result.rgb = ApplyCartoonLook(result.rgb, result.a, screenUv);

                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
