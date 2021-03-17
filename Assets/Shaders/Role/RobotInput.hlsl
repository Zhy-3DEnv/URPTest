#ifndef ROBOT_INPUT_INCLUDED
#define ROBOT_INPUT_INCLUDED
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
#include "CommonFunction.hlsl"

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
float4 _BaseMap_ST;
half4 _Color;
half4 _Color02;
half4 _SpecColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
float _ScaleSize;
float _ScaleCenter;
float _Damping;
float _ScaleWidth;
int _OriginalColor;
float4 _PaintColor;
half _Brightness;
half4 _BaseColor;
float _OutLineWidth;
half4 _OutLineColor;
half _FresnelExponent;
half4 _FresnelColor;
half _Reflectance;
CBUFFER_END 

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap); SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
TEXTURE2D(_Decal); SAMPLER(sampler_Decal);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
TEXTURE2D(_MetallicAORoughnessMap); SAMPLER(sampler_MetallicAORoughnessMap);
TEXTURE2D(_PlayerMaskRenderTexture); SAMPLER(sampler_PlayerMaskRenderTexture);

inline void InitializeRobotLitSurfaceData(float2 uv, float2 uv2, out LitSurfaceData outSurfaceData) {
    half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half3 mask = SAMPLE_TEXTURE2D(_Mask,sampler_Mask,uv).rgb;

    #ifdef _USE_MULTIP_COLOR
    albedoAlpha.rgb = replaceMultipleColorViaMaskAndBrightness(mask, albedoAlpha.rgb, _Color.rgb, _Color02.rgb, _OriginalColor, _Brightness);
    #else
    albedoAlpha.rgb = replaceColorViaMaskAndBrightness(mask, albedoAlpha.rgb, _Color.rgb, _OriginalColor, _Brightness);
    #endif

    #ifdef _USE_DECAL
    half4 decalAlpha = SAMPLE_TEXTURE2D(_Decal, sampler_Decal, uv2);
    albedoAlpha.rgb *= 1 - decalAlpha.a;
    half fullMask = max(max(mask.r, mask.g), mask.b);
    albedoAlpha.rgb += decalAlpha.rgb * decalAlpha.a * fullMask;
    #endif

    outSurfaceData.baseColor = albedoAlpha;

    half3 metallicAORoughnessMap = SAMPLE_TEXTURE2D(_MetallicAORoughnessMap, sampler_MetallicAORoughnessMap, uv).rgb;
    outSurfaceData.metallic = metallicAORoughnessMap.r;
    outSurfaceData.ambientOcclusion = metallicAORoughnessMap.g;
    outSurfaceData.roughness = metallicAORoughnessMap.b;

    outSurfaceData.reflectance = _Reflectance;

    outSurfaceData.emissionColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;

    half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
    outSurfaceData.normalTS = UnpackNormal(normal);
}

#endif
