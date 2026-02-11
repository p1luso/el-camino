Shader "Custom/NeonRoad"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1.0, 0.9, 0.0, 0.6) // Yellow Translucent Base
        _EdgeColor ("Edge Color", Color) = (1.0, 1.0, 0.5, 1.0) // Brighter Edge
        _GlowIntensity ("Glow Intensity", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" } 
        LOD 100
        
        // Standard Alpha Blending
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

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
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            float4 _MainColor;
            float4 _EdgeColor;
            float _GlowIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float3 viewDir = normalize(i.viewDir);

                // --- Fresnel/Rim for Edges ---
                float NdotV = saturate(dot(normal, viewDir));
                float rim = 1.0 - NdotV;
                rim = pow(rim, 2.0); // Soft rim

                // --- Base ---
                float4 finalColor = _MainColor;
                
                // Add Glow at edges
                finalColor.rgb += _EdgeColor.rgb * rim * _GlowIntensity;
                
                // Increase opacity at edges
                finalColor.a = max(finalColor.a, rim * 0.9);

                return finalColor;
            }
            ENDCG
        }
    }
}
