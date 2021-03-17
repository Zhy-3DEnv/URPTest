#ifndef LIT_EXTENSION_INCLUDED
#define LIT_EXTENSION_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Shading.hlsl"

half4 Test() {
    return half4(0, 1, 0, 1);
}

half4 TestLight() {
    Light light = GetMainLight();
    return light.colorIntensity;
}

float3 GetDiffuse(float3 lightColor, float3 diffuseFactor, float atte, float3 normalValue, float3 lightDirection) {
    return lightColor.rgb * diffuseFactor.rgb * atte * max(0, dot(normalValue.xyz, lightDirection.xyz));
}

float3 GetDiffuseWithSH(float3 lightColor, float3 diffuseFactor, float atte, float3 normalValue, float3 lightDirection) {
    half3 indirectDiffuseLight = SampleSH(normalValue);
    //return indirectDiffuseLight;
    return lightColor.rgb * diffuseFactor.rgb * atte * max(0, dot(normalValue.xyz, lightDirection.xyz)) + indirectDiffuseLight * diffuseFactor;
}

#endif
