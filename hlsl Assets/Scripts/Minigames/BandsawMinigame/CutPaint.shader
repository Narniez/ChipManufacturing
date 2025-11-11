Shader "Custom/CutPaint"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            // Brush uniforms set from SawCutter
            float2 _Center;
            float  _Radius;
            float  _Hardness; // 0..1

            Varyings vert(Attributes a){
                Varyings o;
                o.positionHCS = a.positionOS;
                o.uv = a.uv;
                return o;
            }

            half4 frag(Varyings i):SV_Target
            {
                float d = distance(i.uv, _Center);
                float feather = max(1e-5, _Radius * (1 - _Hardness));
                float alpha = 1 - smoothstep(_Radius - feather, _Radius, d);
                // Paint black with alpha so mask darkens toward 0 (holes)
                return half4(0,0,0, alpha);
            }
            ENDHLSL
        }
    }
}