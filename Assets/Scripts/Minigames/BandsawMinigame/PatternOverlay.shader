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

                // Same mapping uniforms as PlateCutReveal
                float4 _PlateUVMin;
                float4 _PlateUVSize;
                float4 _UAxisMask;
                float4 _VAxisMask;
                float  _InvertV;
            CBUFFER_END

            TEXTURE2D(_PatternTex); SAMPLER(sampler_PatternTex);
            float4 _PatternTex_TexelSize; // x=1/w, y=1/h, z=w, w=h

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 posOS:TEXCOORD0; };

            Varyings vert(Attributes v){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.posOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i):SV_Target
            {
                // Bounds-based UVs (identical to PlateCutReveal)
                float uMin  = dot(_PlateUVMin.xyz,  _UAxisMask.xyz);
                float uSize = max(1e-5, dot(_PlateUVSize.xyz, _UAxisMask.xyz));
                float vMin  = dot(_PlateUVMin.xyz,  _VAxisMask.xyz);
                float vSize = max(1e-5, dot(_PlateUVSize.xyz, _VAxisMask.xyz));
                float u = (dot(i.posOS, _UAxisMask.xyz) - uMin) / uSize;
                float v = (dot(i.posOS, _VAxisMask.xyz) - vMin) / vSize;
                if (_InvertV > 0.5) v = 1.0 - v;
                float2 uvPlate = float2(u, v);

                // Letterbox the pattern to preserve its aspect within plate UV 0..1
                float texAspect = max(1e-6, _PatternTex_TexelSize.z / _PatternTex_TexelSize.w);
                float2 scale = float2(1.0, 1.0);
                if (texAspect > 1.0) scale.y = 1.0 / texAspect; else scale.x = texAspect;
                float2 uv = (uvPlate - 0.5) * scale + 0.5;

                // Outside letterboxed region = transparent
                if (any(uv < 0.0) || any(uv > 1.0)) return half4(0,0,0,0);

                half4 t = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, uv);
                half isLine = (t.a > 0.1 && dot(t.rgb, half3(0.333,0.333,0.333)) < 0.3) ? 1.0 : 0.0;
                half a = isLine * _Alpha;
                return half4(_LineTint.rgb, a);
            }
            ENDHLSL
        }
    }
}
