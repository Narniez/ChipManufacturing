Shader "Custom/PlateCutReveal"
{
    Properties
    {
        _BaseColor("Plate Color", Color) = (0.8,0.8,0.8,1)
        _CutMask("Cut Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20" }

        Pass
        {
            Name "PlateReveal"
            Tags { "LightMode"="SRPDefaultUnlit" }

            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_CutMask);
            SAMPLER(sampler_CutMask);

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            Varyings vert(Attributes v){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i):SV_Target
            {
                float mask = SAMPLE_TEXTURE2D(_CutMask, sampler_CutMask, i.uv).r;
                // Debug: visualize mask values
                // return half4(mask, mask, mask, 1);
                clip(mask - 0.5);
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}