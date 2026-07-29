Shader "Sprites/EdgeDistort"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Detect Width (texels)", Float) = 2.0
        _DistortAmount ("Distort Amount (texels)", Float) = 4.0
        _NoiseScale ("Wave Size (UV, bigger = smoother)", Float) = 0.25
        _DistortSpeed ("Distort Speed (0 = static)", Float) = 0.0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment EdgeDistortSpriteFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            float _EdgeWidth;
            float _DistortAmount;
            float _NoiseScale;
            float _DistortSpeed;
            float4 _MainTex_TexelSize;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth value noise, cheap enough for a per-pixel warp field.
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 EdgeDistortSpriteFrag(v2f IN) : SV_Target
            {
                float2 detectTexel = _MainTex_TexelSize.xy * max(_EdgeWidth, 0.0);

                // How close this pixel is to a transparency boundary: sample a small
                // neighborhood and see how much the alpha varies. 0 = flat interior
                // or flat background, 1 = right on a silhouette edge.
                fixed minA = 1;
                fixed maxA = 0;
                UNITY_UNROLL
                for (int x = -1; x <= 1; x++)
                {
                    UNITY_UNROLL
                    for (int y = -1; y <= 1; y++)
                    {
                        fixed a = SampleSpriteTexture(IN.texcoord + detectTexel * float2(x, y)).a;
                        minA = min(minA, a);
                        maxA = max(maxA, a);
                    }
                }
                // Smoothstep instead of a hard threshold so the distortion fades in
                // continuously rather than switching on abruptly at the boundary.
                float edgeFactor = smoothstep(0.05, 0.5, maxA - minA);

                // Low-frequency, resolution-independent flow field (UV-space, not
                // texel-space) so wave size only depends on _NoiseScale, never on
                // the sprite's pixel dimensions - this is what was reading as grain.
                float2 seed = IN.texcoord / max(_NoiseScale, 0.0001) + _DistortSpeed * _Time.y;
                float nx = valueNoise(seed) * 2.0 - 1.0;
                float ny = valueNoise(seed + float2(37.2, 17.7)) * 2.0 - 1.0;
                float2 warp = float2(nx, ny) * _MainTex_TexelSize.xy * _DistortAmount * edgeFactor;

                // Average several taps along the flow direction (premultiplied) so
                // the warp reads as a soft, continuous smear rather than a single
                // sharply-relocated texel. When edgeFactor is 0 (interior) every
                // tap lands on the same texel, so this is a no-op away from edges.
                const int TAPS = 6;
                fixed3 colorSum = fixed3(0, 0, 0);
                fixed alphaSum = 0;
                UNITY_UNROLL
                for (int i = 0; i < TAPS; i++)
                {
                    float t = i / (float)(TAPS - 1);
                    fixed4 s = SampleSpriteTexture(IN.texcoord + warp * t);
                    colorSum += s.rgb * s.a;
                    alphaSum += s.a;
                }
                colorSum /= TAPS;
                alphaSum /= TAPS;

                fixed4 result;
                result.rgb = (alphaSum > 0.0001) ? colorSum / alphaSum : fixed3(0, 0, 0);
                result.rgb *= IN.color.rgb;
                result.a = alphaSum * IN.color.a;
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
