
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
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _Fresnel;
half _PixelWidth;
half _Thickness;
half _FresnelExponent;
half4 _FresnelColor;
half _Reflectance;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap); SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MatCap); SAMPLER(sampler_MatCap);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
TEXTURE2D(_MetallicAORoughnessMap); SAMPLER(sampler_MetallicAORoughnessMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = SAMPLE_METALLICSPECULAR(uv);
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
    #endif
#else // _METALLICSPECGLOSSMAP
    #if _SPECULAR_SETUP
        specGloss.rgb = _SpecColor.rgb;
    #else
        specGloss.rgb = _Metallic.rrr;
    #endif

    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a = _Smoothness;
    #endif
#endif

    return specGloss;
}

inline void InitializeGlassLitSurfaceData(float2 uv, out LitSurfaceData outSurfaceData) {

    half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

    outSurfaceData.baseColor.rgb = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.baseColor.a = albedoAlpha.a * _BaseColor.a;

    half3 metallicAORoughnessMap = SAMPLE_TEXTURE2D(_MetallicAORoughnessMap, sampler_MetallicAORoughnessMap, uv).rgb;
    outSurfaceData.metallic = metallicAORoughnessMap.r;
    outSurfaceData.ambientOcclusion = metallicAORoughnessMap.g;
    outSurfaceData.roughness = metallicAORoughnessMap.b;

    half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
    outSurfaceData.normalTS = UnpackNormal(normal);

    outSurfaceData.emissionColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor;

    outSurfaceData.reflectance = _Reflectance;
}


