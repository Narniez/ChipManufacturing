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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            float2 _Center;
            float  _Radius; 
            float  _Hardness;

            Varyings vert(Attributes a)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(a.vertexID);
                o.uv          = GetFullScreenTriangleTexCoord(a.vertexID);
                return o;
            }

            half4 frag(Varyings i):SV_Target
            {
                float d       = distance(i.uv, _Center);
                float feather = max(1e-5, _Radius * (1 - _Hardness));
                float alpha   = 1 - smoothstep(_Radius - feather, _Radius, d);
                return half4(0,0,0, alpha);
            }
            ENDHLSL
        }
    }
}