Shader "Custom/RadialWaveBlur"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}

        // Gaussian blur
        _BlurRadius ("Blur Radius (pixels)", Range(0, 20)) = 3.0

        // Radial wave distortion (distance-based and angle-based)
        _WaveCountRadial ("Wave Count Radial", Range(0, 8)) = 3
        _WaveCountAngular ("Wave Count Angular", Range(0, 8)) = 3
        _WaveAnimate ("Animate Waves", Float) = 1

        // Gaussian brightness distribution
        _CenterBrightnessMultiplier ("Center Brightness Multiplier", Range(1, 10)) = 1
        _BrightnessRange ("Brightness Range", Range(0.05, 0.5)) = 0.3
        _BrightnessOffset ("Brightness Center Offset", Range(-0.5, 0.5)) = 0
        _BrightnessFalloff ("Brightness Falloff Ratio", Range(0.05, 0.9)) = 0.5

        // Radial fade (from center to edge)
        _FadeStartRadius ("Fade Start Radius", Range(0, 1)) = 0.8
        _FadeEndAlpha ("Fade End Alpha (at edge)", Range(0, 1)) = 0
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

            float _BlurRadius;

            int _WaveCountRadial;
            int _WaveCountAngular;
            float _WaveAnimate;

            float _WaveStrengthsRadial[8];
            float _WaveFrequenciesRadial[8];
            float _WaveSpeedsRadial[8];

            float _WaveStrengthsAngular[8];
            float _WaveFrequenciesAngular[8];
            float _WaveSpeedsAngular[8];

            float _CenterBrightnessMultiplier;
            float _BrightnessRange;
            float _BrightnessOffset;
            float _BrightnessFalloff;

            float _FadeStartRadius;
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

                // Compute polar coordinates from UV center
                float2 center = float2(0.5, 0.5);
                float2 delta = i.uv - center;
                float distance = length(delta);

                // Prevent division by zero
                float eps = 0.0001;
                float2 radialDir = (distance > eps) ? delta / distance : float2(1.0, 0.0);
                float2 tangentDir = float2(-radialDir.y, radialDir.x);
                float angle = atan2(delta.y, delta.x);

                // Accumulate radial waves (distance-based)
                float waveRadial = 0;
                for (int j = 0; j < _WaveCountRadial; j++)
                    waveRadial += sin(distance * _WaveFrequenciesRadial[j] + t * _WaveSpeedsRadial[j]) * _WaveStrengthsRadial[j];

                // Accumulate angular waves (angle-based)
                float waveAngular = 0;
                for (int k = 0; k < _WaveCountAngular; k++)
                    waveAngular += cos(angle * _WaveFrequenciesAngular[k] + t * _WaveSpeedsAngular[k]) * _WaveStrengthsAngular[k];

                // Apply offset: radial direction + tangential direction
                float2 distortedUV = i.uv + radialDir * waveRadial + tangentDir * waveAngular;

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

                // Gaussian brightness: brighten center, fall off towards edges
                float brightnessSigma = _BrightnessRange / sqrt(2.0 * log(1.0 / _BrightnessFalloff));
                float brightnessGauss = exp(-0.5 * (distance * distance) / (brightnessSigma * brightnessSigma));
                col.rgb *= brightnessGauss * _CenterBrightnessMultiplier;

                // Radial fade (from center to edge)
                if (distance > _FadeStartRadius)
                {
                    float tRadius = saturate((distance - _FadeStartRadius) / (1.0 - _FadeStartRadius));
                    col.a *= lerp(1.0, _FadeEndAlpha, tRadius);
                }

                return col;
            }
            ENDCG
        }
    }
}
