#ifndef XRP_COMMONINPUT_INCLUDED
#define XRP_COMMONINPUT_INCLUDED

#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/UnityInput.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/SpaceTransforms.hlsl"

half4 _GlossyEnvironmentColor;
half4 _SubtractiveShadowColor;

float4x4 _InvCameraViewProj;
float4 _ScaledScreenParams;

#endif
