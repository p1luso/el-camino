Shader "Custom/NeonTrail"
{
    Properties
    {
        _MainColor ("Color", Color) = (1, 0, 0, 1)
        _GlowIntensity ("Glow Intensity", Float) = 3.0
        _PulseSpeed ("Pulse Speed", Float) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" } // Draw very late to be on top of other transparents
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha // Solid blending (not additive) for better visibility
        ZWrite Off
        ZTest LEqual // Don't draw through walls
        Offset -50, -50 // Very Aggressive offset to ensure it sits on top of road

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

            float4 _MainColor;
            float _GlowIntensity;
            float _PulseSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Pulse effect
                float pulse = 0.8 + 0.2 * sin(_Time.y * _PulseSpeed);
                
                float4 col = _MainColor * _GlowIntensity * pulse;
                col.a = _MainColor.a; // Use alpha from color
                
                return col;
            }
            ENDCG
        }
    }
}
