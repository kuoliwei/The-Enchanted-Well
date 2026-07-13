Shader "Custom/BlackKeySmoothBlur"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}
        _OpaquePoint ("Opaque Point", Range(0, 0.1)) = 0.01
        _TransparentPoint ("Transparent Point", Range(0, 0.1)) = 0.005
        _MaxAlphaValue ("Max Alpha", Range(0, 1)) = 1.0
        _BlurRadius ("Blur Radius (pixels)", Range(0, 20)) = 3.0
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
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)

            float _OpaquePoint;
            float _TransparentPoint;
            float _MaxAlphaValue;
            float _BlurRadius;

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 3x3 Gaussian blur (weights sum to 1.0: corners=1/16, edges=1/8, center=1/4)
                float2 ts = _MainTex_TexelSize.xy * _BlurRadius;

                fixed4 col =
                    tex2D(_MainTex, i.uv + float2(-ts.x, -ts.y)) * 0.0625 +
                    tex2D(_MainTex, i.uv + float2(  0.0, -ts.y)) * 0.125  +
                    tex2D(_MainTex, i.uv + float2( ts.x, -ts.y)) * 0.0625 +
                    tex2D(_MainTex, i.uv + float2(-ts.x,   0.0)) * 0.125  +
                    tex2D(_MainTex, i.uv                        ) * 0.25   +
                    tex2D(_MainTex, i.uv + float2( ts.x,   0.0)) * 0.125  +
                    tex2D(_MainTex, i.uv + float2(-ts.x,  ts.y)) * 0.0625 +
                    tex2D(_MainTex, i.uv + float2(  0.0,  ts.y)) * 0.125  +
                    tex2D(_MainTex, i.uv + float2( ts.x,  ts.y)) * 0.0625;

                // Black-key removal (same logic as BlackKeySmooth)
                float maxC = max(col.r, max(col.g, col.b));

                float alpha;
                if (maxC <= _TransparentPoint)
                    alpha = 0.0;
                else if (maxC >= _OpaquePoint)
                    alpha = _MaxAlphaValue;
                else
                    alpha = smoothstep(_TransparentPoint, _OpaquePoint, maxC) * _MaxAlphaValue;

                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
}
