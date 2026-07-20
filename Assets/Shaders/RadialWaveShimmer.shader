Shader "Custom/RadialWaveShimmer"
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

        // Water shimmer effect
        _ShimmerIntensity ("Shimmer Intensity", Range(0, 2)) = 0.35
        _ShimmerThreshold ("Shimmer Threshold (crest height)", Range(0, 1)) = 0.5
        _ShimmerSoftness ("Shimmer Softness", Range(0.01, 1)) = 0.1

        // Noise that breaks up the shimmer rings
        _NoiseScale ("Noise Scale", Range(1, 40)) = 10
        _NoiseSpeed ("Noise Drift Speed", Range(0, 2)) = 0.5
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.5

        // Large-scale patches: only some regions sparkle at a time
        _PatchScale ("Patch Scale", Range(0.5, 10)) = 10
        _PatchSpeed ("Patch Drift Speed", Range(0, 1)) = 0.5
        _PatchAmount ("Patch Amount", Range(0, 1)) = 0.5
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

            float _ShimmerIntensity;
            float _ShimmerThreshold;
            float _ShimmerSoftness;

            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseAmount;

            float _PatchScale;
            float _PatchSpeed;
            float _PatchAmount;

            // Cheap hash-based 2D value noise (no texture needed)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float2 u = fp * fp * (3.0 - 2.0 * fp); // smooth interpolation
                float a = hash21(ip);
                float b = hash21(ip + float2(1, 0));
                float c = hash21(ip + float2(0, 1));
                float d = hash21(ip + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

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

                // Accumulate radial waves (distance-based); also track max possible amplitude
                float waveRadial = 0;
                float strengthSum = 0;
                for (int j = 0; j < _WaveCountRadial; j++)
                {
                    waveRadial += sin(distance * _WaveFrequenciesRadial[j] + t * _WaveSpeedsRadial[j]) * _WaveStrengthsRadial[j];
                    strengthSum += abs(_WaveStrengthsRadial[j]);
                }

                // Accumulate directional plane waves (replaces angular waves).
                // Plane waves have uniform wavelength everywhere in UV space, so
                // there is no grain-size collapse near the center and no seam at
                // the +/-pi angle boundary. Directions are spread evenly using the
                // golden angle so the 8 waves cross each other irregularly.
                float wavePlanar = 0;
                float2 planarOffset = float2(0.0, 0.0);
                for (int k = 0; k < _WaveCountAngular; k++)
                {
                    float phi = (float)k * 2.399963; // golden angle (radians)
                    float2 dir = float2(cos(phi), sin(phi));
                    float w = sin(dot(delta, dir) * _WaveFrequenciesAngular[k] + t * _WaveSpeedsAngular[k]) * _WaveStrengthsAngular[k];
                    wavePlanar += w;
                    planarOffset += dir * w; // each wave displaces along its own direction
                    strengthSum += abs(_WaveStrengthsAngular[k]);
                }

                // Apply offset: radial ripple + directional plane waves
                float2 distortedUV = i.uv + radialDir * waveRadial + planarOffset;

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

                // Water shimmer: glints appear where waves constructively interfere.
                // Normalize combined wave height to ~[-1, 1]; only crests that rise
                // above the threshold light up, with a soft smoothstep transition,
                // producing sparse soft sparkles instead of a hard grid.
                float waveHeight = (waveRadial + wavePlanar) / max(strengthSum, 1e-5);

                // Drifting 2D noise perturbs the crest height per-pixel, eating
                // irregular holes into the rings so glints become scattered patches.
                float n = valueNoise(i.uv * _NoiseScale + t * _NoiseSpeed * float2(0.7, 0.4));
                waveHeight -= (n - 0.5) * 2.0 * _NoiseAmount;

                float glint = smoothstep(_ShimmerThreshold, _ShimmerThreshold + _ShimmerSoftness, waveHeight);

                // Large-scale patch modulation: a second, low-frequency noise layer
                // gates the sparkle so only some drifting regions shimmer at a time,
                // like wind patches on real water. Drift direction is different from
                // the fine noise so the two layers never move in lockstep.
                float patch = valueNoise(i.uv * _PatchScale + t * _PatchSpeed * float2(-0.3, 0.5));
                float patchMask = smoothstep(0.35, 0.75, patch);
                glint *= lerp(1.0, patchMask, _PatchAmount);

                // Composite the glint as an independent white layer, decoupled from
                // the underlying color: push rgb toward white AND raise alpha so
                // sparkles remain visible over black or fully transparent pixels.
                float glintAmount = saturate(glint * _ShimmerIntensity);
                col.rgb = lerp(col.rgb, float3(1.0, 1.0, 1.0), glintAmount);
                col.a = max(col.a, glintAmount);

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
