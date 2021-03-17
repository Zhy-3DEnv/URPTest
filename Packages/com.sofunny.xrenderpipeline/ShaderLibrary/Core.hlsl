#ifndef XRP_CORE_INCLUDED
#define XRP_CORE_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Packing.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/CommonInput.hlsl"

// if shader quality not defined by xrp, use high quality
#if !defined(SHADER_QUALITY_HIGH) && !defined(SHADER_QUALITY_MEDIUM) && !defined(SHADER_QUALITY_LOW)
#define SHADER_QUALITY_HIGH
#endif

#if UNITY_REVERSED_Z
#if SHADER_API_OPENGL || SHADER_API_GLES || SHADER_API_GLES3
// GL with reversed z => z clip range is [near, -far] -> should remap in theory but dont do it in practice to save some perf (range is close enough)
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(-(coord), 0)
#else
// D3D with reversed Z => z clip range is [near, 0] -> remapping to [0, far]
// max is required to protect ourselves from near plane not being correct/meaningfull in case of oblique matrices.
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(((1.0-(coord)/_ProjectionParams.y)*_ProjectionParams.z),0)
#endif
#elif UNITY_UV_STARTS_AT_TOP
// D3D without reversed z => z clip range is [0, far] -> nothing to do
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#else
// OpenGL => z clip range is [-near, far] -> should remap in theory but dont do it in practice to save some perf (range is close enough)
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#endif

struct VertexPositionInputs {
    float3 positionWS; // World space position
    float3 positionVS; // View space position
    float4 positionCS; // Homogeneous clip space position
    float4 positionNDC;// Homogeneous normalized device coordinates
};

struct VertexNormalInputs {
    real3 tangentWS;
    real3 bitangentWS;
    float3 normalWS;
};

VertexPositionInputs GetVertexPositionInputs(float3 positionOS) {
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

VertexNormalInputs GetVertexNormalInputs(float3 normalOS) {
    VertexNormalInputs tbn;
    tbn.tangentWS = real3(1.0, 0.0, 0.0);
    tbn.bitangentWS = real3(0.0, 1.0, 0.0);
    tbn.normalWS = TransformObjectToWorldNormal(normalOS);
    return tbn;
}

VertexNormalInputs GetVertexNormalInputs(float3 normalOS, float4 tangentOS) {
    VertexNormalInputs tbn;

    // mikkts space compliant. only normalize when extracting normal at frag.
    real sign = tangentOS.w * GetOddNegativeScale();
    tbn.normalWS = TransformObjectToWorldNormal(normalOS);
    tbn.tangentWS = TransformObjectToWorldDir(tangentOS.xyz);
    tbn.bitangentWS = cross(tbn.normalWS, tbn.tangentWS) * sign;
    return tbn;
}

float3 GetWorldSpaceViewDir(float3 positionWS) {
    if (unity_OrthoParams.w == 0) {
        // perspective projection
        return _WorldSpaceCameraPos - positionWS;
    } else {
        // orthographic projection
        return UNITY_MATRIX_V[2].xyz;
    }
}

real ComputeFogFactor(float z) {
    float clipZ_01 = UNITY_Z_0_FAR_FROM_CLIPSPACE(z);

#if defined(FOG_LINEAR)
    // factor = (end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
    float fogFactor = saturate(clipZ_01 * unity_FogParams.z + unity_FogParams.w);
    return real(fogFactor);
#elif defined(FOG_EXP) || defined(FOG_EXP2)
    // factor = exp(-(density*z)^2)
    // -density * z computed at vertex
    return real(unity_FogParams.x * clipZ_01);
#else
    return 0.0h;
#endif
}

real ComputeFogIntensity(real fogFactor) {
    real fogIntensity = 0.0h;
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
#if defined(FOG_EXP)
    // factor = exp(-density*z)
    // fogFactor = density*z compute at vertex
    fogIntensity = saturate(exp2(-fogFactor));
#elif defined(FOG_EXP2)
    // factor = exp(-(density*z)^2)
    // fogFactor = density*z compute at vertex
    fogIntensity = saturate(exp2(-fogFactor * fogFactor));
#elif defined(FOG_LINEAR)
    fogIntensity = fogFactor;
#endif
#endif
    return fogIntensity;
}

half3 MixFogColor(real3 fragColor, real3 fogColor, real fogFactor) {
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
    real fogIntensity = ComputeFogIntensity(fogFactor);
    fragColor = lerp(fogColor, fragColor, fogIntensity);
#endif
    return fragColor;
}

half3 MixFog(real3 fragColor, real fogFactor) {
    return MixFogColor(fragColor, unity_FogColor.rgb, fogFactor);
}

#endif
