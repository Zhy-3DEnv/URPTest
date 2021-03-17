#ifndef XRP_LIT_FORWARD_PASS_INCLUDED
#define XRP_LIT_FORWARD_PASS_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Shading.hlsl"

struct Attributes {
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float2 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

#ifdef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    float3 positionWS               : TEXCOORD2;
#endif

#ifdef _USE_NORMALMAP
    float4 normalWS                 : TEXCOORD3;    // xyz: normal, w: viewDir.x
    float4 tangentWS                : TEXCOORD4;    // xyz: tangent, w: viewDir.y
    float4 bitangentWS              : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
#else // !_USE_NORMALMAP
    float3 normalWS                 : TEXCOORD3;
    float3 viewDirWS                : TEXCOORD4;
#endif

    half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    float4 shadowCoord              : TEXCOORD7;
#endif

#ifdef _USE_CLUSTER_LIGHTING
    float4 projectedPosition        : TEXCOORD8;
#endif
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

inline void InitializeLitInputData(Varyings input, half3 normalTS, half roughness, out LitInputData inputData) {
    inputData = (LitInputData)0;

#ifdef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    inputData.positionWS = input.positionWS;
#endif

#ifdef _USE_NORMALMAP
    half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz));
#else // !_USE_NORMALMAP
    half3 viewDirWS = input.viewDirWS;
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = normalize(inputData.normalWS);
    viewDirWS = SafeNormalize(viewDirWS);

    inputData.viewDirectionWS = viewDirWS;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#ifdef LIGHTMAP_ON
    inputData.lightmapUV = input.lightmapUV;
#endif

#ifdef _USE_CLUSTER_LIGHTING
    inputData.projectedPosition = input.projectedPosition;
#endif
}

Varyings LitPassVertex(Attributes input) {
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    half3 vertexLight = half3(0,0,0); // VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

#ifdef _USE_NORMALMAP
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS = normalize(normalInput.normalWS);
    output.viewDirWS = viewDirWS;
#endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);

    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

#ifdef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    output.positionWS = vertexInput.positionWS;
#endif

#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
    output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
#endif

#ifdef _USE_CLUSTER_LIGHTING
    output.projectedPosition = vertexInput.positionNDC;
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

half4 LitPassFragment(Varyings input) : SV_Target {

    UNITY_SETUP_INSTANCE_ID(input);

    LitSurfaceData surfaceData;
    InitializeLitSurfaceData(input.uv, surfaceData);

    LitInputData inputData;
    InitializeLitInputData(input, surfaceData.normalTS, surfaceData.roughness, inputData);

#ifdef _DEBUG_MODE
    DebugData debugData = (DebugData)0;
    half4 color = XRPFragmentPBR(inputData, surfaceData, debugData);

    int materialDebugMode;
#if defined(_DEBUG_MATERIAL)
    materialDebugMode = _IndividualMaterialDebugMode;
#elif defined (_DEBUG_PIPELINE)
    materialDebugMode = _PipelineMaterialDebugMode;
#endif
    if (materialDebugMode == DEBUG_BASECOLOR_ENUM) {
        return debugData.baseColor;
    } else if (materialDebugMode == DEBUG_METALLIC_ENUM) {
        return half4(debugData.metallic, debugData.metallic, debugData.metallic, 1);
    } else if (materialDebugMode == DEBUG_ROUGHNESS_ENUM) {
        return half4(debugData.roughness, debugData.roughness, debugData.roughness, 1);
    } else if (materialDebugMode == DEBUG_AMBIENTOCCLUSION_ENUM) {
        return half4(debugData.ambientOcclusion, debugData.ambientOcclusion, debugData.ambientOcclusion, 1);
    } else if (materialDebugMode == DEBUG_NORMAL_ENUM) {
        return half4(debugData.normalWS.x * 0.5 + 0.5, debugData.normalWS.y * 0.5 + 0.5, debugData.normalWS.z * 0.5 + 0.5, 1);
    } else if (materialDebugMode == DEBUG_VALIDATE_PBR_DIFFUSE_ENUM) {
        return ValidatePBRDiffuseColor(debugData.diffuseColor, debugData.metallic > 0);
    } else if (materialDebugMode == DEBUG_VALIDATE_PBR_SPECULAR_ENUM) {
        return ValidatePBRSpecularColor(debugData.diffuseColor, debugData.specularColor, debugData.metallic, debugData.metallic > 0);
    }

    half3 lightingDebug = half3(0, 0, 0);
    int lightingDebugMode = _PipelineLightingDebugMode;
    if ((lightingDebugMode & DEBUG_DIRECT_SPECULAR_ENUM) != 0) {
        lightingDebug += debugData.directSpecular;
    }
    if ((lightingDebugMode & DEBUG_INDIRECT_SPECULAR_ENUM) != 0) {
        lightingDebug += debugData.indirectSpecular;
    }
    if ((lightingDebugMode & DEBUG_DIRECT_DIFFUSE_ENUM) != 0) {
        lightingDebug += debugData.directDiffuse;
    }
    if ((lightingDebugMode & DEBUG_INDIRECT_DIFFUSE_ENUM) != 0) {
        lightingDebug += debugData.indirectDiffuse;
    }
    if (lightingDebugMode == DEBUG_LIGHTING_ALL_ENUM) {
        lightingDebug += debugData.emissionColor;
    }

    color.rgb = lightingDebug;
#else // !_DEBUG_MODE
    half4 color = XRPFragmentPBR(inputData, surfaceData);
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif