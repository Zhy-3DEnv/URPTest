#ifndef XRP_OVERRIDE_DEPTH_PASS_INCLUDED
#define XRP_OVERRIDE_DEPTH_PASS_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

struct Attributes {
    float4 position     : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings OverrideDepthVertex(Attributes input) {
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    return output;
}

float FillNearestDepthFragment(Varyings input) : SV_DEPTH {
#if UNITY_REVERSED_Z
    return 1;
#else
    return 0;
#endif
}

float FillFarestDepthFragment(Varyings input) : SV_DEPTH {
#if UNITY_REVERSED_Z
    return 0;
#else
    return 1;
#endif
}


half4 FillOriginDepthFragment() : SV_TARGET {
    return 0;
}

#endif
