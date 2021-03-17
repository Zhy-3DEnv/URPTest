#ifndef XRP_LIT_META_PASS_INCLUDED
#define XRP_LIT_META_PASS_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/MetaInput.hlsl"

struct Attributes {
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
#ifdef _TANGENT_TO_WORLD
    float4 tangentOS     : TANGENT;
#endif
};

struct Varyings {
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
};

Varyings VertexMeta(Attributes input) {
    Varyings output;
    output.positionCS = MetaVertexPosition(input.positionOS, input.uv1, input.uv2,
        unity_LightmapST, unity_DynamicLightmapST);
    output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
    return output;
}

half4 FragmentMeta(Varyings input) : SV_Target {
    LitSurfaceData surfaceData;
    InitializeLitSurfaceData(input.uv, surfaceData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, 1.0, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuseColor + brdfData.f0 * brdfData.roughness * 0.5;
#ifdef _USE_EMISSION
    metaInput.Emission = surfaceData.emissionColor;
#else
    metaInput.Emission = half3(0, 0, 0);
#endif
    return MetaFragment(metaInput);
}

#endif
