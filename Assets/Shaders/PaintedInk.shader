Shader "Hidden/RetroLOTR/PaintedInk"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Painted Ink"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _EffectIntensity;
            float _InkStrength;
            float _InkRadius;
            float _PigmentStrength;
            float _GrainStrength;
            float _GrainScale;
            float _Warmth;
            float _Vignette;
            float _AnimationSpeed;

            float LuminancePaint(float3 color)
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

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1, 0)), local.x);
                float top = lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1.0), local.x);
                return lerp(bottom, top, local.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy * _InkRadius;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 color = source.rgb;

                float center = LuminancePaint(color);
                float left = LuminancePaint(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texel.x, 0)).rgb);
                float right = LuminancePaint(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texel.x, 0)).rgb);
                float down = LuminancePaint(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, texel.y)).rgb);
                float up = LuminancePaint(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, texel.y)).rgb);

                float edge = saturate((abs(left - right) + abs(down - up)) * 2.4);
                float darkEdgeBias = saturate(1.15 - center);
                color *= 1.0 - edge * darkEdgeBias * _InkStrength;

                float timeStep = floor(_Time.y * _AnimationSpeed * 6.0) / 6.0;
                float2 pigmentUv = uv * lerp(45.0, 115.0, saturate(_GrainScale));
                float pigment = ValueNoise(pigmentUv + float2(timeStep * 0.07, -timeStep * 0.04)) - 0.5;
                float pigmentMask = 1.0 - smoothstep(0.72, 1.0, center);
                color *= 1.0 + pigment * _PigmentStrength * pigmentMask;

                float2 pixel = floor(uv * _BlitTexture_TexelSize.zw / max(0.25, _GrainScale));
                float grain = Hash21(pixel + timeStep * float2(17.0, 31.0)) - 0.5;
                color += grain * _GrainStrength * (0.35 + pigmentMask * 0.65);

                float shadow = 1.0 - smoothstep(0.12, 0.82, center);
                color *= lerp(1.0.xxx, float3(1.055, 1.015, 0.925), _Warmth * (0.45 + shadow * 0.55));

                float2 vignetteUv = uv * 2.0 - 1.0;
                vignetteUv.x *= _BlitTexture_TexelSize.z / max(1.0, _BlitTexture_TexelSize.w);
                float vignette = smoothstep(0.35, 1.35, dot(vignetteUv, vignetteUv));
                color *= 1.0 - vignette * _Vignette;

                color = lerp(source.rgb, color, _EffectIntensity);
                return half4(max(color, 0.0), source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
