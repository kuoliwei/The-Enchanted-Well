Shader "Custom/BlackKeySmooth"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}

        // 上限（完全不透明點）
        _OpaquePoint ("Opaque Point", Range(0, 0.1)) = 0.01

        // 下限（完全透明點）
        _TransparentPoint ("Transparent Point", Range(0, 0.1)) = 0.005

        // 不透明度最大值
        _MaxAlphaValue ("Max Alpha", Range(0, 1)) = 1.0
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

            float _OpaquePoint;       // 0.01
            float _TransparentPoint;  // 0.005
            float _MaxAlphaValue;     // 1.0 by default

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
                fixed4 col = tex2D(_MainTex, i.uv);

                // 使用 max channel 判斷像素黑度
                float maxC = max(col.r, max(col.g, col.b));

                float alpha;

                if (maxC <= _TransparentPoint)
                {
                    // 完全透明
                    alpha = 0.0;
                }
                else if (maxC >= _OpaquePoint)
                {
                    // 完全不透明（MaxAlphaValue）
                    alpha = _MaxAlphaValue;
                }
                else
                {
                    // 使用 smoothstep 讓透明度平滑過渡
                    float t = smoothstep(_TransparentPoint, _OpaquePoint, maxC);

                    alpha = t * _MaxAlphaValue;
                }

                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
}
