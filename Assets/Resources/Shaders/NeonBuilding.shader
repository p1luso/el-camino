Shader "Custom/NeonBuilding"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.0, 1.0, 1.0, 0.5) // Cyan Base with Alpha
        _RimColor ("Rim Color", Color) = (0.8, 1.0, 1.0, 1) // White/Cyan Rim
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 3.0
        _EmissionGain ("Emission Gain", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // Standard Alpha Blending for "Translucent" look (Glass-like)
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On // Write depth to occlude trails behind buildings
        Cull Back // Only show front faces

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            float4 _MainColor;
            float4 _RimColor;
            float _RimPower;
            float _EmissionGain;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float3 viewDir = normalize(i.viewDir);
                
                // --- Rim Light (Fresnel) ---
                // Stronger at edges
                float NdotV = saturate(dot(normal, viewDir));
                float rim = 1.0 - NdotV;
                rim = pow(rim, _RimPower);
                
                // --- Composition ---
                // Base is the main color
                float4 finalColor = _MainColor;
                
                // Add Rim Emission (Additive boost to RGB, keep Alpha logic separate or combined)
                // We add rim color to the base rgb
                finalColor.rgb += _RimColor.rgb * rim * _EmissionGain;
                
                // Make edges more opaque?
                // Or keep uniform alpha. User wants "Solid color".
                // Let's boost alpha slightly at rim to define shape
                finalColor.a = max(finalColor.a, rim * 0.8);
                
                return finalColor;
            }
            ENDCG
        }
    }
}
