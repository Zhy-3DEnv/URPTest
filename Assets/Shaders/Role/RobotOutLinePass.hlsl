#ifndef ROBOT_OUTLINEPASS_INCLUDED
#define ROBOT_OUTLINEPASS_INCLUDED
#define _OffsetDistance 1

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Shading.hlsl"

struct Attributes{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    #ifdef _LINESCALE
    float2 lightmapUV   : TEXCOORD1;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 positionCS   : SV_POSITION;
    float fogCoord      : TEXCOORD0;
    float3 viewDirWS    : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float distanceOfCamera : TEXCOORD3;
    float4 screenPos     : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings VertexOutLine(Attributes input){
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    
    #if _LINESCALE
    float center = _ScaleCenter * 2 - 1;
    float Scaledamp = saturate((abs(saturate(input.lightmapUV.y + center) * 2 - 1) - _ScaleWidth)/_Damping);
    input.positionOS.xyz += input.normalOS * _ScaleSize * Scaledamp;
    #endif

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = vertexInput.positionCS;
    float3 vnormal = mul((float3x3)UNITY_MATRIX_IT_MV, input.normalOS);
    vnormal = normalize(vnormal);
    float2 offset = mul((float2x2)UNITY_MATRIX_P, vnormal.xy);
    output.positionCS.xy += offset * _OutLineWidth * 0.1;

    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    output.fogCoord = fogFactor;

    float3 objectPos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
    output.distanceOfCamera = distance(objectPos, _WorldSpaceCameraPos) * 0.01;

    
    output.screenPos = ComputeScreenPosition(output.positionCS);
    return output;
}

half4 FragmentOutLine(Varyings input) : SV_Target{
    UNITY_SETUP_INSTANCE_ID(input);

    float2 uv = input.screenPos.xy / input.screenPos.w;

    half4 finalcol = _OutLineColor;
    finalcol.rgb = MixFog(finalcol.rgb, input.fogCoord);
    finalcol.a = 1;
    return finalcol;
}
#endif
