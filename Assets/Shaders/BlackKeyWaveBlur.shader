Shader "Custom/BlackKeyWaveBlur"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}

        // Black-key removal
        _OpaquePoint ("Opaque Point", Range(0, 0.1)) = 0.01
        _TransparentPoint ("Transparent Point", Range(0, 0.1)) = 0.005
        _MaxAlphaValue ("Max Alpha", Range(0, 1)) = 1.0

        // Gaussian blur
        _BlurRadius ("Blur Radius (pixels)", Range(0, 20)) = 3.0

        // Wave distortion
        _WaveCountX ("Wave Count X", Range(1, 8)) = 3
        _WaveCountY ("Wave Count Y", Range(1, 8)) = 3
        _WaveAnimate ("Animate Waves", Float) = 1

        // Gaussian brightness distribution
        _CenterBrightnessMultiplier ("Center Brightness Multiplier", Range(1, 10)) = 1
        _BrightnessRange ("Brightness Range", Range(0.05, 0.5)) = 0.3
        _BrightnessOffset ("Brightness Center Offset", Range(-0.5, 0.5)) = 0
        _BrightnessFalloff ("Brightness Falloff Ratio", Range(0.05, 0.9)) = 0.5

        // Y-axis fade
        _FadeStartY ("Fade Start Y", Range(0, 1)) = 0.5
        _FadeEndAlpha ("Fade End Alpha (at y=1)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _OpaquePoint;
            float _TransparentPoint;
            float _MaxAlphaValue;
            float _BlurRadius;

            int _WaveCountX;
            int _WaveCountY;
            float _WaveAnimate;

            float _WaveStrengthsX[8];
            float _WaveFrequenciesX[8];
            float _WaveSpeedsX[8];

            float _WaveStrengthsY[8];
            float _WaveFrequenciesY[8];
            float _WaveSpeedsY[8];

            float _CenterBrightnessMultiplier;
            float _BrightnessRange;
            float _BrightnessOffset;
            float _BrightnessFalloff;

            float _FadeStartY;
            float _FadeEndAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = (_WaveAnimate > 0.5) ? _Time.y : 0.0;

                // Accumulate multi-wave UV offset
                float waveX = 0;
                float waveY = 0;
                for (int j = 0; j < _WaveCountY; j++)
                    waveX += sin(i.uv.y * _WaveFrequenciesY[j] + t * _WaveSpeedsY[j]) * _WaveStrengthsY[j];
                for (int k = 0; k < _WaveCountX; k++)
                    waveY += cos(i.uv.x * _WaveFrequenciesX[k] + t * _WaveSpeedsX[k]) * _WaveStrengthsX[k];

                float2 distortedUV = i.uv + float2(waveX, waveY);

                // 3x3 Gaussian blur sampled at distorted UV
                float2 ts = _MainTex_TexelSize.xy * _BlurRadius;

                fixed4 col =
                    tex2D(_MainTex, distortedUV + float2(-ts.x, -ts.y)) * 0.0625 +
                    tex2D(_MainTex, distortedUV + float2(  0.0, -ts.y)) * 0.125  +
                    tex2D(_MainTex, distortedUV + float2( ts.x, -ts.y)) * 0.0625 +
                    tex2D(_MainTex, distortedUV + float2(-ts.x,   0.0)) * 0.125  +
                    tex2D(_MainTex, distortedUV                        ) * 0.25   +
                    tex2D(_MainTex, distortedUV + float2( ts.x,   0.0)) * 0.125  +
                    tex2D(_MainTex, distortedUV + float2(-ts.x,  ts.y)) * 0.0625 +
                    tex2D(_MainTex, distortedUV + float2(  0.0,  ts.y)) * 0.125  +
                    tex2D(_MainTex, distortedUV + float2( ts.x,  ts.y)) * 0.0625;

                // Black-key removal
                float maxC = max(col.r, max(col.g, col.b));
                float alpha;
                if (maxC <= _TransparentPoint)
                    alpha = 0.0;
                else if (maxC >= _OpaquePoint)
                    alpha = _MaxAlphaValue;
                else
                    alpha = smoothstep(_TransparentPoint, _OpaquePoint, maxC) * _MaxAlphaValue;
                col.a = alpha;

                // Gaussian brightness: brighten center, fall off towards edges
                float center = 0.5 + _BrightnessOffset;
                float dx = i.uv.x - center;
                float sigma = _BrightnessRange / sqrt(2.0 * log(1.0 / _BrightnessFalloff));
                float gauss = exp(-0.5 * (dx * dx) / (sigma * sigma));
                col.rgb *= gauss * _CenterBrightnessMultiplier;

                // Y-axis fade
                if (i.uv.y > _FadeStartY)
                {
                    float tY = saturate((i.uv.y - _FadeStartY) / (1.0 - _FadeStartY));
                    col.a *= lerp(1.0, _FadeEndAlpha, tY);
                }

                return col;
            }
            ENDCG
        }
    }
}
