Shader "Custom/NeonLanduse"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.0, 0.4, 1.0, 0.6) // Medium Blue Translucent
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" } // Treat as solid floor
        LOD 100
        
        // Standard Alpha Blending (optional if opaque, but kept for alpha support)
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On // Write to depth buffer so it feels solid

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float4 _MainColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _MainColor;
            }
            ENDCG
        }
    }
}
