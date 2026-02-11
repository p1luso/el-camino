Shader "Custom/NeonSplat"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0,1,0,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        
        // ZWrite Off for transparency
        // ZTest Always or LEqual with Offset to appear on top of roads
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1

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

            fixed4 _MainColor;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Box calculation for "Road Paint" effect
                float2 uv = i.uv - 0.5;
                // Slightly rounded box
                float2 d = abs(uv);
                float box = max(d.x, d.y);
                
                // Solid center, soft edges only at the very end
                float alpha = 1.0 - smoothstep(0.4, 0.5, box);
                
                fixed4 col = _MainColor;
                col.a *= alpha;
                
                return col;
            }
            ENDCG
        }
    }
}
