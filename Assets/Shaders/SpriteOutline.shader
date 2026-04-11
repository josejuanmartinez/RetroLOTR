Shader "Sprites/Outline"
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
                float2 texel = _MainTex_TexelSize.xy * max(_OutlineSize, 0.0);

                float neighborAlpha = 0.0;
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + float2(texel.x, 0.0)).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + float2(-texel.x, 0.0)).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + float2(0.0, texel.y)).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + float2(0.0, -texel.y)).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + texel).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + float2(texel.x, -texel.y)).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord + float2(-texel.x, texel.y)).a);
                neighborAlpha = max(neighborAlpha, SampleSpriteTexture(IN.texcoord - texel).a);

                float outlineAlpha = saturate(neighborAlpha - centerAlpha) * _OutlineColor.a;
                fixed3 outlineRgb = _OutlineColor.rgb * outlineAlpha;

                fixed4 result;
                result.rgb = color.rgb + outlineRgb * (1.0 - color.a);
                result.a = saturate(color.a + outlineAlpha);
                return result;
            }
            ENDCG
        }
    }
}
