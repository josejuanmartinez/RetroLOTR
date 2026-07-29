Shader "Sprites/EdgeErode"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeWidth ("Erode Width (texels)", Range(0.1, 20)) = 3.0
        _AlphaThreshold ("Transparent Threshold", Range(0, 1)) = 0.5
        _Strength ("Removal Strength", Range(0, 1)) = 1.0
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
            #pragma fragment EdgeErodeSpriteFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            float _EdgeWidth;
            float _AlphaThreshold;
            float _Strength;
            float4 _MainTex_TexelSize;

            // Shrinks the sprite's opaque region inward from its silhouette:
            // any pixel within _EdgeWidth texels of a transparent neighbor gets
            // faded out, with the fade strengthening the closer it is to the
            // true boundary. Pixels deep inside the sprite are untouched.
            fixed4 EdgeErodeSpriteFrag(v2f IN) : SV_Target
            {
                fixed4 center = SampleSpriteTexture(IN.texcoord);

                const int R = 3;
                float2 step = _MainTex_TexelSize.xy * max(_EdgeWidth, 0.0001) / R;

                // Approximate distance (in search steps) from this pixel to the
                // nearest neighbor that's already below the transparency threshold.
                float minDist = R + 1; // sentinel: no transparent neighbor found
                UNITY_UNROLL
                for (int x = -R; x <= R; x++)
                {
                    UNITY_UNROLL
                    for (int y = -R; y <= R; y++)
                    {
                        float dist = length(float2(x, y));
                        if (dist > R || dist < 0.5) continue;
                        fixed a = SampleSpriteTexture(IN.texcoord + step * float2(x, y)).a;
                        if (a < _AlphaThreshold) minDist = min(minDist, dist);
                    }
                }

                // minDist can never actually be 0 (the pixel's own position is
                // skipped above), so the nearest possible detection is 1 step out.
                // Remap [1, R] -> [1, 0] so a pixel immediately next to a
                // transparent neighbor still reaches full (1.0) closeness/removal.
                float closeness = 1.0 - saturate((minDist - 1.0) / max((float)R - 1.0, 0.0001));
                float removal = saturate(closeness * _Strength);

                fixed4 result = center * IN.color;
                result.a *= (1.0 - removal);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
