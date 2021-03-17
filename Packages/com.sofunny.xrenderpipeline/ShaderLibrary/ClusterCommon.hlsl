#ifndef XRP_CLUSTER_COMMON_INCLUDED
#define XRP_CLUSTER_COMMON_INCLUDED

#if defined(SHADER_API_GLES3) || defined(SHADER_API_METAL)
#define USE_CBUFFER_FOR_CLUSTERED_SHADING 0
#else
#define USE_CBUFFER_FOR_CLUSTERED_SHADING 1
#endif

// keep in sync with maxVisibleLightsPerCluser in GPUClusterLights.cs
#define MAX_VISIBLE_LIGHTS_PER_CLUSTER 16
#define MAX_VISIBLE_POINTLIGHT_COUNT 256
#define MAX_VISIBLE_SPOTLIGHT_COUNT 256
#define TOTAL_CLUSTER_COUNT (16 * 8 * 16)

// for constant buffer usage
#define PREFERRED_CBUFFER_SIZE (64 * 1024) // preferred constant buffer size: 64kb, TODO: check all hw limits
#define MAX_VEC4_COUNT_CBUFFER (PREFERRED_CBUFFER_SIZE / 16)
#define POINTLIGHT_VEC4_SIZE 2
#define SPOTLIGHT_VEC4_SIZE 4

// keep in sync with structs in ClusteredLighting.cs
// for structured buffer usage
struct ClusterAABB {
    float4 minPt;
    float4 maxPt;
};

struct PointLight {
    float4 positionAndRange;
    float4 colorIntensity;
};

struct SpotLight {
    float4 positionAndRange;
    float4 colorIntensity;
    float4 spotDirAndAngle;
    float4 attenuation;
};

struct LightGrid {
    uint offset;
    uint count; // high 16 bits: spot light count, low 16 bits : point light count
};

#endif // XRP_CLUSTER_COMMON_INCLUDED