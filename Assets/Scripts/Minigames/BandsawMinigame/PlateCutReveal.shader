Shader "Custom/PlateCutReveal"
{
Properties
  {
      _BaseColor("Plate Color", Color) = (0.8,0.8,0.8,1)
      _CutMask("Cut Mask", 2D) = "white" {}
      _DebugMode("Debug Mode (0=Normal,1=UV,2=Mask)", Float) = 0
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

              // Bounds mapping from SawCutter
              float4 _PlateUVMin;
              float4 _PlateUVSize;
              float4 _UAxisMask;
              float4 _VAxisMask;
              float  _InvertV;
              float  _DebugMode;
          CBUFFER_END

          TEXTURE2D(_CutMask);
          SAMPLER(sampler_CutMask);

          struct Attributes { float4 positionOS : POSITION; };
          struct Varyings
          {
              float4 positionHCS : SV_POSITION;
              float3 posOS       : TEXCOORD0; // object-space position
          };

          Varyings vert(Attributes v)
          {
              Varyings o;
              o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
              o.posOS       = v.positionOS.xyz;
              return o;
          }

          half4 frag(Varyings i) : SV_Target
          {
              float uMin  = dot(_PlateUVMin.xyz,  _UAxisMask.xyz);
              float uSize = max(1e-5, dot(_PlateUVSize.xyz, _UAxisMask.xyz));
              float vMin  = dot(_PlateUVMin.xyz,  _VAxisMask.xyz);
              float vSize = max(1e-5, dot(_PlateUVSize.xyz, _VAxisMask.xyz));

              float u = (dot(i.posOS, _UAxisMask.xyz) - uMin) / uSize;
              float v = (dot(i.posOS, _VAxisMask.xyz) - vMin) / vSize;
              if (_InvertV > 0.5) v = 1.0 - v;

              float2 uv = float2(u, v);

              if (_DebugMode > 1.5) // 2 = show mask
              {
                  float m = SAMPLE_TEXTURE2D(_CutMask, sampler_CutMask, uv).r;
                  return half4(m, m, m, 1);
              }
              else if (_DebugMode > 0.5) // 1 = show UV
              {
                  return half4(frac(u), frac(v), 0, 1);
              }

              float mask = SAMPLE_TEXTURE2D(_CutMask, sampler_CutMask, uv).r;
              clip(mask - 0.5);
              return _BaseColor;
          }
          ENDHLSL
      }
  }
}