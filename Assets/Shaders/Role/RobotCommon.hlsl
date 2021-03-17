#ifndef ROBOT_COMMON_INCLUDED
#define ROBOT_COMMON_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Shading.hlsl"

struct Attributes{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD2;
#endif

    float4 normalWS                 : TEXCOORD3;
    float4 tangentWS                : TEXCOORD4;
    float4 bitangentWS              : TEXCOORD5;

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

void InitializeInputData(Varyings input, half3 normalTS, out LitInputData inputData){
    inputData = (LitInputData)0;

#ifdef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz));

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

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input){
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
 
#ifdef _METALSCALE
    input.positionOS.xyz += input.normalOS * _ScaleSize ;
#endif

#ifdef _LINESCALE
    float center = _ScaleCenter * 2 - 1;
    float Scaledamp = saturate((abs(saturate(input.lightmapUV.y + center) * 2 - 1) - _ScaleWidth)/_Damping);
    input.positionOS.xyz += input.normalOS * _ScaleSize * Scaledamp;
#endif

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    
    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    half3 vertexLight = half3(0,0,0);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv.xy = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.uv.zw = input.lightmapUV;

    // already normalized from normal transform to WS.
    output.normalWS = half4(normalInput.normalWS, viewDirWS.x);
    output.tangentWS = half4(normalInput.tangentWS, viewDirWS.y);
    output.bitangentWS = half4(normalInput.bitangentWS, viewDirWS.z);

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);

    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
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

// Used in Standard (Physically Based) shader
half4 LitPassFragment(Varyings input) : SV_Target{
    UNITY_SETUP_INSTANCE_ID(input);

    LitSurfaceData surfaceData;
    InitializeRobotLitSurfaceData(input.uv.xy, input.uv.zw, surfaceData);

    LitInputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    half4 color = XRPFragmentPBR(inputData, surfaceData);

    // Fresnel Effect
    half fresnel = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    int isEffectAction = step(0.0001, _FresnelExponent);
    color.rgb += (1 - pow(fresnel, max(0.0001, _FresnelExponent * 10))) * _FresnelColor.rgb * isEffectAction;
    color.rgb = saturate(color.rgb);

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = color.a;

    return color;
}

#endif
