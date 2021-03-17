#ifndef GRID_LIT_FORWARD_PASS_INCLUDED
#define GRID_LIT_FORWARD_PASS_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Shading.hlsl"

struct Attributes {
  float4 positionOS   : POSITION;
  float3 normalOS     : NORMAL;
  float4 tangentOS    : TANGENT;
  float2 texcoord     : TEXCOORD0;
  float2 lightmapUV   : TEXCOORD1;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct Varyings {
  float2 uv             : TEXCOORD0;
  DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

  float3 positionWS     : TEXCOORD2;

#ifdef _USE_NORMALMAP
  float4 normalWS                 : TEXCOORD3;    // xyz: normal, w: viewDir.x
  float4 tangentWS                : TEXCOORD4;    // xyz: tangent, w: viewDir.y
  float4 bitangentWS              : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
#else // !_USE_NORMALMAP
    float3 normalWS                 : TEXCOORD3;
    float3 viewDirWS                : TEXCOORD4;
#endif

  half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    float4 shadowCoord              : TEXCOORD7;
#endif

#ifdef _USE_CLUSTER_LIGHTING
    float4 projectedPosition        : TEXCOORD8;
#endif

  float4 positionCS     : SV_POSITION;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

void InitializeInputData(Varyings input, half3 normalTS, out LitInputData inputData) {
  inputData = (LitInputData)0;

#ifdef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    inputData.positionWS = input.positionWS;
#endif

#ifdef _USE_NORMALMAP
    half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz));
#else // !_USE_NORMALMAP
    half3 viewDirWS = input.viewDirWS;
    inputData.normalWS = input.normalWS;
#endif

  inputData.normalWS = normalize(inputData.normalWS);
  viewDirWS = SafeNormalize(viewDirWS);
  inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

  inputData.fogCoord = input.fogFactorAndVertexLight.x;
  inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;

 #ifdef LIGHTMAP_ON
    inputData.lightmapUV = input.lightmapUV;
#endif

#ifdef _USE_CLUSTER_LIGHTING
    inputData.projectedPosition = input.projectedPosition;
#endif
}

///////////////////////////////////////////////////////////////////////////////
//          Vertex and Fragment functions              //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input) {
  Varyings output = (Varyings)0;

  UNITY_SETUP_INSTANCE_ID(input);
  UNITY_TRANSFER_INSTANCE_ID(input, output);

  VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

  // normalWS and tangentWS already normalize.
  // this is required to avoid skewing the direction during interpolation
  // also required for per-vertex lighting and SH evaluation
  VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

  half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
  half3 vertexLight = half3(0,0,0);
  half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

  output.uv = input.texcoord * float2(_TilingX, _TilingY);

#ifdef _USE_NORMALMAP
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = normalize(normalInput.normalWS);
    output.viewDirWS = viewDirWS;
#endif

  OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);

  output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

  output.positionWS = vertexInput.positionWS;

#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
#endif

#ifdef _USE_CLUSTER_LIGHTING
    output.projectedPosition = vertexInput.positionNDC;
#endif

  output.positionCS = vertexInput.positionCS;

  return output;
}

half4 LitPassFragment(Varyings input) : SV_Target {
  UNITY_SETUP_INSTANCE_ID(input);

  LitSurfaceData surfaceData;
  InitializeStandardLitSurfaceData(input.uv, input.positionWS, input.normalWS, surfaceData);

  LitInputData inputData;
  InitializeInputData(input, surfaceData.normalTS, inputData);

  half4 color = XRPFragmentPBR(inputData, surfaceData);
  color.rgb = MixFog(color.rgb, inputData.fogCoord);
  color.a = color.a;

  return color;
}

#endif