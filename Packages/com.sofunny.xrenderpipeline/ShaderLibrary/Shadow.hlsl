#ifndef XRP_SHADOW_INCLUDED
#define XRP_SHADOW_INCLUDED

#define MAX_SHADOW_CASCADES 4

#if defined(_RECEIVE_SHADOWS)
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
        #define MAIN_LIGHT_CALCULATE_SHADOWS

        #if !defined(_MAIN_LIGHT_SHADOWS_CASCADE)
            #define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
        #endif
    #endif
#endif

TEXTURE2D_SHADOW(_MainLightShadowmap);
SAMPLER_CMP(sampler_MainLightShadowmap);

// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef SHADER_API_GLES3
CBUFFER_START(MainLightShadows)
#endif
// Last cascade is initialized with a no-op matrix. It always transforms
// shadow coord to half3(0, 0, NEAR_PLANE). We use this trick to avoid
// branching since ComputeCascadeIndex can return cascade index = MAX_SHADOW_CASCADES
float4x4    _MainLightWorldToShadow[MAX_SHADOW_CASCADES + 1];
float4      _CascadeShadowSplitSpheres0;
float4      _CascadeShadowSplitSpheres1;
float4      _CascadeShadowSplitSpheres2;
float4      _CascadeShadowSplitSpheres3;
float4      _CascadeShadowSplitSphereRadii;
half4       _MainLightShadowOffset01; // xy: offset0, zw: offset1
half4       _MainLightShadowOffset23; // xy: offset2, zw: offset3
half4       _MainLightShadowParams;  // x: shadowStrength, y: 1.0 if soft shadows, 0.0 otherwise, z: oneOverFadeDist, w: minusStartFade
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

#define BEYOND_SHADOW_FAR(shadowCoord) shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0

half MainLightRealtimeShadow(float4 shadowCoord) {
    half attenuation;

#ifdef _SOFT_SHADOWS
    // 4-tap hardware comparison
    half4 attenuation4;
    attenuation4.x = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, shadowCoord.xyz + float3(_MainLightShadowOffset01.xy, 0));
    attenuation4.y = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, shadowCoord.xyz + float3(_MainLightShadowOffset01.zw, 0));
    attenuation4.z = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, shadowCoord.xyz + float3(_MainLightShadowOffset23.xy, 0));
    attenuation4.w = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, shadowCoord.xyz + float3(_MainLightShadowOffset23.zw, 0));
    attenuation = dot(attenuation4, 0.25);
#else
    // 1-tap hardware comparison
    attenuation = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, shadowCoord.xyz);
#endif

    attenuation = (1.0f - _MainLightShadowParams.x) + attenuation * _MainLightShadowParams.x; // LerpWhiteTo(attenuation, _MainLightShadowStrength);
    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.

    return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
}

half ComputeCascadeIndex(float3 positionWS) {
    float3 fromCenter0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    float3 fromCenter1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    float3 fromCenter2 = positionWS - _CascadeShadowSplitSpheres2.xyz;
    float3 fromCenter3 = positionWS - _CascadeShadowSplitSpheres3.xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    half4 weights = half4(distances2 < _CascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return 4 - dot(weights, half4(4, 3, 2, 1));
}

float4 TransformWorldToShadowCoord(float3 positionWS) {
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    half cascadeIndex = 0;
#endif
    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));
    return shadowCoord;
}


#endif // XRP_SHADOW_INCLUDED