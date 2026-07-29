Shader "Hidden/RetroLOTR/CardArtStyles"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Card Art Styles"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Style;
            float _EffectIntensity;
            float _MotionPixels;
            float _MotionScale;
            float _MotionSpeed;
            float _WarmHalation;
            float _MisregistrationPixels;
            float _PrintVariation;
            float _BrushRadius;
            float _CelBands;
            float _CartoonOutline;
            float _CartoonOutlineRadius;
            float _ColorSimplification;
            float _BroadcastTexture;
            float _CartoonSaturation;

            float ArtLuminance(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);
                float a = lerp(Hash21(cell), Hash21(cell + float2(1, 0)), local.x);
                float b = lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1.0), local.x);
                return lerp(a, b, local.y);
            }

            float EdgeStrength(float2 uv)
            {
                float2 texel = _BlitTexture_TexelSize.xy;
                float left = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texel.x, 0)).rgb);
                float right = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texel.x, 0)).rgb);
                float down = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, texel.y)).rgb);
                float up = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, texel.y)).rgb);
                return saturate((abs(left - right) + abs(down - up)) * 3.0);
            }

            float3 LivingIllustration(float2 uv, float3 source)
            {
                float time = _Time.y * _MotionSpeed;
                float2 noiseUv = uv * _MotionScale;
                float2 flow;
                flow.x = ValueNoise(noiseUv + float2(time, -time * 0.37));
                flow.y = ValueNoise(noiseUv + float2(31.7 - time * 0.29, 17.1 + time * 0.71));
                flow = flow * 2.0 - 1.0;

                float luminance = ArtLuminance(source);
                float saturation = max(source.r, max(source.g, source.b)) - min(source.r, min(source.g, source.b));
                float stableEdges = 1.0 - EdgeStrength(uv);
                float paintedArea = saturate(0.22 + saturation * 1.8) *
                    smoothstep(0.025, 0.24, luminance) * stableEdges * stableEdges;

                float2 displacedUv = uv + flow * _BlitTexture_TexelSize.xy *
                    _MotionPixels * paintedArea;
                float3 moved = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displacedUv).rgb;

                float2 glowTexel = _BlitTexture_TexelSize.xy * 2.5;
                float3 glow =
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(glowTexel.x, 0)).rgb +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(glowTexel.x, 0)).rgb +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, glowTexel.y)).rgb +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, glowTexel.y)).rgb;
                glow *= 0.25;
                glow = max(glow - 0.72, 0.0) * float3(1.0, 0.62, 0.27);

                return moved + glow * _WarmHalation;
            }

            float3 VintagePrint(float2 uv, float3 source)
            {
                float edge = EdgeStrength(uv);
                float2 direction = normalize(float2(0.87, 0.49));
                float2 offset = direction * _BlitTexture_TexelSize.xy *
                    _MisregistrationPixels * (0.35 + edge * 0.65);

                float3 positive = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).rgb;
                float3 negative = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset).rgb;
                float3 registered = float3(positive.r, source.g, negative.b);

                float2 paperCell = floor(uv * _BlitTexture_TexelSize.zw / 3.0);
                float paper = Hash21(paperCell) - 0.5;
                float luminance = ArtLuminance(source);
                registered *= 1.0 + paper * _PrintVariation * (0.35 + luminance * 0.65);

                return registered;
            }

            void QuadrantStatistics(float2 uv, float2 sx, float2 sy, out float3 mean, out float variance)
            {
                float2 texel = _BlitTexture_TexelSize.xy * _BrushRadius;
                float3 c0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float3 c1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sx * texel).rgb;
                float3 c2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sy * texel).rgb;
                float3 c3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + (sx + sy) * texel).rgb;
                mean = (c0 + c1 + c2 + c3) * 0.25;

                float l0 = ArtLuminance(c0);
                float l1 = ArtLuminance(c1);
                float l2 = ArtLuminance(c2);
                float l3 = ArtLuminance(c3);
                float lm = (l0 + l1 + l2 + l3) * 0.25;
                variance = (pow(l0 - lm, 2.0) + pow(l1 - lm, 2.0) +
                    pow(l2 - lm, 2.0) + pow(l3 - lm, 2.0)) * 0.25;
            }

            float3 PainterlySimplification(float2 uv)
            {
                float3 m0, m1, m2, m3;
                float v0, v1, v2, v3;
                QuadrantStatistics(uv, float2(1, 0), float2(0, 1), m0, v0);
                QuadrantStatistics(uv, float2(-1, 0), float2(0, 1), m1, v1);
                QuadrantStatistics(uv, float2(1, 0), float2(0, -1), m2, v2);
                QuadrantStatistics(uv, float2(-1, 0), float2(0, -1), m3, v3);

                float3 result = m0;
                float lowestVariance = v0;
                if (v1 < lowestVariance) { result = m1; lowestVariance = v1; }
                if (v2 < lowestVariance) { result = m2; lowestVariance = v2; }
                if (v3 < lowestVariance) { result = m3; }
                return result;
            }

            float3 EightiesCartoon(float2 uv, float3 source)
            {
                float2 texel = _BlitTexture_TexelSize.xy;
                float2 smoothTexel = texel * 1.35;
                float3 smoothed =
                    source * 0.40 +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(smoothTexel.x, 0)).rgb * 0.15 +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(smoothTexel.x, 0)).rgb * 0.15 +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, smoothTexel.y)).rgb * 0.15 +
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, smoothTexel.y)).rgb * 0.15;

                float luminance = ArtLuminance(smoothed);
                float quantizedLuminance = floor(luminance * _CelBands + 0.5) / _CelBands;
                float3 chroma = smoothed - luminance.xxx;
                float3 celColor = quantizedLuminance.xxx + chroma;

                float paletteSteps = lerp(32.0, 8.0, _ColorSimplification);
                celColor = floor(saturate(celColor) * paletteSteps + 0.5) / paletteSteps;

                float celLuminance = ArtLuminance(celColor);
                celColor = celLuminance.xxx + (celColor - celLuminance.xxx) * _CartoonSaturation;

                float2 outlineTexel = texel * _CartoonOutlineRadius;
                float l = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(outlineTexel.x, 0)).rgb);
                float r = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(outlineTexel.x, 0)).rgb);
                float d = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, outlineTexel.y)).rgb);
                float u = ArtLuminance(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, outlineTexel.y)).rgb);
                float contour = smoothstep(0.035, 0.24, abs(l - r) + abs(d - u));
                celColor *= 1.0 - contour * saturate(_CartoonOutline) * 0.78;

                float2 broadcastCell = floor(uv * _BlitTexture_TexelSize.zw * 0.55);
                float analogNoise = Hash21(broadcastCell + floor(_Time.y * 12.0) * float2(7.0, 13.0)) - 0.5;
                float scanline = sin(uv.y * _BlitTexture_TexelSize.w * 3.14159265) * 0.5;
                celColor += (analogNoise + scanline * 0.18) * _BroadcastTexture;

                // Broadcast-era transfers rarely held absolute black or paper white.
                return lerp(float3(0.018, 0.021, 0.020), float3(0.965, 0.955, 0.905), saturate(celColor));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 styled;

                if (_Style < 0.5)
                    styled = LivingIllustration(uv, source.rgb);
                else if (_Style < 1.5)
                    styled = VintagePrint(uv, source.rgb);
                else if (_Style < 2.5)
                    styled = PainterlySimplification(uv);
                else
                    styled = EightiesCartoon(uv, source.rgb);

                return half4(max(lerp(source.rgb, styled, _EffectIntensity), 0.0), source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
