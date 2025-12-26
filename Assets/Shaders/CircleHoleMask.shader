Shader "Custom/CircleHoleMask"
{
    Properties
    {
        _Color ("Mask Color", Color) = (0,0,0,1)
        _Center ("Hole Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Hole Radius", Range(0,1)) = 0.25
        _Feather ("Edge Feather", Range(0,0.2)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            fixed4 _Color;
            float4 _Center;
            float _Radius;
            float _Feather;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 計算 UV 距離
                float dist = distance(i.uv, _Center.xy);

                // 圓形洞 Alpha（內 0，外 1）
                float alpha = smoothstep(
                    _Radius,
                    _Radius + _Feather,
                    dist
                );

                fixed4 col = _Color;
                col.a *= alpha;

                return col;
            }
            ENDCG
        }
    }
}
