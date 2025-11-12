Shader "Custom/CutPaint"
{
  SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            // Previous mask (ping-pong source)
            TEXTURE2D(_PrevMask);
            SAMPLER(sampler_PrevMask);

            // Brush uniforms
            float2 _Center; // 0..1
            float  _Radius; // 0..1
            float  _Hardness; // 0..1

            Varyings vert(Attributes a)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(a.vertexID);
                o.uv          = GetFullScreenTriangleTexCoord(a.vertexID);
                return o;
            }

            half4 frag(Varyings i):SV_Target
            {
                // Read previous mask value (1=uncut, 0=fully cut)
                float prev = SAMPLE_TEXTURE2D(_PrevMask, sampler_PrevMask, i.uv).r;

                // Brush falloff
                float d       = distance(i.uv, _Center);
                float feather = max(1e-5, _Radius * (1 - _Hardness));
                float alpha   = 1 - smoothstep(_Radius - feather, _Radius, d); // 0..1

                // New mask: min(prev, 1 - alpha) removes material where the brush hits
                float newMask = min(prev, 1.0 - alpha);

                // Write to red (works for R8 and RGBA8)
                return half4(newMask, newMask, newMask, 1);
            }
            ENDHLSL
        }
    }
}
