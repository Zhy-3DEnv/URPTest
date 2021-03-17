#ifndef XRP_GLOBALILLUMINATION_INCLUDED
#define XRP_GLOBALILLUMINATION_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"

#define LIGHTMAP_RGBM_MAX_GAMMA     real(5.0)       // NB: Must match value in RGBMRanges.h
#define LIGHTMAP_RGBM_MAX_LINEAR    real(34.493242) // LIGHTMAP_RGBM_MAX_GAMMA ^ 2.2

#ifdef UNITY_LIGHTMAP_RGBM_ENCODING
#ifdef UNITY_COLORSPACE_GAMMA
#define LIGHTMAP_HDR_MULTIPLIER LIGHTMAP_RGBM_MAX_GAMMA
#define LIGHTMAP_HDR_EXPONENT   real(1.0)   // Not used in gamma color space
#else
#define LIGHTMAP_HDR_MULTIPLIER LIGHTMAP_RGBM_MAX_LINEAR
#define LIGHTMAP_HDR_EXPONENT   real(2.2)
#endif
#elif defined(UNITY_LIGHTMAP_DLDR_ENCODING)
#ifdef UNITY_COLORSPACE_GAMMA
#define LIGHTMAP_HDR_MULTIPLIER real(2.0)
#else
#define LIGHTMAP_HDR_MULTIPLIER real(4.59) // 2.0 ^ 2.2
#endif
#define LIGHTMAP_HDR_EXPONENT real(0.0)
#else // (UNITY_LIGHTMAP_FULL_HDR)
#define LIGHTMAP_HDR_MULTIPLIER real(1.0)
#define LIGHTMAP_HDR_EXPONENT real(1.0)
#endif

#ifndef UNITY_SPECCUBE_LOD_STEPS
#define UNITY_SPECCUBE_LOD_STEPS 6
#endif

#ifdef LIGHTMAP_ON
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
#else
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
#endif

// The *approximated* version of the non-linear remapping. It works by
// approximating the cone of the specular lobe, and then computing the MIP map level
// which (approximately) covers the footprint of the lobe with a single texel.
// Improves the perceptual roughness distribution.
half PerceptualRoughnessToMipmapLevel(half perceptualRoughness, uint mipMapCount) {
    perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);

    return perceptualRoughness * mipMapCount;
}

half PerceptualRoughnessToMipmapLevel(half perceptualRoughness) {
    return PerceptualRoughnessToMipmapLevel(perceptualRoughness, UNITY_SPECCUBE_LOD_STEPS);
}

// TODO: Check if PI is correctly handled!
// Ref: "Efficient Evaluation of Irradiance Environment Maps" from ShaderX 2
half3 SHEvalLinearL0L1(half3 N, half4 shAr, half4 shAg, half4 shAb) {
    half4 vA = half4(N, 1.0);

    half3 x1;
    // Linear (L1) + constant (L0) polynomial terms
    x1.r = dot(shAr, vA);
    x1.g = dot(shAg, vA);
    x1.b = dot(shAb, vA);

    return x1;
}

half3 SHEvalLinearL2(half3 N, half4 shBr, half4 shBg, half4 shBb, half4 shC) {
    half3 x2;
    // 4 of the quadratic (L2) polynomials
    half4 vB = N.xyzz * N.yzzx;
    x2.r = dot(shBr, vB);
    x2.g = dot(shBg, vB);
    x2.b = dot(shBb, vB);

    // Final (5th) quadratic (L2) polynomial
    half vC = N.x * N.x - N.y * N.y;
    half3 x3 = shC.rgb * vC;

    return x2 + x3;
}

half3 SampleSH(half3 normalWS) {
    // Linear + constant polynomial terms
    half3 res = SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb);
    // Quadratic polynomials
    res += SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);

    return max(half3(0, 0, 0), res);
}


// Following functions are to sample enlighten lightmaps (or lightmaps encoded the same way as our
// enlighten implementation). They assume use of RGB9E5 for dynamic illuminance map and RGBM for baked ones.
// It is required for other platform that aren't supporting this format to implement variant of these functions
// (But these kind of platform should use regular render loop and not news shaders).

// TODO: This is the max value allowed for emissive (bad name - but keep for now to retrieve it) (It is 8^2.2 (gamma) and 8 is the limit of punctual light slider...), comme from UnityCg.cginc. Fix it!
// Ask Jesper if this can be change for HDRenderPipeline
#define EMISSIVE_RGBM_SCALE 97.0

// RGBM stuff is temporary. For now baked lightmap are in RGBM and the RGBM range for lightmaps is specific so we can't use the generic method.
// In the end baked lightmaps are going to be BC6H so the code will be the same as dynamic lightmaps.
// Same goes for emissive packed as an input for Enlighten with another hard coded multiplier.

// TODO: This function is used with the LightTransport pass to encode lightmap or emissive
real4 PackEmissiveRGBM(real3 rgb) {
    real kOneOverRGBMMaxRange = 1.0 / EMISSIVE_RGBM_SCALE;
    const real kMinMultiplier = 2.0 * 1e-2;

    real4 rgbm = real4(rgb * kOneOverRGBMMaxRange, 1.0);
    rgbm.a = max(max(rgbm.r, rgbm.g), max(rgbm.b, kMinMultiplier));
    rgbm.a = ceil(rgbm.a * 255.0) / 255.0;

    // Division-by-zero warning from d3d9, so make compiler happy.
    rgbm.a = max(rgbm.a, kMinMultiplier);

    rgbm.rgb /= rgbm.a;
    return rgbm;
}

real3 UnpackLightmapRGBM(real4 rgbmInput, real4 decodeInstructions) {
#ifdef UNITY_COLORSPACE_GAMMA
    return rgbmInput.rgb * (rgbmInput.a * decodeInstructions.x);
#else
    return rgbmInput.rgb * (PositivePow(rgbmInput.a, decodeInstructions.y) * decodeInstructions.x);
#endif
}

real3 UnpackLightmapDoubleLDR(real4 encodedColor, real4 decodeInstructions) {
    return encodedColor.rgb * decodeInstructions.x;
}

real3 DecodeLightmap(real4 encodedIlluminance, real4 decodeInstructions) {
#if defined(UNITY_LIGHTMAP_RGBM_ENCODING)
    return UnpackLightmapRGBM(encodedIlluminance, decodeInstructions);
#elif defined(UNITY_LIGHTMAP_DLDR_ENCODING)
    return UnpackLightmapDoubleLDR(encodedIlluminance, decodeInstructions);
#else // (UNITY_LIGHTMAP_FULL_HDR)
    return encodedIlluminance.rgb;
#endif
}

real3 DecodeHDREnvironment(real4 encodedIrradiance, real4 decodeInstructions) {
    // Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    real alpha = max(decodeInstructions.w * (encodedIrradiance.a - 1.0) + 1.0, 0.0);

    // If Linear mode is not supported we can skip exponent part
    return (decodeInstructions.x * PositivePow(alpha, decodeInstructions.y)) * encodedIrradiance.rgb;
}

half EnvSpecularPhongApprox(half roughness, half RoL, half fac2) {
    half a2 = roughness * roughness;
    half rcp_a2 = rcp(a2);
    half c = 0.72134752 * rcp_a2 + 0.39674113;
    half p = rcp_a2 * exp2((c * RoL - c) * fac2);
    return min(p, rcp_a2) * INV_PI;
}

// Sample baked lightmap. Non-Direction and Directional if available.
// Realtime GI is not supported.
void SampleLightmap(float2 lightmapUV, half roughness, half3 normalWS, half3 viewWS, out half3 indirectDiffuse, out half3 indirectSpecular) {
    half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
    half3 illuminance = half3(0.0, 0.0, 0.0);
    // Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
#ifdef UNITY_LIGHTMAP_FULL_HDR
    illuminance = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, lightmapUV).rgb;
#else
    half4 encodedIlluminance = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, lightmapUV).rgba;
    illuminance = DecodeLightmap(encodedIlluminance, decodeInstructions);
#endif

#ifdef DIRLIGHTMAP_COMBINED
    half4 direction = SAMPLE_TEXTURE2D(unity_LightmapInd, samplerunity_Lightmap, lightmapUV);
    direction.xyz = direction.xyz * 2.0 - 1.0;
    half lambert = max(0, dot(normalWS, direction.xyz));
    half rebalancingCoeff = max(1e-4, direction.w);
    indirectDiffuse = illuminance * lambert / rebalancingCoeff;
    // fake indirect specular lighting inspired by Enlighten. we do not use original phong shading model because we need to take roughness into account
    // ref: https://enlighten.atlassian.net/wiki/spaces/SDK311/pages/1127133641/Directional+irradiance
    half3 reflectVector = reflect(-viewWS, normalWS);
    // Extract the light distribution factor from the length.
    half fac = length(direction.xyz);
    half fac2 = fac * fac;
    half RoL = max(dot(reflectVector, normalize(direction.xyz)), 0.0f);
    half specular = EnvSpecularPhongApprox(roughness, RoL, fac2) * fac2;
    indirectSpecular = illuminance * specular;
#else
    indirectDiffuse = illuminance;
    indirectSpecular = half3(0, 0, 0);
#endif
}

// reasons for using analytical dfg approximation instead of brdf lut map
// lut map cannot be compressed, and texture fetching is not bandwidth friendly for mobile platform
// TODO: test performance for variant devices
// ref: https://www.unrealengine.com/zh-CN/blog/physically-based-shading-on-mobile
// another approx ref: https://knarkowicz.wordpress.com/2014/12/27/analytical-dfg-term-for-ibl/

half2 DFGApproxV1(half Roughness, half NoV) {
    const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
    const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
    half4 r = Roughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;
    // below is the origin version that returns value multiplied by specularColor
    // we need to consider energy compensation, so multiply specularColor(f0) outside of the DFGApprox function
    // return SpecularColor * AB.x + AB.y;
    return AB;
}

// ref: siggraph 2013, PBS Black Ops2 COD
half2 DFGApproxV2(half Roughness, half NoV) {
    const half4 c0 = { 1.04, 0.475, 0.0182, 0.25 };
    const half4 c1 = { 0, 0, -0.0156, 0.75 };
    half4 t = c0 * (1 - Roughness) + c1;
    return half2(t.w, t.x * min(t.y, exp2(-9.28 * NoV)) + t.z);
}

half3 GlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness) {
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);
#if !defined(UNITY_USE_NATIVE_HDR)
    half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
    half3 irradiance = encodedIrradiance.rbg;
#endif
    return irradiance;
}

half ComputeSpecularAO(half NoV, half visibility, half roughness) {
    // Lagarde and de Rousiers 2014, "Moving Frostbite to PBR"
    return saturate(pow(NoV + visibility, exp2(-16.0 * roughness - 1.0)) - 1.0 + visibility);
}

#endif // XRP_GLOBALILLUMINATION_INCLUDED
