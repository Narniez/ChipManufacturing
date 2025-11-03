Shader "Custom/MetalPlateCutShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CutMask ("Cut Mask", 2D) = "black" {}
        _CutColor ("Cut Color", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CutMask;
            fixed4 _CutColor;

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
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 cut = tex2D(_CutMask, i.uv);

                // Where mask is white, show the cut color (or transparent)
                float cutStrength = cut.r;
                col.rgb = lerp(col.rgb, _CutColor.rgb, cutStrength);
                col.a *= (1.0 - cutStrength); // fade out cut areas

                return col;
            }
            ENDCG
        }
    }
}
