Shader "Custom/PatternOverlay"
{
   Properties
    {
        _PatternTex("Pattern", 2D) = "white" {}
        _LineTint("Line Tint", Color) = (1,1,1,1)
        _Alpha("Alpha", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "PatternLines"
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LineTint;
                float  _Alpha;
            CBUFFER_END

            TEXTURE2D(_PatternTex); SAMPLER(sampler_PatternTex);

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
                half4 t = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, i.uv);
                // Show only line pixels: assume lines are dark (RGB near 0) and alpha > 0
                half isLine = (t.a > 0.1 && dot(t.rgb, half3(0.333,0.333,0.333)) < 0.3) ? 1.0 : 0.0;
                half a = isLine * _Alpha;
                return half4(_LineTint.rgb, a);
            }
            ENDHLSL
        }
    }
}
