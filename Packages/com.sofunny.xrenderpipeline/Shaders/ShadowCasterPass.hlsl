#ifndef XRP_SHADOW_CASTER_PASS_INCLUDED
#define XRP_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

float3 _LightDirection;
half4 _ShadowBias; // x: depth bias, y: normal bias

struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
};

float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection) {
    float invNoL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNoL * _ShadowBias.y;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = lightDirection * _ShadowBias.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

float4 GetShadowPositionHClip(Attributes input) {
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

Varyings ShadowPassVertex(Attributes input) {
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);

    output.uv = input.texcoord;
    output.positionCS = GetShadowPositionHClip(input);
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET {
    half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
#ifdef _USE_ALPHATEST
    clip(color.a - _Cutoff);
#endif

    return 0;
}

#endif // XRP_SHADOW_CASTER_PASS_INCLUDED