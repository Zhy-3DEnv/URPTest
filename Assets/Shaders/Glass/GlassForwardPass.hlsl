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

#ifdef _USE_REFRACTION
    float4 screenUV                 : TEXCOORD9;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

void InitializeInputData(Varyings input, half3 normalTS, out LitInputData inputData) {
    inputData = (LitInputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
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

Varyings LitPassVertex(Attributes input) {
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    half3 vertexLight = half3(0,0,0);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

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

#ifdef _USE_REFRACTION
    output.screenUV = ComputeScreenPosition(vertexInput.positionCS);
    output.screenUV.z = -TransformWorldToView(vertexInput.positionWS).z;
#endif

    return output;
}


half4 LitPassFragment(Varyings input) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(input);

    LitSurfaceData surfaceData;
    InitializeGlassLitSurfaceData(input.uv, surfaceData);

    LitInputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    
    half4 color = XRPFragmentPBR(inputData, surfaceData);

    half4 finalCol;

    half NdotV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half fresnelPow4 = Pow4(1.0 - NdotV);
    half fresnelTerm = pow(saturate(fresnelPow4 * (1 + _Thickness)), _Fresnel);
    half alpha = saturate(color.a + fresnelTerm);

    #ifdef _USE_REFRACTION
    float addValue = fresnelPow4 * 0.05 * pow(saturate(input.screenUV.z), 2);
    float2 opaqueUV = (input.screenUV.xy + addValue) / input.screenUV.w;
    half4 opaqueCol = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, opaqueUV);
    finalCol = lerp(opaqueCol, color, alpha);
    finalCol.a = 1;
    #else
    finalCol = color;
    finalCol.a = alpha;
    #endif

    #ifdef _USE_MATCAP
    half3 normalVS = TransformWorldToViewDir(inputData.normalWS);
    half3 matcap = SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, normalVS.xy * 0.5 + 0.5).rgb;
    finalCol.rgb += matcap * (1 - fresnelTerm) * surfaceData.metallic;
    #endif

    // Fresnel Effect
    half fresnel = saturate(NdotV);
    int isEffectAction = step(0.0001, _FresnelExponent);
    finalCol.rgb += (1 - pow(fresnel, max(0.0001, _FresnelExponent * 10))) * _FresnelColor.rgb * isEffectAction;
    finalCol.rgb = saturate(finalCol.rgb);

    finalCol.rgb = MixFog(finalCol.rgb, inputData.fogCoord);
    return finalCol;
}
