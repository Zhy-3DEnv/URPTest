#ifndef UNITY_INSTANCING_INCLUDED
#define UNITY_INSTANCING_INCLUDED

#if SHADER_TARGET >= 35 && (defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL))
#define UNITY_SUPPORT_INSTANCING
#endif

// These platforms support dynamically adjusting the instancing CB size according to the current batch.
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
#define UNITY_INSTANCING_SUPPORT_FLEXIBLE_ARRAY_SIZE
#endif

#if defined(SHADER_TARGET_SURFACE_ANALYSIS) && defined(UNITY_SUPPORT_INSTANCING)
#undef UNITY_SUPPORT_INSTANCING
#endif

////////////////////////////////////////////////////////
// instancing paths
// - UNITY_INSTANCING_ENABLED               Defined if instancing path is taken.
// - UNITY_PROCEDURAL_INSTANCING_ENABLED    Defined if procedural instancing path is taken.
// - UNITY_ANY_INSTANCING_ENABLED           Defined if any instancing path is taken
#if defined(UNITY_SUPPORT_INSTANCING) && defined(INSTANCING_ON)
#define UNITY_INSTANCING_ENABLED
#endif
#if defined(UNITY_SUPPORT_INSTANCING) && defined(PROCEDURAL_INSTANCING_ON)
#define UNITY_PROCEDURAL_INSTANCING_ENABLED
#endif

#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
#define UNITY_ANY_INSTANCING_ENABLED 1
#else
#define UNITY_ANY_INSTANCING_ENABLED 0
#endif

#if defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
    // These platforms have constant buffers disabled normally, but not here (see CBUFFER_START/CBUFFER_END in HLSLSupport.cginc).
#define UNITY_INSTANCING_CBUFFER_SCOPE_BEGIN(name)  cbuffer name {
#define UNITY_INSTANCING_CBUFFER_SCOPE_END          }
#else
#define UNITY_INSTANCING_CBUFFER_SCOPE_BEGIN(name)  CBUFFER_START(name)
#define UNITY_INSTANCING_CBUFFER_SCOPE_END          CBUFFER_END
#endif

////////////////////////////////////////////////////////
// basic instancing setups
// - UNITY_VERTEX_INPUT_INSTANCE_ID     Declare instance ID field in vertex shader input / output struct.
// - UNITY_GET_INSTANCE_ID              (Internal) Get the instance ID from input struct.
#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)

// A global instance ID variable that functions can directly access.
static uint unity_InstanceID;

// Don't make UnityDrawCallInfo an actual CB on GL
#if !defined(SHADER_API_GLES3) && !defined(SHADER_API_GLCORE)
UNITY_INSTANCING_CBUFFER_SCOPE_BEGIN(UnityDrawCallInfo)
#endif
int unity_BaseInstanceID;
int unity_InstanceCount;
#if !defined(SHADER_API_GLES3) && !defined(SHADER_API_GLCORE)
UNITY_INSTANCING_CBUFFER_SCOPE_END
#endif

#define DEFAULT_UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;
#define UNITY_GET_INSTANCE_ID(input)    input.instanceID

#else

#define DEFAULT_UNITY_VERTEX_INPUT_INSTANCE_ID

#endif // UNITY_INSTANCING_ENABLED || UNITY_PROCEDURAL_INSTANCING_ENABLED

#if !defined(UNITY_VERTEX_INPUT_INSTANCE_ID)
#   define UNITY_VERTEX_INPUT_INSTANCE_ID DEFAULT_UNITY_VERTEX_INPUT_INSTANCE_ID
#endif


////////////////////////////////////////////////////////
// - UNITY_SETUP_INSTANCE_ID        Should be used at the very beginning of the vertex shader / fragment shader,
//                                  so that succeeding code can have access to the global unity_InstanceID.
//                                  Also procedural function is called to setup instance data.
// - UNITY_TRANSFER_INSTANCE_ID     Copy instance ID from input struct to output struct. Used in vertex shader.

#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
void UnitySetupInstanceID(uint inputInstanceID) {
    unity_InstanceID = inputInstanceID + unity_BaseInstanceID;
}

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
#ifndef UNITY_INSTANCING_PROCEDURAL_FUNC
#error "UNITY_INSTANCING_PROCEDURAL_FUNC must be defined."
#else
void UNITY_INSTANCING_PROCEDURAL_FUNC(); // forward declaration of the procedural function
#define DEFAULT_UNITY_SETUP_INSTANCE_ID(input)      { UnitySetupInstanceID(UNITY_GET_INSTANCE_ID(input)); UNITY_INSTANCING_PROCEDURAL_FUNC();}
#endif
#else
#define DEFAULT_UNITY_SETUP_INSTANCE_ID(input)          { UnitySetupInstanceID(UNITY_GET_INSTANCE_ID(input));}
#endif
#define UNITY_TRANSFER_INSTANCE_ID(input, output)   output.instanceID = UNITY_GET_INSTANCE_ID(input)
#else
#define DEFAULT_UNITY_SETUP_INSTANCE_ID(input)
#define UNITY_TRANSFER_INSTANCE_ID(input, output)
#endif

#if !defined(UNITY_SETUP_INSTANCE_ID)
#   define UNITY_SETUP_INSTANCE_ID(input) DEFAULT_UNITY_SETUP_INSTANCE_ID(input)
#endif

////////////////////////////////////////////////////////
// instanced property arrays
#if defined(UNITY_INSTANCING_ENABLED)

#ifdef UNITY_FORCE_MAX_INSTANCE_COUNT
#define UNITY_INSTANCED_ARRAY_SIZE  UNITY_FORCE_MAX_INSTANCE_COUNT
#elif defined(UNITY_INSTANCING_SUPPORT_FLEXIBLE_ARRAY_SIZE)
#define UNITY_INSTANCED_ARRAY_SIZE  2 // minimum array size that ensures dynamic indexing
#elif defined(UNITY_MAX_INSTANCE_COUNT)
#define UNITY_INSTANCED_ARRAY_SIZE  UNITY_MAX_INSTANCE_COUNT
#else
#if defined(SHADER_API_VULKAN) && defined(SHADER_API_MOBILE)
#define UNITY_INSTANCED_ARRAY_SIZE  250
#else
#define UNITY_INSTANCED_ARRAY_SIZE  500
#endif
#endif

#define UNITY_INSTANCING_BUFFER_START(buf)      UNITY_INSTANCING_CBUFFER_SCOPE_BEGIN(UnityInstancing_##buf) struct {
#define UNITY_INSTANCING_BUFFER_END(arr)        } arr##Array[UNITY_INSTANCED_ARRAY_SIZE]; UNITY_INSTANCING_CBUFFER_SCOPE_END
#define UNITY_DEFINE_INSTANCED_PROP(type, var)  type var;
#define UNITY_ACCESS_INSTANCED_PROP(arr, var)   arr##Array[unity_InstanceID].var

// Put worldToObject array to a separate CB if UNITY_ASSUME_UNIFORM_SCALING is defined. Most of the time it will not be used.
#ifdef UNITY_ASSUME_UNIFORM_SCALING
#define UNITY_WORLDTOOBJECTARRAY_CB 1
#else
#define UNITY_WORLDTOOBJECTARRAY_CB 0
#endif

#if defined(UNITY_INSTANCED_LOD_FADE) && (defined(LOD_FADE_PERCENTAGE) || defined(LOD_FADE_CROSSFADE))
#define UNITY_USE_LODFADE_ARRAY
#endif

#if defined(UNITY_INSTANCED_RENDERING_LAYER)
#define UNITY_USE_RENDERINGLAYER_ARRAY
#endif

#ifdef UNITY_INSTANCED_LIGHTMAPSTS
#ifdef LIGHTMAP_ON
#define UNITY_USE_LIGHTMAPST_ARRAY
#endif
#ifdef DYNAMICLIGHTMAP_ON
#define UNITY_USE_DYNAMICLIGHTMAPST_ARRAY
#endif
#endif

#if defined(UNITY_INSTANCED_SH) && !defined(LIGHTMAP_ON)
#if !defined(DYNAMICLIGHTMAP_ON)
#define UNITY_USE_SHCOEFFS_ARRAYS
#endif
#if defined(SHADOWS_SHADOWMASK)
#define UNITY_USE_PROBESOCCLUSION_ARRAY
#endif
#endif

UNITY_INSTANCING_BUFFER_START(PerDraw0)
#ifndef UNITY_DONT_INSTANCE_OBJECT_MATRICES
UNITY_DEFINE_INSTANCED_PROP(float4x4, unity_ObjectToWorldArray)
#if UNITY_WORLDTOOBJECTARRAY_CB == 0
UNITY_DEFINE_INSTANCED_PROP(float4x4, unity_WorldToObjectArray)
#endif
#endif
#if defined(UNITY_USE_LODFADE_ARRAY) && defined(UNITY_INSTANCING_SUPPORT_FLEXIBLE_ARRAY_SIZE)
UNITY_DEFINE_INSTANCED_PROP(float2, unity_LODFadeArray)
#define unity_LODFade UNITY_ACCESS_INSTANCED_PROP(unity_Builtins0, unity_LODFadeArray).xyxx
#endif
#if defined(UNITY_USE_RENDERINGLAYER_ARRAY) && defined(UNITY_INSTANCING_SUPPORT_FLEXIBLE_ARRAY_SIZE)
UNITY_DEFINE_INSTANCED_PROP(float, unity_RenderingLayerArray)
#define unity_RenderingLayer UNITY_ACCESS_INSTANCED_PROP(unity_Builtins0, unity_RenderingLayerArray).xxxx
#endif
#if defined(SHADER_GRAPH_GENERATED)
DOTS_CUSTOM_ADDITIONAL_MATERIAL_VARS
#endif
UNITY_INSTANCING_BUFFER_END(unity_Builtins0)

UNITY_INSTANCING_BUFFER_START(PerDraw1)
#if !defined(UNITY_DONT_INSTANCE_OBJECT_MATRICES) && UNITY_WORLDTOOBJECTARRAY_CB == 1
UNITY_DEFINE_INSTANCED_PROP(float4x4, unity_WorldToObjectArray)
#endif
#if defined(UNITY_USE_LODFADE_ARRAY) && !defined(UNITY_INSTANCING_SUPPORT_FLEXIBLE_ARRAY_SIZE)
UNITY_DEFINE_INSTANCED_PROP(float2, unity_LODFadeArray)
#define unity_LODFade UNITY_ACCESS_INSTANCED_PROP(unity_Builtins1, unity_LODFadeArray).xyxx
#endif
#if defined(UNITY_USE_RENDERINGLAYER_ARRAY) && !defined(UNITY_INSTANCING_SUPPORT_FLEXIBLE_ARRAY_SIZE)
UNITY_DEFINE_INSTANCED_PROP(float, unity_RenderingLayerArray)
#define unity_RenderingLayer UNITY_ACCESS_INSTANCED_PROP(unity_Builtins1, unity_RenderingLayerArray).xxxx
#endif
UNITY_INSTANCING_BUFFER_END(unity_Builtins1)

UNITY_INSTANCING_BUFFER_START(PerDraw2)
#ifdef UNITY_USE_LIGHTMAPST_ARRAY
UNITY_DEFINE_INSTANCED_PROP(float4, unity_LightmapSTArray)
#define unity_LightmapST UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_LightmapSTArray)
#endif
#ifdef UNITY_USE_DYNAMICLIGHTMAPST_ARRAY
UNITY_DEFINE_INSTANCED_PROP(float4, unity_DynamicLightmapSTArray)
#define unity_DynamicLightmapST UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_DynamicLightmapSTArray)
#endif
#ifdef UNITY_USE_SHCOEFFS_ARRAYS
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHArArray)
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHAgArray)
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHAbArray)
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHBrArray)
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHBgArray)
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHBbArray)
UNITY_DEFINE_INSTANCED_PROP(half4, unity_SHCArray)
#define unity_SHAr UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHArArray)
#define unity_SHAg UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHAgArray)
#define unity_SHAb UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHAbArray)
#define unity_SHBr UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHBrArray)
#define unity_SHBg UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHBgArray)
#define unity_SHBb UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHBbArray)
#define unity_SHC  UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_SHCArray)
#endif
#ifdef UNITY_USE_PROBESOCCLUSION_ARRAY
UNITY_DEFINE_INSTANCED_PROP(half4, unity_ProbesOcclusionArray)
#define unity_ProbesOcclusion UNITY_ACCESS_INSTANCED_PROP(unity_Builtins2, unity_ProbesOcclusionArray)
#endif
UNITY_INSTANCING_BUFFER_END(unity_Builtins2)

#ifndef UNITY_DONT_INSTANCE_OBJECT_MATRICES
#undef UNITY_MATRIX_M
#undef UNITY_MATRIX_I_M
#define MERGE_UNITY_BUILTINS_INDEX(X) unity_Builtins##X
#define CALL_MERGE_UNITY_BUILTINS_INDEX(X)  MERGE_UNITY_BUILTINS_INDEX(X)
#ifdef MODIFY_MATRIX_FOR_CAMERA_RELATIVE_RENDERING
#define UNITY_MATRIX_M      ApplyCameraTranslationToMatrix(UNITY_ACCESS_INSTANCED_PROP(unity_Builtins0, unity_ObjectToWorldArray))
#define UNITY_MATRIX_I_M    ApplyCameraTranslationToInverseMatrix(UNITY_ACCESS_INSTANCED_PROP(CALL_MERGE_UNITY_BUILTINS_INDEX(UNITY_WORLDTOOBJECTARRAY_CB), unity_WorldToObjectArray))
#else
#define UNITY_MATRIX_M      UNITY_ACCESS_INSTANCED_PROP(unity_Builtins0, unity_ObjectToWorldArray)
#define UNITY_MATRIX_I_M    UNITY_ACCESS_INSTANCED_PROP(CALL_MERGE_UNITY_BUILTINS_INDEX(UNITY_WORLDTOOBJECTARRAY_CB), unity_WorldToObjectArray)
#endif
#endif

#else // UNITY_INSTANCING_ENABLED

    // in procedural mode we don't need cbuffer, and properties are not uniforms
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
#define UNITY_INSTANCING_BUFFER_START(buf)
#define UNITY_INSTANCING_BUFFER_END(arr)
#define UNITY_DEFINE_INSTANCED_PROP(type, var)      static type var;
#else
#define UNITY_INSTANCING_BUFFER_START(buf)          CBUFFER_START(buf)
#define UNITY_INSTANCING_BUFFER_END(arr)            CBUFFER_END
#define UNITY_DEFINE_INSTANCED_PROP(type, var)      type var;
#endif

#define UNITY_ACCESS_INSTANCED_PROP(arr, var)           var

#endif // UNITY_INSTANCING_ENABLED

#endif // UNITY_INSTANCING_INCLUDED
