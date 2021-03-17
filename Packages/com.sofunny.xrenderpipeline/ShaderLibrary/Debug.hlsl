#ifndef XRP_DEBUG_INCLUDED
#define XRP_DEBUG_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Color.hlsl"

struct DebugData {
    half4 baseColor;
    half3 emissionColor;
    half metallic;
    half roughness;
    half ambientOcclusion;
    half3 normalWS;
    half3 directSpecular;
    half3 indirectSpecular;
    half3 directDiffuse;
    half3 indirectDiffuse;
    half3 diffuseColor;
    half3 specularColor;
};

#if defined(_DEBUG_MATERIAL) || defined(_DEBUG_PIPELINE)
#define _DEBUG_MODE 1
#endif

// keep in sync with XRenderPipelineAsset.cs enum MaterialDebugMode
#define DEBUG_BASECOLOR_ENUM                  1
#define DEBUG_METALLIC_ENUM                   2
#define DEBUG_ROUGHNESS_ENUM                  3
#define DEBUG_AMBIENTOCCLUSION_ENUM           4
#define DEBUG_NORMAL_ENUM                     5
#define DEBUG_VALIDATE_PBR_DIFFUSE_ENUM       6
#define DEBUG_VALIDATE_PBR_SPECULAR_ENUM      7

#define DEBUG_DIRECT_SPECULAR_ENUM    (1 << 0)
#define DEBUG_INDIRECT_SPECULAR_ENUM  (1 << 1)
#define DEBUG_DIRECT_DIFFUSE_ENUM     (1 << 2)
#define DEBUG_INDIRECT_DIFFUSE_ENUM   (1 << 3)
#define DEBUG_LIGHTING_ALL_ENUM  (DEBUG_DIRECT_SPECULAR_ENUM | DEBUG_INDIRECT_SPECULAR_ENUM | DEBUG_DIRECT_DIFFUSE_ENUM | DEBUG_INDIRECT_DIFFUSE_ENUM)

int _PipelineMaterialDebugMode;
int _PipelineLightingDebugMode;
int _IndividualMaterialDebugMode;
half4 _DebugValidatePureMetalColor; // x: whether validate pure metal or not, yzw: debug color
half4 _DebugValidateHighColor;
half4 _DebugValidateLowColor;

// Adopt same validation function from HDRP
// Define bounds value in linear RGB for fresnel0 values
// Note: "static const" qualifier is mandatory, "const" alone doesn't work
static const float dieletricMin = 0.02;
static const float dieletricMax = 0.07;
static const float conductorMin = 0.45;
static const float conductorMax = 1.00;
static const float albedoMin    = 0.012;
static const float albedoMax    = 0.9;
static const float validateMetalSpecularThreshold = 0.95;

half4 ValidatePBRDiffuseColor(half3 diffuseColor, bool isMetal) {
    half3 untouched = Luminance(diffuseColor).xxx; // if no errors, leave color as it was in render
    // When checking full range we do not take the luminance but the mean because often in game blue color are highlight as too low whereas this is what we are looking for.
    half diffuseMean = dot(diffuseColor, half3(0.3333, 0.3333, 0.3333));
    // Check if we are pure metal with black albedo
    if (_DebugValidatePureMetalColor.x > 0.0 && isMetal && diffuseMean != 0.0) {
        return half4(_DebugValidatePureMetalColor.yzw, 1);
    }
    // If we have a metallic object, don't complain about low albedo
    if (!isMetal && diffuseMean < albedoMin) {
       return _DebugValidateLowColor;
    } else if (diffuseMean > albedoMax) {
        return _DebugValidateHighColor;
    } else {
       return half4(untouched, 1);
    }
    return half4(untouched, 1);
}

half4 ValidatePBRSpecularColor(half3 diffuseColor, half3 specularColor, half metallic, bool isMetal) {
    // When checking full range we do not take the luminance but the mean because often in game blue color are highlight as too low whereas this is what we are looking for.
    half specularMean = dot(specularColor, half3(0.3333,0.3333,0.3333));
    half4 outColor = half4(Luminance(diffuseColor.xyz).xxx, 1.0f);
    bool shouldValidateSpecularLowColor = metallic > validateMetalSpecularThreshold;
    if (specularMean < conductorMin && shouldValidateSpecularLowColor) {
        outColor = _DebugValidateLowColor;
    } else if (specularMean > conductorMax) {
        outColor = _DebugValidateHighColor;
    } else if (isMetal) {
       if (_DebugValidatePureMetalColor.x > 0.0) {
            outColor = dot(diffuseColor.xyz, half3(1,1,1)) == 0 ? outColor : half4(_DebugValidatePureMetalColor.yzw, 0);
       }
    }
    return outColor;
}

#endif // XRP_DEBUG_INCLUDED