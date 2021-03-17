#ifndef GRID_LIT_INPUT_INCLUDED
#define GRID_LIT_INPUT_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

struct LitSurfaceData {
    half4 baseColor;
    half  metallic;
    half  roughness;
    half  ambientOcclusion;
    half  reflectance; 
    half3 normalTS;
    half3 emissionColor;
};

struct LitInputData {
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    float4  shadowCoord;
    half    fogCoord;
    half3   vertexLighting;
    float2 lightmapUV;
    float4 projectedPosition;
};

CBUFFER_START(UnityPerMaterial)
half3 _EmissionColor;

half4 _BaseColorWhite;
half4 _BaseColorBlack;

half _BaseMetallicWhite;
half _BaseMetallicBlack;
half _BaseRoughnessWhite;
half _BaseRoughnessBlack ;

half4 _DetailMap_ST;
half4 _DetailColor;
half _DetailMetallic;
half _DetailRoughness;

half4 _DetailMap2_ST;
half4 _DetailColor2;

half4 _DetailMap3_ST;
half4 _DetailColor3;

half _TilingX;
half _TilingY;

float4 _BaseMap_ST;
half _Reflectance;

half _Cutoff;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
TEXTURE2D(_DetailMap2); SAMPLER(sampler_DetailMap2);
TEXTURE2D(_DetailMap3); SAMPLER(sampler_DetailMap3);

TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

half SampleOcclusion(float2 uv) {
#ifdef _OCCLUSIONMAP
// TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
#if defined(SHADER_API_GLES)
  return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
#else
  half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
  return LerpWhiteTo(occ, _OcclusionStrength);
#endif
#else
  return 1.0;
#endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, float3 wpos, float3 wnormal, out LitSurfaceData outSurfaceData) {
  float2 uvBase = TRANSFORM_TEX(uv, _BaseMap);

  #if DETAIL_ON
  float2 uvDetail = TRANSFORM_TEX(uv, _DetailMap);
  #endif

  #if DETAIL2_ON
  float2 uvDetail2 = TRANSFORM_TEX(uv, _DetailMap2);
  #endif

  #if DETAIL3_ON
  float2 uvDetail3 = TRANSFORM_TEX(uv, _DetailMap3);
  #endif

  // TODO: wpos, wnormal should passed in InitializeStandardLitSurfaceData
#if WPOS_ON
  float2 tiling = float2(_TilingX, _TilingY);
  if (abs(wnormal.x) > 0.5) { // side
    uvBase = (wpos.zy * tiling * _BaseMap_ST.xy) + _BaseMap_ST.zw;
    #if DETAIL_ON
    uvDetail = (wpos.zy * tiling * _DetailMap_ST.xy) + _DetailMap_ST.zw;
    #endif
    #if DETAIL2_ON
    uvDetail2 = (wpos.zy * tiling * _DetailMap2_ST.xy) + _DetailMap2_ST.zw;
    #endif
    #if DETAIL3_ON
    uvDetail3 = (wpos.zy * tiling * _DetailMap3_ST.xy) + _DetailMap3_ST.zw;
    #endif
  } else if (abs(wnormal.z) > 0.5) { // front
    uvBase = (wpos.xy * tiling * _BaseMap_ST.xy) + _BaseMap_ST.zw;
    #if DETAIL_ON
    uvDetail = (wpos.xy * tiling * _DetailMap_ST.xy) + _DetailMap_ST.zw;
    #endif

    #if DETAIL2_ON
    uvDetail2 = (wpos.xy * tiling * _DetailMap2_ST.xy) + _DetailMap2_ST.zw;
    #endif

    #if DETAIL3_ON
    uvDetail3 = (wpos.xy * tiling * _DetailMap3_ST.xy) + _DetailMap3_ST.zw;
    #endif
  } else { // top
    uvBase = (wpos.xz * tiling * _BaseMap_ST.xy) + _BaseMap_ST.zw;
    #if DETAIL_ON
    uvDetail = (wpos.xz * tiling * _DetailMap_ST.xy) + _DetailMap_ST.zw;
    #endif

    #if DETAIL2_ON
    uvDetail2 = (wpos.xz * tiling * _DetailMap2_ST.xy) + _DetailMap2_ST.zw;
    #endif

    #if DETAIL3_ON
    uvDetail3 = (wpos.xz * tiling * _DetailMap3_ST.xy) + _DetailMap3_ST.zw;
    #endif
  }
#endif

  half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase);
  half baseStep = baseMap.r;

  half4 albedo = lerp(_BaseColorBlack, _BaseColorWhite, baseStep);
  half metallic = lerp(_BaseMetallicBlack, _BaseMetallicWhite, baseStep);
  half roughness = lerp(_BaseRoughnessBlack , _BaseRoughnessWhite, baseStep);

#if DETAIL_ON
  half4 detailMap = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uvDetail);
  half detailStep = detailMap.r;
  albedo = lerp(albedo, lerp(albedo, _DetailColor, _DetailColor.a), detailStep);
  metallic = lerp(metallic, _DetailMetallic, detailStep);
  roughness = lerp(roughness, _DetailRoughness, detailStep);
#endif

#if DETAIL2_ON
  half4 detailMap2 = SAMPLE_TEXTURE2D(_DetailMap2, sampler_DetailMap2, uvDetail2);
  half detailStep2 = detailMap2.r;
  albedo = lerp(albedo, lerp(albedo, _DetailColor2, _DetailColor2.a), detailStep2);
  metallic = lerp(metallic, 0.0, detailStep2);
  roughness = lerp(roughness, 0.0, detailStep2);
#endif

#if DETAIL3_ON
  half4 detailMap3 = SAMPLE_TEXTURE2D(_DetailMap3, sampler_DetailMap3, uvDetail3);
  half detailStep3 = detailMap3.r;
  albedo = lerp(albedo, lerp(albedo, _DetailColor3, _DetailColor3.a), detailStep3);
  metallic = lerp(metallic, 0.0, detailStep3);
  roughness = lerp(roughness, 0.0, detailStep3);
#endif

  outSurfaceData.baseColor = albedo;

#ifdef _USE_ALPHATEST
    clip(outSurfaceData.baseColor.a - _Cutoff);
#endif

  outSurfaceData.metallic = metallic;
  outSurfaceData.roughness = roughness;
  outSurfaceData.ambientOcclusion = 1;

  // ---------------------------------------
#ifdef _USE_NORMALMAP
  half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
  outSurfaceData.normalTS = UnpackNormal(normal);
#else
  outSurfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
#endif

  outSurfaceData.reflectance = _Reflectance;

#ifdef _USE_EMISSION
    outSurfaceData.emissionColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor;
#else
    outSurfaceData.emissionColor = half3(0.0h, 0.0h, 0.0h);
#endif
}

#endif // GRID_LIT_INPUT_INCLUDED