#ifndef XRP_SHADING_INCLUDED
#define XRP_SHADING_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Light.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Debug.hlsl"

// min roughness such that (MIN_PERCEPTUAL_ROUGHNESS^4) > 0 in fp16 (i.e. 2^(-14/4), rounded up)
#if defined(SHADER_API_MOBILE)
#define MIN_PERCEPTUAL_ROUGHNESS 0.089
#define MIN_ROUGHNESS            0.007921
#else
#define MIN_PERCEPTUAL_ROUGHNESS 0.045
#define MIN_ROUGHNESS            0.002025
#endif
#define MIN_N_DOT_V 1e-4

struct BRDFData {
    half3 diffuseColor;
    half  perceptualRoughness;
    half  perceptualRoughnessUnclamped;
    half3 f0;
    half3 fakeSpecularF0;
    half  roughness;
    half3 dfg;
    half3 energyCompensation;
};

#ifdef _DEBUG_MODE
inline void InitializeBRDFData(LitSurfaceData surfaceData, half NoV, out BRDFData brdfData, inout DebugData debugData) {
#else
inline void InitializeBRDFData(LitSurfaceData surfaceData, half NoV, out BRDFData brdfData) {
#endif

    brdfData.diffuseColor = surfaceData.baseColor.rgb * (1.0 - surfaceData.metallic);
    half reflectance = 0.16 * surfaceData.reflectance * surfaceData.reflectance;
    brdfData.f0 = surfaceData.baseColor.rgb * surfaceData.metallic + (reflectance * (1.0 - surfaceData.metallic));
#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
    // empirical f0 for fake indirect specular lighting
    brdfData.fakeSpecularF0 = surfaceData.baseColor.rgb * surfaceData.metallic + (0.03 * (1.0 - surfaceData.metallic));
#else
    brdfData.fakeSpecularF0 = half3(1, 1, 1);
#endif
    brdfData.perceptualRoughnessUnclamped = surfaceData.roughness;
    // Clamp the roughness to a minimum value to avoid divisions by 0 during lighting
    brdfData.perceptualRoughness = clamp(surfaceData.roughness, MIN_PERCEPTUAL_ROUGHNESS, 1.0);
    // Remaps the roughness to a perceptually linear roughness (roughness^2)
    brdfData.roughness = brdfData.perceptualRoughness * brdfData.perceptualRoughness;
#ifdef _ENVBRDFAPPROX_V2
    brdfData.dfg = half3(DFGApproxV2(brdfData.perceptualRoughness, NoV), 1);
#else
    brdfData.dfg = half3(DFGApproxV1(brdfData.perceptualRoughness, NoV), 1);
#endif
#ifdef _USE_ENERGYCOMPENSATION
    // Energy compensation for multiple scattering in a microfacet model
    // See "Multiple-Scattering Microfacet BSDFs with the Smith Model"
    brdfData.energyCompensation = 1.0 + brdfData.f0 * (1.0 / brdfData.dfg.x - 1.0);
#else
    brdfData.energyCompensation = half3(1, 1, 1);
#endif

#ifdef _DEBUG_MODE
    debugData.diffuseColor = brdfData.diffuseColor;
    debugData.specularColor = brdfData.f0;
#endif
}

half3 SpecularLobe(BRDFData brdfData, half3 normalWS, half3 viewWS, half3 lightWS, half3 h, half NoL, half NoV, half NoH, half LoH) {
#ifdef _SHADINGQUALITY_HIGH
    return SpecularBRDF_HighQ(brdfData.roughness, brdfData.f0, normalWS, h, NoL, NoV, NoH, LoH);
#endif
#ifdef _SHADINGQUALITY_MEDIUM
    return SpecularBRDF_MediumQ(brdfData.roughness, brdfData.perceptualRoughnessUnclamped, brdfData.f0, normalWS, h, NoH);
#endif
#ifdef _SHADINGQUALITY_LOW
    return SpecularBRDF_LowQ(brdfData.roughness, brdfData.f0, viewWS, normalWS, lightWS);
#endif
    // default high quality
    return SpecularBRDF_HighQ(brdfData.roughness, brdfData.f0, normalWS, h, NoL, NoV, NoH, LoH);
}

half3 DiffuseLobe(half3 diffuseColor) {
    return diffuseColor * Fd_Lambert();
}

#ifdef _DEBUG_MODE
half3 LightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS, half NoV, inout DebugData debugData) {
#else
half3 LightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS, half NoV) {
#endif

    half NoL = saturate(dot(normalWS, light.direction));
    half3 h = normalize(light.direction + viewDirectionWS);
    half NoH = saturate(dot(normalWS, h));
    half LoH = saturate(dot(light.direction, h));

    half3 Fr = SpecularLobe(brdfData, normalWS, viewDirectionWS, light.direction, h, NoL, NoV, NoH, LoH);
    half3 Fd = DiffuseLobe(brdfData.diffuseColor);
#ifdef _USE_ENERGYCOMPENSATION
    Fr *= brdfData.energyCompensation;
#endif

    half3 color = Fd + Fr;
#ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    half lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
#else
    half lightAttenuation = light.distanceAttenuation;
#endif

#ifdef _DEBUG_MODE
    debugData.directSpecular += (Fr * light.colorIntensity.rgb) * (light.colorIntensity.w * lightAttenuation * NoL);
    debugData.directDiffuse += (Fd * light.colorIntensity.rgb) * (light.colorIntensity.w * lightAttenuation * NoL);
#endif
    return (color * light.colorIntensity.rgb) * (light.colorIntensity.w * lightAttenuation * NoL);
}


half3 SubtractDirectMainLightFromLightmap(Light mainLight, half3 normalWS, half3 indirectDiffuse) {
    // Let's try to make realtime shadows work on a surface, which already contains
    // baked lighting and shadowing from the main sun light.
    // Summary:
    // 1) Calculate possible value in the shadow by subtracting estimated light contribution from the places occluded by realtime shadow:
    //      a) preserves other baked lights and light bounces
    //      b) eliminates shadows on the geometry facing away from the light
    // 2) Clamp against user defined ShadowColor.
    // 3) Pick original lightmap value, if it is the darkest one.


    // 1) Gives good estimate of illumination as if light would've been shadowed during the bake.
    // We only subtract the main direction light. This is accounted in the contribution term below.
    half contributionTerm = saturate(dot(mainLight.direction, normalWS));
    half3 lambert = mainLight.colorIntensity.rgb * mainLight.colorIntensity.w * contributionTerm;
    half3 estimatedLightContributionMaskedByInverseOfShadow = lambert * (1.0 - mainLight.shadowAttenuation);
    half3 subtractedLightmap = indirectDiffuse - estimatedLightContributionMaskedByInverseOfShadow;

    // 2) Allows user to define overall ambient of the scene and control situation when realtime shadow becomes too dark.
    half3 realtimeShadow = max(subtractedLightmap, _SubtractiveShadowColor.xyz);
    realtimeShadow = lerp(indirectDiffuse, realtimeShadow, _MainLightShadowParams.x);

    // 3) Pick darkest color
    return min(indirectDiffuse, realtimeShadow);
}

#ifdef _DEBUG_MODE
half3 GlobalIllumination(BRDFData brdfData, half ambientOcclusion, half3 normalWS, half3 viewWS, float2 lightmapUV, Light mainLight, inout DebugData debugData) {
#else
half3 GlobalIllumination(BRDFData brdfData, half ambientOcclusion, half3 normalWS, half3 viewWS, float2 lightmapUV, Light mainLight) {
#endif

    half3 indirectDiffuseLight = half3(0, 0, 0);
    half3 indirectSpecularLight = half3(0, 0, 0);
    half3 Fr = half3(0, 0, 0);
    half3 Fd = half3(0, 0, 0);

#ifdef LIGHTMAP_ON
    SampleLightmap(lightmapUV, brdfData.roughness, normalWS, viewWS, indirectDiffuseLight, indirectSpecularLight);
#else
    indirectDiffuseLight = SampleSH(normalWS);
    indirectSpecularLight = half3(0, 0, 0);
#endif

#if defined(LIGHTMAP_ON) && defined(_MIXED_LIGHTING_SUBTRACTIVE)
    indirectDiffuseLight = SubtractDirectMainLightFromLightmap(mainLight, normalWS, indirectDiffuseLight);
#endif

#ifdef _USE_GLOSSYENVREFLECTION
    half3 reflectVector = reflect(-viewWS, normalWS);
    // NOTE: when energy compensation is turned off, we should use different blend function
#ifdef _USE_ENERGYCOMPENSATION
    half3 E = lerp(brdfData.dfg.yyy, brdfData.dfg.xxx, brdfData.f0);
#else
    half3 E = brdfData.f0 * brdfData.dfg.x + brdfData.dfg.y;
#endif // _USE_ENERGYCOMPENSATION
    Fr = E * GlossyEnvironmentReflection(reflectVector, brdfData.perceptualRoughnessUnclamped);
#ifdef _USE_SPECULARAO
    half specularAO = ComputeSpecularAO(dot(normalWS, viewWS), ambientOcclusion, brdfData.roughness);
    Fr *= specularAO;
#endif
#ifdef _USE_ENERGYCOMPENSATION
    Fr *= brdfData.energyCompensation;
#endif
    Fd = indirectDiffuseLight * brdfData.diffuseColor * ambientOcclusion * (1.0 - E);

#else // !_USE_GLOSSYENVREFLECTION

#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
    Fd = indirectDiffuseLight * brdfData.diffuseColor * ambientOcclusion;
#ifdef _USE_FAKEENVSPECULAR
    Fr = indirectSpecularLight * brdfData.fakeSpecularF0;
#endif // _USE_FAKEENVSPECULAR
#else // !(defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED))
    Fd = indirectDiffuseLight * brdfData.diffuseColor * ambientOcclusion;
#endif // LIGHTMAP_ON && DIRLIGHTMAP_COMBINED

#endif // _USE_GLOSSYENVREFLECTION

#ifdef _DEBUG_MODE
    debugData.indirectSpecular = Fr;
    debugData.indirectDiffuse = Fd;
#endif

    return (Fr + Fd);
}

#ifdef _DEBUG_MODE
half4 XRPFragmentPBR(LitInputData inputData, LitSurfaceData surfaceData, inout DebugData debugData) {
#else
half4 XRPFragmentPBR(LitInputData inputData, LitSurfaceData surfaceData) {
#endif

    half NoV = max(dot(inputData.normalWS, inputData.viewDirectionWS), MIN_N_DOT_V);
#ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    Light mainLight = GetMainLight(inputData.shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif
    BRDFData brdfData;

#ifdef _DEBUG_MODE
    debugData.baseColor = surfaceData.baseColor;
    debugData.emissionColor = surfaceData.emissionColor;
    debugData.metallic = surfaceData.metallic;
    debugData.roughness = surfaceData.roughness;
    debugData.ambientOcclusion = surfaceData.ambientOcclusion;
    debugData.normalWS = inputData.normalWS;

    InitializeBRDFData(surfaceData, NoV, brdfData, debugData);
    half3 color = GlobalIllumination(brdfData, surfaceData.ambientOcclusion, inputData.normalWS, inputData.viewDirectionWS, inputData.lightmapUV, mainLight, debugData);
    color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS, NoV, debugData);
#else // !_DEBUG_MODE
    InitializeBRDFData(surfaceData, NoV, brdfData);
    half3 color = GlobalIllumination(brdfData, surfaceData.ambientOcclusion, inputData.normalWS, inputData.viewDirectionWS, inputData.lightmapUV, mainLight);
    color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS, NoV);
#endif

#if defined(_USE_CLUSTER_LIGHTING)
    float linearDepth = LinearEyeDepth(inputData.projectedPosition.z / inputData.projectedPosition.w, _ZBufferParams);
    float linearDepthLog2 = log2(linearDepth);
    uint zSlice = uint(max(linearDepthLog2 * _SliceScale + _SliceBias, 0.0));
    float2 screenPos = inputData.projectedPosition.xy / inputData.projectedPosition.w * _ScreenParams.xy;
    uint3 clusterId = uint3(uint2(screenPos / _ClusterDimXYAndTileSize.zw), zSlice);
    uint clusterIndex = clusterId.x + _ClusterDimXYAndTileSize.x * clusterId.y + (_ClusterDimXYAndTileSize.x * _ClusterDimXYAndTileSize.y) * clusterId.z;

    LightGrid lightGrid = LoadLightGrid(clusterIndex);

// cluster lighting overdraw debug code
#if 0
    half3 cntDebugRGB;
    uint lcnt = (lightGrid.count & 0xFFFF) + (lightGrid.count >> 16) & 0xFFFF;
    if (lcnt <= 3) {
        return half4(0, 0.3 * lcnt, 0, 1); // green
    } else if (lcnt <= 6) {
        return half4(0, 0, 0.3 * (lcnt - 3), 1); // blue
    } else if (lcnt <= 10) {
        return half4(0.3 * (lcnt - 6), 0.3 * (lcnt - 6), 0, 1); // yellow
    } else if (lcnt <= 16) {
        return half4(0.3 * (lcnt - 10), 0, 0, 1); // red
    } else {
        return half4(1, 0, 1, 1); // magenta
    }
#endif

    uint pointLightCount = lightGrid.count & 0xFFFF;
    for (uint i = 0; i < pointLightCount; ++i) {
        uint lightIndex = LoadLightIndex(lightGrid.offset + i);
        PointLight plight = LoadPointLight(lightIndex);
        Light light = GetClusteredPointLight(plight, inputData.positionWS);
        // TODO: use cheaper brdf model for cluster shading?
#ifdef _DEBUG_MODE
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, NoV, debugData);
#else
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, NoV);
#endif
    }

    uint spotLightCount = (lightGrid.count >> 16) & 0xFFFF;
    for (uint i = 0; i < spotLightCount; ++i) {
        uint lightIndex = LoadLightIndex(lightGrid.offset + pointLightCount + i);
        SpotLight slight = LoadSpotLight(lightIndex);
        Light light = GetClusteredSpotLight(slight, inputData.positionWS);
        // TODO: use cheaper brdf model for cluster shading?
#ifdef _DEBUG_MODE
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, NoV, debugData);
#else
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, NoV);
#endif
    }
#endif // _USE_CLUSTER_LIGHTING

#if defined(_ADDITIONAL_LIGHTS) && !defined(_USE_CLUSTER_LIGHTING)
    uint pixelLightCount = GetAdditionalLightsCount();
    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex) {
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
#ifdef _DEBUG_MODE
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, NoV, debugData);
#else
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS, NoV);
#endif
    }
#endif

#ifdef _USE_EMISSION
    color += surfaceData.emissionColor;
#endif
    return half4(color, surfaceData.baseColor.a);
}

#endif // XRP_SHADING_INCLUDED