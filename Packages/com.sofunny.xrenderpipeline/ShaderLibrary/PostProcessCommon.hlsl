#ifndef XRP_POSTPROCESS_COMMON_INCLUDED
#define XRP_POSTPROCESS_COMMON_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Color.hlsl"

struct Attributes {
    float4 positionOS   : POSITION;
    float2 uv           : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float4 positionCS    : SV_POSITION;
    float2 uv            : TEXCOORD0;
};

Varyings Vert(Attributes input) {
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv;
    return output;
}

// ----------------------------------------------------------------------------------
// Render fullscreen mesh by using a matrix set directly by the pipeline instead of
// relying on the matrix set by the C++ engine to avoid issues with XR
float4x4 _FullscreenProjMat;

float4 TransformFullscreenMesh(half3 positionOS) {
    return mul(_FullscreenProjMat, half4(positionOS, 1));
}

Varyings VertFullscreenMesh(Attributes input) {
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    output.positionCS = TransformFullscreenMesh(input.positionOS.xyz);
    output.uv = input.uv;
    return output;
}

// ----------------------------------------------------------------------------------
// Samplers

SAMPLER(sampler_LinearClamp);
SAMPLER(sampler_LinearRepeat);
SAMPLER(sampler_PointClamp);
SAMPLER(sampler_PointRepeat);

// ----------------------------------------------------------------------------------
// Utility functions

half GetLuminance(half3 colorLinear) {
#if _TONEMAPPING_ACES
    return AcesLuminance(colorLinear);
#else
    return Luminance(colorLinear);
#endif
}

half3 ApplyTonemap(half3 color) {
#if _TONEMAPPING_ACES
    color = AcesTonemap(color);
#elif _TONEMAPPING_NEUTRAL
    color = NeutralTonemap(color);
#elif _TONEMAPPING_UCHIMURA
    color = UchimuraTonemap(color);
#endif
    return saturate(color);
}

half3 ApplyColorGrading(half3 input, float postExposure, TEXTURE2D_PARAM(lutTex, lutSampler), float3 lutParams, TEXTURE2D_PARAM(userLutTex, userLutSampler), float3 userLutParams, float userLutContrib) {
    // Artist request to fine tune exposure in post without affecting bloom, dof etc
    input *= postExposure;

    #if _HDR_GRADING
    // HDR Grading:
    //   - Apply internal LogC LUT
    //   - (optional) Clamp result & apply user LUT
    float3 inputLutSpace = saturate(LinearToLogC(input)); // LUT space is in LogC
    input = ApplyLut2D(TEXTURE2D_ARGS(lutTex, lutSampler), inputLutSpace, lutParams);

    UNITY_BRANCH
    if (userLutContrib > 0.0) {
        input = saturate(input);
        input.rgb = LinearToSRGB(input.rgb); // In LDR do the lookup in sRGB for the user LUT
        half3 outLut = ApplyLut2D(TEXTURE2D_ARGS(userLutTex, userLutSampler), input, userLutParams);
        input = lerp(input, outLut, userLutContrib);
        input.rgb = SRGBToLinear(input.rgb);
    }
    #else
    // LDR Grading:
    //   - Apply tonemapping (result is clamped)
    //   - (optional) Apply user LUT
    //   - Apply internal linear LUT
    input = ApplyTonemap(input);

    UNITY_BRANCH
    if (userLutContrib > 0.0) {
        input.rgb = LinearToSRGB(input.rgb); // In LDR do the lookup in sRGB for the user LUT
        half3 outLut = ApplyLut2D(TEXTURE2D_ARGS(userLutTex, userLutSampler), input, userLutParams);
        input = lerp(input, outLut, userLutContrib);
        input.rgb = SRGBToLinear(input.rgb);
    }

    input = ApplyLut2D(TEXTURE2D_ARGS(lutTex, lutSampler), input, lutParams);
    #endif

    return input;
}

half3 ApplyGrain(half3 input, float2 uv, TEXTURE2D_PARAM(GrainTexture, GrainSampler), float intensity, float response, float2 scale, float2 offset) {
    // Grain in range [0;1] with neutral at 0.5
    half grain = SAMPLE_TEXTURE2D(GrainTexture, GrainSampler, uv * scale + offset).w;

    // Remap [-1;1]
    grain = (grain - 0.5) * 2.0;

    // Noisiness response curve based on scene luminance
    float lum = 1.0 - sqrt(Luminance(input));
    lum = lerp(1.0, lum, response);

    return input + input * grain * intensity * lum;
}

#endif // XRP_POSTPROCESS_COMMON_INCLUDED
