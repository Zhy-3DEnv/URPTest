#ifndef XRP_LITINPUT_INCLUDED
#define XRP_LITINPUT_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

struct LitSurfaceData {
    half4 baseColor;
    half  metallic;
    half  roughness;
    half  ambientOcclusion;
    half  reflectance; // Fresnel reflectance at normal incidence for dielectric surfaces, this replaces an explicit index of refraction
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
    float2  lightmapUV;
    float4  projectedPosition;
};

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _MetallicOffset;
half _RoughnessOffset;
half _AmbientOcclusionOffset;
half _Reflectance;
half3 _EmissionColor;
half _Cutoff;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_MetallicAORoughnessMap); SAMPLER(sampler_MetallicAORoughnessMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

TEXTURE2D(_AmbientOcclusionMap); SAMPLER(sampler_AmbientOcclusionMap);
TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);

inline void InitializeLitSurfaceData(float2 uv, out LitSurfaceData outSurfaceData) {
    outSurfaceData.baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

#ifdef _USE_ALPHATEST
    clip(outSurfaceData.baseColor.a - _Cutoff);
#endif

#ifdef _USE_METALLICAOROUGHNESSMAP
#ifdef _USE_METALLICAOROUGHNESSOFFSET
    half3 metallicAORoughnessMap = SAMPLE_TEXTURE2D(_MetallicAORoughnessMap, sampler_MetallicAORoughnessMap, uv).rgb;
    outSurfaceData.metallic = saturate(metallicAORoughnessMap.r + _MetallicOffset);
    outSurfaceData.ambientOcclusion = saturate(metallicAORoughnessMap.g + _AmbientOcclusionOffset);
    outSurfaceData.roughness = saturate(metallicAORoughnessMap.b + _RoughnessOffset);
#else
    half3 metallicAORoughnessMap = SAMPLE_TEXTURE2D(_MetallicAORoughnessMap, sampler_MetallicAORoughnessMap, uv).rgb;
    outSurfaceData.metallic = metallicAORoughnessMap.r;
    outSurfaceData.ambientOcclusion = metallicAORoughnessMap.g;
    outSurfaceData.roughness = metallicAORoughnessMap.b;
#endif // _USE_METALLICAOROUGHNESSOFFSET
#else // !_USE_METALLICAOROUGHNESSMAP
    outSurfaceData.metallic = saturate(_MetallicOffset);
    outSurfaceData.ambientOcclusion = saturate(_AmbientOcclusionOffset);
    outSurfaceData.roughness = saturate(_RoughnessOffset);
#endif

#ifdef _USE_SEPARATEMAP
    outSurfaceData.metallic = saturate(SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uv).r + _MetallicOffset);
    outSurfaceData.ambientOcclusion = saturate(SAMPLE_TEXTURE2D(_AmbientOcclusionMap, sampler_AmbientOcclusionMap, uv).r + _AmbientOcclusionOffset);
    outSurfaceData.roughness = saturate(SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uv).r + _RoughnessOffset);
#endif

    outSurfaceData.reflectance = _Reflectance;

#ifdef _USE_EMISSION
    outSurfaceData.emissionColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor;
#else
    outSurfaceData.emissionColor = half3(0.0h, 0.0h, 0.0h);
#endif

#ifdef _USE_NORMALMAP
    half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
    outSurfaceData.normalTS = UnpackNormal(normal);
#else
    outSurfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
#endif
}

#endif // XRP_LITINPUT_INCLUDED