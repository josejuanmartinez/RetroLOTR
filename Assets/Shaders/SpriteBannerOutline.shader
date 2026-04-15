Shader "Sprites/BannerOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size", Float) = 1.0
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
            #pragma fragment OutlineSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed4 _OutlineColor;
            float _OutlineSize;
            float4 _MainTex_TexelSize;

            fixed4 OutlineSpriteFrag(v2f IN) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(IN.texcoord) * IN.color;
                color.rgb *= color.a;

                float centerAlpha = color.a;
                float outlineRadius = max(_OutlineSize, 1.0);
                float2 stepBase = _MainTex_TexelSize.xy;

                float dilatedAlpha = 0.0;
                [unroll]
                for (int ring = 1; ring <= 3; ring++)
                {
                    float radius = outlineRadius * (ring / 3.0);
                    float2 offsetX = stepBase * float2(radius, 0.0);
                    float2 offsetY = stepBase * float2(0.0, radius);
                    float2 offsetD = stepBase * float2(radius, radius);
                    float2 offsetA = stepBase * float2(radius, -radius);

                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord + offsetX).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord - offsetX).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord + offsetY).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord - offsetY).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord + offsetD).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord - offsetD).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord + offsetA).a);
                    dilatedAlpha = max(dilatedAlpha, SampleSpriteTexture(IN.texcoord - offsetA).a);
                }

                float silhouetteOutline = saturate(dilatedAlpha - centerAlpha);

                float2 edgeDist = min(IN.texcoord, 1.0 - IN.texcoord);
                float2 borderPx = _MainTex_TexelSize.xy * max(_OutlineSize * 2.5, 0.0);
                float borderAlphaX = borderPx.x > 0.0 ? 1.0 - smoothstep(borderPx.x * 0.15, borderPx.x, edgeDist.x) : 0.0;
                float borderAlphaY = borderPx.y > 0.0 ? 1.0 - smoothstep(borderPx.y * 0.15, borderPx.y, edgeDist.y) : 0.0;
                float borderAlpha = max(borderAlphaX, borderAlphaY);

                float outlineAlpha = saturate((silhouetteOutline * 1.8) + (borderAlpha * 0.65)) * _OutlineColor.a;
                fixed3 outlineRgb = _OutlineColor.rgb;

                float finalAlpha = saturate(max(color.a, outlineAlpha));
                fixed3 outlinePremult = outlineRgb * outlineAlpha;

                fixed4 result;
                result.rgb = saturate(color.rgb + outlinePremult);
                result.a = finalAlpha;
                return result;
            }
            ENDCG
        }
    }
}
