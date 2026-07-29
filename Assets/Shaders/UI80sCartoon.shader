Shader "RetroLOTR/UI/80s Cartoon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _CelBands ("Cel Bands", Range(2, 12)) = 6
        _OutlineStrength ("Ink Contours", Range(0, 2)) = 0.75
        _ColorSimplification ("Color Simplification", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0.5, 1.5)) = 1.08
        _BroadcastTexture ("Broadcast Texture", Range(0, 0.15)) = 0.025
        _EffectIntensity ("Effect Intensity", Range(0, 1)) = 0.85

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "80s Cartoon UI"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _CelBands;
            float _OutlineStrength;
            float _ColorSimplification;
            float _Saturation;
            float _BroadcastTexture;
            float _EffectIntensity;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                output.screenPosition = ComputeScreenPos(output.vertex);
                return output;
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

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 original = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                float luminance = LuminanceCartoon(original.rgb);

                float bands = max(2.0, _CelBands);
                float quantizedLuminance = floor(luminance * bands + 0.5) / bands;
                float3 cartoon = quantizedLuminance.xxx + (original.rgb - luminance.xxx);

                float paletteSteps = lerp(32.0, 8.0, _ColorSimplification);
                cartoon = floor(saturate(cartoon) * paletteSteps + 0.5) / paletteSteps;
                float cartoonLuminance = LuminanceCartoon(cartoon);
                cartoon = cartoonLuminance.xxx +
                    (cartoon - cartoonLuminance.xxx) * _Saturation;

                // Screen derivatives create contours without sampling outside a sprite's atlas rect.
                float contourGradient = abs(ddx(luminance)) + abs(ddy(luminance));
                float alphaGradient = abs(ddx(original.a)) + abs(ddy(original.a));
                float contour = smoothstep(0.025, 0.16, contourGradient + alphaGradient * 0.35);
                cartoon *= 1.0 - contour * saturate(_OutlineStrength) * 0.72;

                float2 screenUv = input.screenPosition.xy / max(input.screenPosition.w, 0.0001);
                float2 broadcastCell = floor(screenUv * _ScreenParams.xy * 0.55);
                float noise = Hash21(broadcastCell + floor(_Time.y * 12.0) * float2(7.0, 13.0)) - 0.5;
                float scanline = sin(screenUv.y * _ScreenParams.y * UNITY_PI) * 0.09;
                cartoon += (noise + scanline) * _BroadcastTexture;

                cartoon = lerp(float3(0.018, 0.021, 0.020),
                    float3(0.965, 0.955, 0.905), saturate(cartoon));

                fixed4 outputColor;
                outputColor.rgb = lerp(original.rgb, cartoon, _EffectIntensity);
                outputColor.a = original.a;

                #ifdef UNITY_UI_CLIP_RECT
                outputColor.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outputColor.a - 0.001);
                #endif

                outputColor.rgb *= outputColor.a;
                return outputColor;
            }
            ENDCG
        }
    }
}
