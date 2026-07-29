Shader "RetroLOTR/UI/Fog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background Texture", 2D) = "white" {}
        _Color ("UI Tint", Color) = (1,1,1,1)

        [Header(Painting Palette)]
        _ShadowColor ("Shadow", Color) = (0.020,0.028,0.027,1)
        _FogColor ("Fog", Color) = (0.245,0.285,0.260,1)
        _HighlightColor ("Mist Highlight", Color) = (0.470,0.475,0.405,1)
        _SourceInfluence ("Black / White Texture Influence", Range(0,1)) = 0.12
        _Brightness ("Brightness", Range(0,2)) = 0.85
        _Saturation ("Saturation", Range(0,1)) = 0.42

        [Header(Fog Motion)]
        _Scale ("Fog Scale", Range(0.5,12)) = 3.2
        _SpeedX ("Horizontal Speed", Range(-1,1)) = 0.035
        _SpeedY ("Vertical Speed", Range(-1,1)) = 0.008
        _Distortion ("Rolling Distortion", Range(0,2)) = 0.72
        _Density ("Fog Density", Range(0,1)) = 0.58
        _Contrast ("Fog Contrast", Range(0.1,3)) = 1.25

        [Header(1980s Painted Finish)]
        _PaintBands ("Paint Bands", Range(2,12)) = 6
        _BrushGrain ("Brush Grain", Range(0,0.2)) = 0.035
        _Vignette ("Edge Vignette", Range(0,1)) = 0.2

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
            Name "Painted Animated Fog"

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
            fixed4 _ShadowColor;
            fixed4 _FogColor;
            fixed4 _HighlightColor;
            float _SourceInfluence;
            float _Brightness;
            float _Saturation;
            float _Scale;
            float _SpeedX;
            float _SpeedY;
            float _Distortion;
            float _Density;
            float _Contrast;
            float _PaintBands;
            float _BrushGrain;
            float _Vignette;

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float FogNoise(float2 p)
            {
                float total = 0.0;
                total += ValueNoise(p) * 0.52;
                p = p * 2.03 + 17.1;
                total += ValueNoise(p) * 0.27;
                p = p * 2.01 + 9.7;
                total += ValueNoise(p) * 0.14;
                p = p * 2.04 + 5.3;
                total += ValueNoise(p) * 0.07;
                return total;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                float2 screenUv = input.screenPosition.xy / max(input.screenPosition.w, 0.0001);
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 p = (screenUv - 0.5) * float2(aspect, 1.0) * _Scale;
                float2 drift = float2(_SpeedX, _SpeedY) * _Time.y;

                float warpA = FogNoise(p * 0.48 + drift * 0.65);
                float warpB = FogNoise(p * 0.41 - drift * 0.42 + 31.7);
                float2 warp = (float2(warpA, warpB) - 0.5) * _Distortion;
                float broadFog = FogNoise(p + drift + warp);
                float wisps = FogNoise(p * float2(1.7, 0.72) - drift * 1.35 - warp * 0.55);
                float fog = broadFog * 0.68 + wisps * 0.32;

                fog = saturate((fog - (1.0 - _Density)) * _Contrast + 0.5);
                float bands = max(2.0, _PaintBands);
                float bandedFog = floor(fog * bands + 0.5) / bands;
                fog = lerp(fog, bandedFog, 0.44);

                float3 painted = lerp(_ShadowColor.rgb, _FogColor.rgb, smoothstep(0.08, 0.68, fog));
                painted = lerp(painted, _HighlightColor.rgb, smoothstep(0.68, 1.0, fog));

                float sourceLuma = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                painted *= lerp(1.0, lerp(0.58, 1.22, sourceLuma), _SourceInfluence);

                float luma = dot(painted, float3(0.2126, 0.7152, 0.0722));
                painted = lerp(luma.xxx, painted, _Saturation) * _Brightness;

                float2 grainCell = floor(screenUv * _ScreenParams.xy * 0.32);
                float grain = Hash21(grainCell + floor(_Time.y * 4.0) * float2(3, 7)) - 0.5;
                painted += grain * _BrushGrain * (0.45 + fog * 0.55);

                float2 edge = abs(screenUv * 2.0 - 1.0);
                float vignette = smoothstep(0.35, 1.25, length(edge));
                painted *= 1.0 - vignette * _Vignette;

                fixed4 outputColor = fixed4(saturate(painted), source.a);

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
