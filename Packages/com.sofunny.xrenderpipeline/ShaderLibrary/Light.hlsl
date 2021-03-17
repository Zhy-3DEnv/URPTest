#ifndef XRP_LIGHTING_INCLUDED
#define XRP_LIGHTING_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Shadow.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/ClusterCommon.hlsl"

#if defined(_ADDITIONAL_LIGHTS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_USE_CLUSTER_LIGHTING)
    #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#endif

float4 _MainLightPosition;
half4 _MainLightColorIntensity;

#define MAX_VISIBLE_LIGHTS 32
half4 _AdditionalLightsCount;
float4 _AdditionalLightsPositions[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsColorIntensities[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsAttenuations[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsSpotDirs[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsOcclusionProbes[MAX_VISIBLE_LIGHTS];

struct Light {
    half4 colorIntensity;
    half3 direction;
    half distanceAttenuation;
    half shadowAttenuation;
};

uint4 _ClusterDimXYAndTileSize; // xy: cluster xy dimension, zw: per tile pixel size
float _SliceScale;
float _SliceBias;

#if USE_CBUFFER_FOR_CLUSTERED_SHADING
CBUFFER_START(CPointLights)
float4 _PointLights[MAX_VISIBLE_POINTLIGHT_COUNT * POINTLIGHT_VEC4_SIZE];
CBUFFER_END
PointLight LoadPointLight(uint idx) {
    uint lightIdx = idx * POINTLIGHT_VEC4_SIZE;
    PointLight light;
    light.positionAndRange = _PointLights[lightIdx];
    light.colorIntensity = _PointLights[lightIdx + 1];
    return light;
}
#else
StructuredBuffer<PointLight> _PointLights;
PointLight LoadPointLight(uint idx) {
    return _PointLights[idx];
}
#endif // USE_CBUFFER_FOR_CLUSTERED_SHADING

#if USE_CBUFFER_FOR_CLUSTERED_SHADING
CBUFFER_START(CSpotLights)
float4 _SpotLights[MAX_VISIBLE_SPOTLIGHT_COUNT * SPOTLIGHT_VEC4_SIZE];
CBUFFER_END
SpotLight LoadSpotLight(uint idx) {
    uint lightIdx = idx * SPOTLIGHT_VEC4_SIZE;
    SpotLight light;
    light.positionAndRange = _SpotLights[lightIdx];
    light.colorIntensity = _SpotLights[lightIdx + 1];
    light.spotDirAndAngle = _SpotLights[lightIdx + 2];
    light.attenuation = _SpotLights[lightIdx + 3];
    return light;
}
#else
StructuredBuffer<SpotLight> _SpotLights;
SpotLight LoadSpotLight(uint idx) {
    return _SpotLights[idx];
}
#endif // USE_CBUFFER_FOR_CLUSTERED_SHADING

#if USE_CBUFFER_FOR_CLUSTERED_SHADING
CBUFFER_START(CLightGrids)
uint4 _LightGrids[TOTAL_CLUSTER_COUNT / 2];
CBUFFER_END
LightGrid LoadLightGrid(uint idx) {
    uint gridIdx = idx >> 1;
    LightGrid lightGrid;
    lightGrid.offset = _LightGrids[gridIdx][2 * (idx & 1)];
    lightGrid.count  = _LightGrids[gridIdx][2 * (idx & 1) + 1];
    return lightGrid;
}
#else
StructuredBuffer<LightGrid> _LightGrids;
LightGrid LoadLightGrid(uint idx) {
    return _LightGrids[idx];
}
#endif // USE_CBUFFER_FOR_CLUSTERED_SHADING

// NOTE: LightIndexList cannot use constant buffer because "maxVisibleLightsPerCluster * totalClusterCount * sizeof(uint)" exceeds maximum allowed size 64kb on d3d11
// CBUFFER_START(CLightIndexList)
// uint4 _LightIndexList[MAX_VISIBLE_LIGHTS_PER_CLUSTER * TOTAL_CLUSTER_COUNT / 4];//[MAX_VEC4_COUNT_CBUFFER];
// CBUFFER_END
// uint LoadLightIndex(uint idx) {
//     return _LightIndexList[idx >> 2][idx & 3];
// }
StructuredBuffer<uint> _LightIndexList;
uint LoadLightIndex(uint idx) {
    return _LightIndexList[idx];
}


Light GetMainLight() {
    Light light;
    light.colorIntensity = _MainLightColorIntensity;
    light.direction = _MainLightPosition.xyz;
    light.distanceAttenuation = unity_LightData.z;
#if defined(LIGHTMAP_ON) || defined(_MIXED_LIGHTING_SUBTRACTIVE)
    // unity_ProbesOcclusion.x is the mixed light probe occlusion data
    light.distanceAttenuation *= unity_ProbesOcclusion.x;
#endif
    light.shadowAttenuation = 1.0;
    return light;
}

Light GetMainLight(float4 shadowCoord) {
    Light light = GetMainLight();
#ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    light.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
#endif
    return light;
}

// Matches Unity Vanila attenuation
// Attenuation smoothly decreases to light range.
float DistanceAttenuation(float distanceSqr, half2 distanceAttenuation) {
    // We use a shared distance attenuation for additional directional and puctual lights
    // for directional lights attenuation will be 1
    float lightAtten = rcp(distanceSqr);
    half smoothFactor = saturate(distanceSqr * distanceAttenuation.x + distanceAttenuation.y);

    return lightAtten * smoothFactor;
}

half AngleAttenuation(half3 spotDirection, half3 lightDirection, half2 spotAttenuation) {
    // Spot Attenuation with a linear falloff can be defined as
    // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
    // This can be rewritten as
    // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
    // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
    // SdotL * spotAttenuation.x + spotAttenuation.y

    // If we precompute the terms in a MAD instruction
    half SdotL = dot(spotDirection, lightDirection);
    half atten = saturate(SdotL * spotAttenuation.x + spotAttenuation.y);
    return atten * atten;
}

// Fills a light struct given a perObjectLightIndex
Light GetAdditionalPerObjectLight(int perObjectLightIndex, float3 positionWS) {
    // Abstraction over Light input constants
    float4 lightPositionWS = _AdditionalLightsPositions[perObjectLightIndex];
    half4 colorIntensity = _AdditionalLightsColorIntensities[perObjectLightIndex];
    half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuations[perObjectLightIndex];
    half4 spotDirection = _AdditionalLightsSpotDirs[perObjectLightIndex];
    half4 lightOcclusionProbeInfo = _AdditionalLightsOcclusionProbes[perObjectLightIndex];

    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float3 lightVector = lightPositionWS.xyz - positionWS * lightPositionWS.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    half attenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy) * AngleAttenuation(spotDirection.xyz, lightDirection, distanceAndSpotAttenuation.zw);

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0;
    light.colorIntensity = colorIntensity;

    // In case we're using light probes, we can sample the attenuation from the `unity_ProbesOcclusion`
#if defined(LIGHTMAP_ON) || defined(_MIXED_LIGHTING_SUBTRACTIVE)
    // First find the probe channel from the light.
    // Then sample `unity_ProbesOcclusion` for the baked occlusion.
    // If the light is not baked, the channel is -1, and we need to apply no occlusion.

    // probeChannel is the index in 'unity_ProbesOcclusion' that holds the proper occlusion value.
    int probeChannel = lightOcclusionProbeInfo.x;

    // lightProbeContribution is set to 0 if we are indeed using a probe, otherwise set to 1.
    half lightProbeContribution = lightOcclusionProbeInfo.y;

    half probeOcclusionValue = unity_ProbesOcclusion[probeChannel];
    light.distanceAttenuation *= max(probeOcclusionValue, lightProbeContribution);
#endif

    return light;
}

Light GetClusteredPointLight(PointLight pointLight, float3 positionWS) {
    float3 lightVector = pointLight.positionAndRange.xyz - positionWS;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);
    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    // TODO: compute in cpu?
    half lightRange = pointLight.positionAndRange.w;
    half lightRangeSqr = lightRange * lightRange;
    half fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
    half fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
    half oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
    half lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
    half attenuation = DistanceAttenuation(distanceSqr, half2(oneOverFadeRangeSqr, lightRangeSqrOverFadeRangeSqr));

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0;
    light.colorIntensity = pointLight.colorIntensity;

    return light;
}

Light GetClusteredSpotLight(SpotLight spotLight, float3 positionWS) {
    float3 lightVector = spotLight.positionAndRange.xyz - positionWS;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    half attenuation = DistanceAttenuation(distanceSqr, spotLight.attenuation.xy) * AngleAttenuation(spotLight.spotDirAndAngle.xyz, lightDirection, spotLight.attenuation.zw);

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0;
    light.colorIntensity = spotLight.colorIntensity;

    return light;
}

// Returns a per-object index given a loop index.
// This abstract the underlying data implementation for storing lights/light indices
int GetPerObjectLightIndex(uint index) {
#if !defined(SHADER_API_GLES)
    // since index is uint shader compiler will implement
    // div & mod as bitfield ops (shift and mask).

    // TODO: Can we index a float4? Currently compiler is
    // replacing unity_LightIndicesX[i] with a dp4 with identity matrix.
    // u_xlat16_40 = dot(unity_LightIndices[int(u_xlatu13)], ImmCB_0_0_0[u_xlati1]);
    // This increases both arithmetic and register pressure.
    return unity_LightIndices[index / 4][index % 4];
#else
    // Fallback to GLES2. No bitfield magic here :(.
    // We limit to 4 indices per object and only sample unity_4LightIndices0.
    // Conditional moves are branch free even on mali-400
    // small arithmetic cost but no extra register pressure from ImmCB_0_0_0 matrix.
    half2 lightIndex2 = (index < 2.0h) ? unity_LightIndices[0].xy : unity_LightIndices[0].zw;
    half i_rem = (index < 2.0h) ? index : index - 2.0h;
    return (i_rem < 1.0h) ? lightIndex2.x : lightIndex2.y;
#endif
}

// Fills a light struct given a loop i index. This will convert the i
// index to a perObjectLightIndex
Light GetAdditionalLight(uint i, float3 positionWS) {
    int perObjectLightIndex = GetPerObjectLightIndex(i);
    return GetAdditionalPerObjectLight(perObjectLightIndex, positionWS);
}

int GetAdditionalLightsCount() {
    // TODO: we need to expose in SRP api an ability for the pipeline cap the amount of lights
    // in the culling. This way we could do the loop branch with an uniform
    // This would be helpful to support baking exceeding lights in SH as well
    return min(_AdditionalLightsCount.x, unity_LightData.y);
}

#endif