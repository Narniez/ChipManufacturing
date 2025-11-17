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

              // Bounds-based UV mapping provided by SawCutter.PushPlateMappingToMaterial()
              float4 _PlateUVMin;    // xyz = local-space bounds min
              float4 _PlateUVSize;   // xyz = local-space bounds size
              float4 _UAxisMask;     // one-hot axis for U (1,0,0) or (0,1,0) or (0,0,1)
              float4 _VAxisMask;     // one-hot axis for V
              float  _InvertV;       // >0.5 to invert V
          CBUFFER_END

          TEXTURE2D(_CutMask);
          SAMPLER(sampler_CutMask);

          struct Attributes
          {
              float4 positionOS : POSITION;
              float2 uv         : TEXCOORD0; // unused
          };
          struct Varyings
          {
              float4 positionHCS : SV_POSITION;
              float3 posOS       : TEXCOORD0;
          };

          Varyings vert(Attributes v)
          {
              Varyings o;
              o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
              o.posOS       = v.positionOS.xyz; // object-space position for bounds mapping
              return o;
          }

          half4 frag(Varyings i) : SV_Target
          {
              // Extract per-axis min/size using the one-hot masks
              float uMin  = dot(_PlateUVMin.xyz,  _UAxisMask.xyz);
              float uSize = max(1e-5, dot(_PlateUVSize.xyz, _UAxisMask.xyz));
              float vMin  = dot(_PlateUVMin.xyz,  _VAxisMask.xyz);
              float vSize = max(1e-5, dot(_PlateUVSize.xyz, _VAxisMask.xyz));

              // Project object-space position onto the chosen axes, normalize into 0..1
              float u = (dot(i.posOS, _UAxisMask.xyz) - uMin) / uSize;
              float v = (dot(i.posOS, _VAxisMask.xyz) - vMin) / vSize;
              if (_InvertV > 0.5) v = 1.0 - v;

              float2 uv = float2(u, v);

              // Sample the mask (RT wrap mode is Repeat)
              float mask = SAMPLE_TEXTURE2D(_CutMask, sampler_CutMask, uv).r;

              // Debug: visualize mask
              // return half4(mask, mask, mask, 1);

              clip(mask - 0.5);
              return _BaseColor;
          }
          ENDHLSL
      }
  }
}