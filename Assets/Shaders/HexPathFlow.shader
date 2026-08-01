// Animated "fluid" flow effect for the hex movement path (HexPathRenderer's LineRenderer).
// Vertex color comes from the LineRenderer's own color gradient (tint/fade authored in the
// Inspector) and is multiplied in as-is. UV.x runs continuously along the path length (the
// LineRenderer must use Tile texture mode, not RepeatPerSegment, or the pulse restarts on every
// tiny spline segment), so a moving band computed from UV.x - time reads as a pulse of light
// travelling from the path's start hex toward its end hex.
Shader "RetroLOTR/HexPathFlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Tint", Color) = (0.35, 0.65, 0.95, 0.45)
        _FlowColor ("Flow Highlight", Color) = (1, 0.92, 0.65, 1)
        _FlowSpeed ("Flow Speed (tiles/sec)", Float) = 1.2
        _FlowDensity ("Flow Density (bands per tile)", Float) = 1
        _FlowSharpness ("Flow Sharpness", Range(1, 32)) = 6
        _BaseAlpha ("Base Alpha (path visible between pulses)", Range(0, 1)) = 0.35
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _FlowColor;
            float _FlowSpeed;
            float _FlowDensity;
            float _FlowSharpness;
            float _BaseAlpha;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // Triangle wave in [0,1] sharpened into a travelling pulse; higher _FlowSpeed/
                // _FlowDensity move the pulse faster / pack pulses closer along the path.
                float phase = frac(i.uv.x * _FlowDensity - _Time.y * _FlowSpeed);
                float pulse = pow(saturate(1.0 - abs(phase - 0.5) * 2.0), _FlowSharpness);

                fixed3 rgb = lerp(_Color.rgb, _FlowColor.rgb, pulse) * tex.rgb;
                fixed alpha = (_BaseAlpha + (1.0 - _BaseAlpha) * pulse) * _Color.a * tex.a;

                fixed4 result;
                result.rgb = rgb * i.color.rgb;
                result.a = alpha * i.color.a;
                return result;
            }
            ENDCG
        }
    }
}
