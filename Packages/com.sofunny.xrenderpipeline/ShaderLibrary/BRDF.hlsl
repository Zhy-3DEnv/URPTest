#ifndef XRP_BRDF_INCLUDED
#define XRP_BRDF_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"

half D_GGX(half roughness, half NoH, half3 h, half3 normalWS) {
    // Walter et al. 2007, "Microfacet Models for Refraction through Rough Surfaces"

    // In mediump, there are two problems computing 1.0 - NoH^2
    // 1) 1.0 - NoH^2 suffers floating point cancellation when NoH^2 is close to 1 (highlights)
    // 2) NoH doesn't have enough precision around 1.0
    // Both problem can be fixed by computing 1-NoH^2 in highp and providing NoH in highp as well
    // However, we can do better using Lagrange's identity:
    //      ||a x b||^2 = ||a||^2 ||b||^2 - (a . b)^2
    // since N and H are unit vectors: ||N x H||^2 = 1.0 - NoH^2
    // This computes 1.0 - NoH^2 directly (which is close to zero in the highlights and has
    // enough precision).
    // Overall this yields better performance, keeping all computations in mediump
    half3 NxH = cross(normalWS, h);
    half oneMinusNoHSquared = dot(NxH, NxH);

    half a = NoH * roughness;
    half k = roughness / (oneMinusNoHSquared + a * a);
    half d = k * k;

#if defined(SHADER_API_MOBILE)
    return min(d, HALF_MAX);
#else
    return d;
#endif
}

half V_SmithGGXCorrelated_Fast(half roughness, half NoV, half NoL) {
    // Hammon 2017, "PBR Diffuse Lighting for GGX+Smith Microsurfaces"
    half v = 0.5 / lerp(2.0 * NoL * NoV, NoL + NoV, roughness);
    return saturate(v);
}

half3 F_Schlick(half3 f0, half VoH) {
    half f = pow(1.0 - VoH, 5.0);
    return f + f0 * (1.0 - f);
}

half Fd_Lambert() {
    return 1.0;
}

half3 SpecularBRDF_HighQ(half roughness, half3 f0, half3 normalWS, half3 h, half NoL, half NoV, half NoH, half LoH) {
    half D = D_GGX(roughness, NoH, h, normalWS);
    half V = V_SmithGGXCorrelated_Fast(roughness, NoV, NoL);
    half3 F = F_Schlick(f0, LoH);
    return (D * V) * F;
}

// UE4 default mobile ggx
half3 SpecularBRDF_MediumQ(half roughness, half perceptualRoughnessUnclamped, half3 f0, half3 normalWS, half3 h, half NoH) {
    half3 NxH = cross(normalWS, h);
    half oneMinusNoHSquared = dot(NxH, NxH);

    half n = NoH * roughness;
    half p = roughness / (oneMinusNoHSquared + n * n);
    half d = p * p;
    half clampedD = min(d, HALF_MAX);
    return f0 * (perceptualRoughnessUnclamped * 0.25 + 0.25) * clampedD;
}

// Phong Approx (UE4 legacy specular)
half3 SpecularBRDF_LowQ(half roughness, half3 f0, half3 viewDirectionWS, half3 normalWS, half3 lightDirectionWS) {
    half3 r = reflect(-viewDirectionWS, normalWS);
    half RoL = max(0, dot(r, lightDirectionWS));
    half a2 = roughness * roughness;
    half rcp_a2 = rcp(a2);

    // Spherical Gaussian approximation: pow( x, n ) ~= exp( (n + 0.775) * (x - 1) )
    // Phong: n = 0.5 / a2 - 0.5
    // 0.5 / ln(2), 0.275 / ln(2)
    half c = 0.72134752 * rcp_a2 + 0.39674113;
    half p = rcp_a2 * exp2(c * RoL - c);
    return f0 * min(p, rcp_a2);
}

#endif