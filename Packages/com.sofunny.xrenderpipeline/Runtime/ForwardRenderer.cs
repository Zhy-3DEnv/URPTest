using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.XRenderPipeline {

    public struct RenderingData {
        public CullingResults cullResults;
        public CameraData cameraData;
        public LightData lightData;
        public ShadowData shadowData;
        public PerObjectData perObjectData;
        public PostProcessData postProcessData;
        public CustomRenderingData customRenderingData;
        public bool usePostProcess;
        public bool useDynamicBatching;
        public bool useClusterLighting;
    }

    public struct CustomRenderingData {
        public bool overrideCameraEnabled;
        public LayerMask overrideOpaqueLayerMask;
        public LayerMask overrideTransparentLayerMask;
        public float overrideFieldOfView;
        public Vector3 cameraPositionOffset;
        public float nearClipPlane;
        public float farClipPlane;
    }

    public struct CameraData {
        public Camera camera;
        public RenderTextureDescriptor cameraTargetDescriptor;
        public SortingCriteria opaqueSortingFlags;
        public float renderScale;
        public float maxShadowDistance;
        public bool requireOpaqueTexture;
        public bool requireDepthTexture;
        public bool isSceneViewCamera;
        public bool isHDREnabled;
        // physical camera params
        // TODO: add focalLength and focalLength/aperture for DOF
        public float exposure;
    }

    public struct LightData {
        public NativeArray<VisibleLight> visibleLights;
        public int mainLightIndex;
        public int additionalLightsCount;
        public int maxPerObjectAdditionalLightsCount;
        public float cameraExposure;
        public bool supportMixedLighting;
    }

    public struct ShadowData {
        public bool isMainLightShadowEnabled;
        public bool isSoftShadowEnabled;
        public int shadowmapResolution;
        public float depthBias;
        public float normalBias;
        public int mainLightShadowCascadesCount;
        public Vector3 mainLightShadowCascadesSplit;
    }

    public struct PostProcessData {
        public ColorGradingMode colorGradingMode;
        public int colorGradingLutSize;
    }

    public static class ShaderKeywords {
        public static readonly string LinearToSRGBConversion = "_LINEAR_TO_SRGB_CONVERSION";
        public static readonly string TonemappingACES = "_TONEMAPPING_ACES";
        public static readonly string TonemappingNeutral = "_TONEMAPPING_NEUTRAL";
        public static readonly string TonemappingUchimura = "_TONEMAPPING_UCHIMURA";
        public static readonly string HDRGrading = "_HDR_GRADING";
        public static readonly string FilmGrain = "_FILM_GRAIN";
        public static readonly string Bloom = "_BLOOM";
        public static readonly string UseRGBM = "_USE_RGBM";
        public static readonly string UseUpsampleBlur = "_USE_UPSAMPLE_BLUR";
        public static readonly string MainLightShadows = "_MAIN_LIGHT_SHADOWS";
        public static readonly string MainLightShadowsCascade = "_MAIN_LIGHT_SHADOWS_CASCADE";
        public static readonly string SoftShadows = "_SOFT_SHADOWS";
        public static readonly string AdditionalLights = "_ADDITIONAL_LIGHTS";
        public static readonly string MixedLightingSubtractive = "_MIXED_LIGHTING_SUBTRACTIVE";
        public static readonly string UseClusterLighting = "_USE_CLUSTER_LIGHTING";
        public static readonly string DebugPipeline = "_DEBUG_PIPELINE";
        public static readonly string DepthNoMsaa = "_DEPTH_NO_MSAA";
        public static readonly string DepthMsaa2 = "_DEPTH_MSAA_2";
        public static readonly string DepthMsaa4 = "_DEPTH_MSAA_4";
    }

    public struct DebugDrawCommand {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material material;
        public MaterialPropertyBlock materialPropertyBlock;
        public bool canViewInScene;
    }

    public class ForwardRenderer : IDisposable {

        static XRenderPipelineAsset pipelineSettings;
        const int k_DepthStencilBufferBits = 32;
        const string k_RenderCameraTag = "Render Camera";
        static ProfilingSampler cameraProfilingSampler = new ProfilingSampler(k_RenderCameraTag);
        const string k_SetRenderTarget = "Set RenderTarget";
        const string k_ReleaseResources = "Release Resources";
        const string k_CreateCameraRT = "Create CameraRT";
        const string k_BlitToBackbuffer = "Blit to Backbuffer";
        const string k_ResetShaderKeywords = "Reset ShaderKeywords";
        const string k_CopyColor = "Copy Color";
        const string k_CopyDepth = "Copy Depth";
        const string k_DrawObjects = "Draw Objects";

        // camera color/depth rendertexture is used when not rendering to backbuffer directly
        RenderTargetHandle cameraColorRenderTexture;
        RenderTargetHandle cameraDepthRenderTexture;
        RenderTargetHandle activeColorAttachment;
        RenderTargetHandle activeDepthAttachment;

        // copy color, depth
        RenderTargetHandle cameraOpaqueTexture;
        RenderTargetHandle cameraDepthTexture;
        Material copyColorMaterial;
        Material copyDepthMaterial;

        ShaderTagId xrpForwardShaderTag;
        ShaderTagId xrpUnlitShaderTag;
        ShaderTagId xrpDebugShaderTag;
        ShaderTagId xrpOverrideDepthNearestTag;
        ShaderTagId xrpOverrideDepthFarestTag;
        ShaderTagId xrpRestoreDepthTag;
        // render pass data
        // opaque
        FilteringSettings opaqueFilteringSettings;
        RenderStateBlock opaqueRenderStates;
        // transparent
        FilteringSettings transparentFilteringSettings;
        RenderStateBlock transparentRenderStates;

#if UNITY_EDITOR
        FilteringSettings previewCameraOpaqueFilteringSettings;
        FilteringSettings previewCameraTransparentFilteringSettings;
#endif

        RenderStateBlock overrideOpaqueRenderStates;
        RenderStateBlock overrideTransparentRenderStates_0;
        RenderStateBlock overrideTransparentRenderStates_1;

        // blit to backbuffer
        Material blitMaterial;
        ForwardLights forwardLights = new ForwardLights();
        MainLightShadow mainLightShadow = new MainLightShadow();

        ClusteredLighting clusteredLighting = new ClusteredLighting();
        bool clusteredLightDataInitialized = false;

        PostProcessPass postProcessPass;
        ColorGradingLutPass colorGradingLutPass;

        // debug draw
        public List<DebugDrawCommand> debugDrawCommandBuffer;
        const string k_DebugDrawTag = "Draw Debug Primitives";

        public ForwardRenderer(XRenderPipelineAsset pipelineAsset) {
            pipelineSettings = pipelineAsset;
            ResetRenderTarget();

            xrpForwardShaderTag = new ShaderTagId("XRPForward");
            xrpUnlitShaderTag = new ShaderTagId("XRPUnlit");
            xrpDebugShaderTag = new ShaderTagId("XRPDebug");
            xrpOverrideDepthNearestTag = new ShaderTagId("XRPOverrideDepthNearest");
            xrpOverrideDepthFarestTag = new ShaderTagId("XRPOverrideDepthFarest");
            xrpRestoreDepthTag = new ShaderTagId("XRPRestoreDepth");

            opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, pipelineSettings.opaqueLayerMask);
            // TODO: should be configurable by pipeline settings
            opaqueRenderStates = new RenderStateBlock(RenderStateMask.Nothing);

            transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, pipelineSettings.transparentLayerMask);
            // TODO: should be configurable by pipeline settings
            transparentRenderStates = new RenderStateBlock(RenderStateMask.Nothing);

#if UNITY_EDITOR
            // NOTE: When layermask is not everything(-1), preview camera cannot render anything, need to fix this issue in native engine
            previewCameraOpaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);
            previewCameraTransparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, -1);
#endif

            overrideOpaqueRenderStates = new RenderStateBlock(RenderStateMask.Stencil);
            overrideTransparentRenderStates_0 = new RenderStateBlock(RenderStateMask.Stencil);
            overrideTransparentRenderStates_1 = new RenderStateBlock(RenderStateMask.Stencil | RenderStateMask.Depth);

            var opaqueStencilState = new StencilState();
            opaqueStencilState.enabled = true;
            opaqueStencilState.writeMask = 255;
            opaqueStencilState.SetCompareFunction(CompareFunction.Always);
            opaqueStencilState.SetPassOperation(StencilOp.Replace);
            overrideOpaqueRenderStates.stencilReference = 127;
            overrideOpaqueRenderStates.stencilState = opaqueStencilState;

            var transparentStencilState_0 = new StencilState();
            transparentStencilState_0.enabled = true;
            transparentStencilState_0.readMask = 255;
            transparentStencilState_0.SetCompareFunction(CompareFunction.Equal);
            overrideTransparentRenderStates_0.stencilReference = 127;
            overrideTransparentRenderStates_0.stencilState = transparentStencilState_0;

            var transparentStencilState_1 = new StencilState();
            transparentStencilState_1.enabled = true;
            transparentStencilState_1.readMask = 255;
            transparentStencilState_1.SetCompareFunction(CompareFunction.NotEqual);
            overrideTransparentRenderStates_1.stencilReference = 127;
            overrideTransparentRenderStates_1.stencilState = transparentStencilState_1;
            overrideTransparentRenderStates_1.depthState = new DepthState(false, CompareFunction.Disabled);

            postProcessPass = new PostProcessPass(pipelineAsset);
            colorGradingLutPass = new ColorGradingLutPass(pipelineAsset.shaders.lutBuilderLdr, pipelineAsset.shaders.lutBuilderHdr);

            blitMaterial = CoreUtils.CreateEngineMaterial(pipelineAsset.shaders.blit);
            copyColorMaterial = CoreUtils.CreateEngineMaterial(pipelineAsset.shaders.copyColor);
            copyDepthMaterial = CoreUtils.CreateEngineMaterial(pipelineAsset.shaders.copyDepth);

            cameraColorRenderTexture.Init("_CameraColorRT");
            cameraDepthRenderTexture.Init("_CameraDepthRT");
            cameraOpaqueTexture.Init("_CameraOpaqueTexture");
            cameraDepthTexture.Init("_CameraDepthTexture");

            debugDrawCommandBuffer = new List<DebugDrawCommand>();
        }

        public void Dispose() {
            if (clusteredLighting != null) {
                clusteredLighting.Dispose();
                clusteredLightDataInitialized = false;
            }
            if (pipelineSettings.usePostProcess) {
                postProcessPass.Cleanup();
                colorGradingLutPass.Cleanup();
            }
            CoreUtils.Destroy(blitMaterial);
            CoreUtils.Destroy(copyColorMaterial);
            CoreUtils.Destroy(copyDepthMaterial);
        }

        void ResetRenderTarget() {
            // switch to backbuffer
            activeColorAttachment = RenderTargetHandle.s_CameraTarget;
            activeDepthAttachment = RenderTargetHandle.s_CameraTarget;
        }

        void ResetShaderKeywords(ScriptableRenderContext context) {
            var cmdbuf = CommandBufferPool.Get(k_ResetShaderKeywords);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.LinearToSRGBConversion);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.TonemappingACES);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.TonemappingNeutral);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.TonemappingUchimura);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.MainLightShadows);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.MainLightShadowsCascade);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.SoftShadows);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.AdditionalLights);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.MixedLightingSubtractive);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.UseClusterLighting);
            cmdbuf.DisableShaderKeyword(ShaderKeywords.DebugPipeline);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        void InitializeRenderingData(ref CameraData cameraData, ref CullingResults cullResults, out RenderingData renderingData) {
            renderingData.cullResults = cullResults;
            renderingData.cameraData = cameraData;
            InitializeLightData(cullResults.visibleLights, renderingData.cameraData.exposure, out renderingData.lightData);
            int mainLightIdx = renderingData.lightData.mainLightIndex;
            var visibleLights = cullResults.visibleLights;
            bool mainLightCastShadow = false;
            bool mainLightSoftShadow = false;
            if (cameraData.maxShadowDistance > 0.0f) {
                mainLightCastShadow = (mainLightIdx != -1 && visibleLights[mainLightIdx].light != null && visibleLights[mainLightIdx].light.shadows != LightShadows.None);
                mainLightSoftShadow = (mainLightIdx != -1 && visibleLights[mainLightIdx].light != null && visibleLights[mainLightIdx].light.shadows == LightShadows.Soft);
            }
            Light mainLight = null;
            if (mainLightIdx != -1) {
                mainLight = visibleLights[mainLightIdx].light;
            }
            InitializeShadowData(mainLightCastShadow, mainLightSoftShadow, mainLight, out renderingData.shadowData);
            renderingData.usePostProcess = pipelineSettings.usePostProcess;
            InitializePostProcessData(out renderingData.postProcessData);

            CustomRenderingData customRenderingData;
            customRenderingData.overrideCameraEnabled = pipelineSettings.overrideCameraSettings.enabled;
            customRenderingData.overrideOpaqueLayerMask = pipelineSettings.overrideCameraSettings.opaqueLayerMask;
            customRenderingData.overrideTransparentLayerMask = pipelineSettings.overrideCameraSettings.transparentLayerMask;
            customRenderingData.cameraPositionOffset = pipelineSettings.overrideCameraSettings.positionOffset;
            customRenderingData.overrideFieldOfView = pipelineSettings.overrideCameraSettings.fieldOfView;
            customRenderingData.nearClipPlane = pipelineSettings.overrideCameraSettings.nearClipPlane;
            customRenderingData.farClipPlane = pipelineSettings.overrideCameraSettings.farClipPlane;
            renderingData.customRenderingData = customRenderingData;

            renderingData.useDynamicBatching = pipelineSettings.useDynamicBatching;
            PerObjectData perObjConfig = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightData | PerObjectData.OcclusionProbe;
            if (renderingData.lightData.additionalLightsCount > 0) {
                // NOTE: without structure buffer support, we need 'unity_LightIndices' for addtional lights
                perObjConfig |= PerObjectData.LightIndices;
            }
            renderingData.perObjectData = perObjConfig;
            renderingData.useClusterLighting = pipelineSettings.useClusterLighting;
        }

        void InitializeCameraData(Camera camera, out CameraData cameraData) {
            cameraData.camera = camera;
            cameraData.renderScale = pipelineSettings.renderScale;
            if (pipelineSettings.supportMainLightShadow) {
                cameraData.maxShadowDistance = Mathf.Min(pipelineSettings.shadowDistance, camera.farClipPlane);
            } else {
                cameraData.maxShadowDistance = 0.0f;
            }
            cameraData.isSceneViewCamera = camera.cameraType == CameraType.SceneView;
            int msaaSample = 1;
            if (camera.allowMSAA && (int)pipelineSettings.msaaQuality > 1) {
                msaaSample = (camera.targetTexture != null) ? camera.targetTexture.antiAliasing : (int)pipelineSettings.msaaQuality;
            }
            cameraData.isHDREnabled = (camera.allowHDR || cameraData.isSceneViewCamera) && pipelineSettings.useHDR;
            cameraData.cameraTargetDescriptor = CoreUtils.CreateRenderTextureDescriptor(camera, cameraData.renderScale, cameraData.isHDREnabled, msaaSample);
            cameraData.requireOpaqueTexture = pipelineSettings.requireOpaqueTexture;
            cameraData.requireDepthTexture = pipelineSettings.requireDepthTexture || cameraData.isSceneViewCamera;
            var commonOpaqueFlags = SortingCriteria.CommonOpaque;
            var noFrontToBackOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;
            bool hasHSRGPU = SystemInfo.hasHiddenSurfaceRemovalOnGPU;
            bool canSkipFrontToBackSorting = (camera.opaqueSortMode == OpaqueSortMode.Default && hasHSRGPU) || camera.opaqueSortMode == OpaqueSortMode.NoDistanceSort;
            cameraData.opaqueSortingFlags = canSkipFrontToBackSorting ? noFrontToBackOpaqueFlags : commonOpaqueFlags;
            if (camera.cameraType == CameraType.Game) {
                // TODO: avoid getcomponent
                camera.gameObject.TryGetComponent<PhysicalCameraData>(out var physicalCameraData);
                if (physicalCameraData != null) {
                    cameraData.exposure = physicalCameraData.GetExposure();
                } else {
                    cameraData.exposure = 1f;
                }
            } else {
                cameraData.exposure = 1f;
            }
        }

        void InitializeLightData(NativeArray<VisibleLight> visibleLights, float cameraExposure, out LightData lightData) {
            // find main light, we can only have one directional light as main light.
            int mainLightIndex = -1;
            int lightCount = visibleLights.Length;
            for (int i = 0; i < lightCount; ++i) {
                var curVisibleLight = visibleLights[i];
                if (curVisibleLight.light == RenderSettings.sun) {
                    mainLightIndex = i;
                    break;
                }
                // if sun not set, find the first directional light
                if (RenderSettings.sun == null && curVisibleLight.lightType == LightType.Directional && curVisibleLight.light != null) {
                    mainLightIndex = i;
                    break;
                }
            }
            lightData.visibleLights = visibleLights;
            lightData.mainLightIndex = mainLightIndex;
            if (pipelineSettings.disableAdditionalLights) {
                lightData.maxPerObjectAdditionalLightsCount = 0;
                lightData.additionalLightsCount = 0;
            } else {
                // no support to bitfield mask and int[] in gles2. Can't index fast more than 4 lights.
                lightData.maxPerObjectAdditionalLightsCount = 4;
                // we use less limits for mobile as some mobile GPUs have small SP cache for constants
                // using more than 32 might cause spilling to main memory
                lightData.additionalLightsCount = Mathf.Min((mainLightIndex != -1) ? visibleLights.Length - 1 : visibleLights.Length, 32);
            }
            lightData.cameraExposure = cameraExposure;
            lightData.supportMixedLighting = pipelineSettings.supportMixedLighting;
        }

        void InitializeShadowData(bool mainLightCastShadow, bool mainLightSoftShadow, Light mainLight, out ShadowData shadowData) {
            shadowData.isMainLightShadowEnabled = SystemInfo.supportsShadows && pipelineSettings.supportMainLightShadow && mainLightCastShadow;
            shadowData.isSoftShadowEnabled = pipelineSettings.supportSoftShadow && mainLightSoftShadow;
            shadowData.shadowmapResolution = (int)pipelineSettings.shadowmapResolution;
            XRPAdditionalLightData additionalData = null;
            if (mainLight) {
                mainLight.gameObject.TryGetComponent(out additionalData);
                if (additionalData && !additionalData.UsePipelineSettings) {
                    shadowData.depthBias = mainLight.shadowBias;
                    shadowData.normalBias = mainLight.shadowNormalBias;
                } else {
                    shadowData.depthBias = pipelineSettings.shadowDepthBias;
                    shadowData.normalBias = pipelineSettings.shadowNormalBias;
                }
            } else {
                shadowData.depthBias = pipelineSettings.shadowDepthBias;
                shadowData.normalBias = pipelineSettings.shadowNormalBias;
            }

            switch (pipelineSettings.shadowCascadesOption) {
                case ShadowCascadesOption.NoCascades:
                    shadowData.mainLightShadowCascadesCount = 1;
                    shadowData.mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    break;
                case ShadowCascadesOption.TwoCascades:
                    shadowData.mainLightShadowCascadesCount = 2;
                    shadowData.mainLightShadowCascadesSplit = new Vector3(pipelineSettings.cascades2Split, 1.0f, 0.0f);
                    break;
                case ShadowCascadesOption.FourCascades:
                    shadowData.mainLightShadowCascadesCount = 4;
                    shadowData.mainLightShadowCascadesSplit = pipelineSettings.cascades4Split;
                    break;
                default:
                    UnityEngine.Debug.LogError("Unknown ShadowCascadesOption");
                    shadowData.mainLightShadowCascadesCount = 1;
                    shadowData.mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    break;

            }
        }

        void InitializePostProcessData(out PostProcessData postProcessData) {
            postProcessData.colorGradingMode = pipelineSettings.colorGradingMode;
            postProcessData.colorGradingLutSize = pipelineSettings.colorGradingLutSize;
        }

        bool RequiresIntermediateColorRenderTexture(ref RenderingData renderingData) {
            ref CameraData cameraData = ref renderingData.cameraData;
            int msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve;
            bool isScaledRender = !Mathf.Approximately(cameraData.renderScale, 1f);
            return requiresExplicitMsaaResolve || isScaledRender || cameraData.isSceneViewCamera
            || cameraData.isHDREnabled || Display.main.requiresBlitToBackbuffer || renderingData.usePostProcess;
        }

        public void RenderCamera(ScriptableRenderContext context, Camera camera) {
            if (!camera.TryGetCullingParameters(false, out var cullingParams)) {
                return;
            }

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView) {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
#endif
            CommandBuffer cmdbuf = CommandBufferPool.Get(k_RenderCameraTag);
            using (new ProfilingScope(cmdbuf, cameraProfilingSampler)) {
                // NOTE: need to set shader variables by XRP when "SetupCameraProperties" is removed from native engine.
                // Configure shader variables and other unity properties that are required for rendering.
                // * Setup Camera RenderTarget and Viewport
                // * Setup camera view, projection and their inverse matrices.
                // * Setup properties: _WorldSpaceCameraPos, _ProjectionParams, _ScreenParams, _ZBufferParams, unity_OrthoParams
                // * Setup camera world clip planes properties
                // * Setup HDR keyword
                // * Setup global time properties (_Time, _SinTime, _CosTime)
                context.SetupCameraProperties(camera);
                ResetShaderKeywords(context);

                InitializeCameraData(camera, out var cameraData);
                // setup shadow culling params
                bool isShadowCastingDisabled = !pipelineSettings.supportMainLightShadow;
                bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
                if (isShadowCastingDisabled || isShadowDistanceZero) {
                    cullingParams.cullingOptions &= ~CullingOptions.ShadowCasters;
                }
                cullingParams.shadowDistance = cameraData.maxShadowDistance;
                var cullResults = context.Cull(ref cullingParams);
                InitializeRenderingData(ref cameraData, ref cullResults, out var renderingData);
                Execute(context, ref renderingData);
            }
            context.Submit();
        }

        void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            // TODO: reset per camera shader keywords here

            // setup lights
            forwardLights.SetupLights(context, ref renderingData);

            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            // clustered lighting
            if (renderingData.useClusterLighting) {
                if (camera.cameraType == CameraType.Game || cameraData.isSceneViewCamera) {
#if UNITY_EDITOR
                    // orthographic camera clustered lighting is not supported
                    if (renderingData.cameraData.camera.orthographic) {
                        var cmdbuf = CommandBufferPool.Get("Turn off clustered lighting");
                        cmdbuf.DisableShaderKeyword(ShaderKeywords.UseClusterLighting);
                        context.ExecuteCommandBuffer(cmdbuf);
                        CommandBufferPool.Release(cmdbuf);
                    }
#endif
                    if (!clusteredLightDataInitialized) {
                        clusteredLightDataInitialized = true;
                        clusteredLighting.InitializeClusteredLightingData(pipelineSettings.shaders.cluster, pipelineSettings.shaders.lightCulling, renderingData.cameraData.camera);
                    }
                    clusteredLighting.CheckClustersDirty(ref renderingData);
                    clusteredLighting.ExecuteClusteringPass(context, renderingData.cameraData.camera);
                    clusteredLighting.CollectVisiblePunctualLights(ref renderingData.lightData.visibleLights, ref renderingData);
                    clusteredLighting.ExecuteLightCullingPass(context, renderingData.cameraData.camera);
                }
            } else {
                clusteredLighting?.CleanupClusteredLightingData();
                clusteredLightDataInitialized = false;
            }

            if (mainLightShadow.PrepareShadowData(ref renderingData)) {
                mainLightShadow.DrawShadowMap(context, ref renderingData);
            }

            if (renderingData.usePostProcess) {
                colorGradingLutPass.RenderColorGradingLut(context, renderingData.postProcessData.colorGradingMode, renderingData.postProcessData.colorGradingLutSize);
            }
            bool createColorRT = RequiresIntermediateColorRenderTexture(ref renderingData);
            bool createDepthRT = cameraData.requireDepthTexture;
            activeColorAttachment = createColorRT ? cameraColorRenderTexture : RenderTargetHandle.s_CameraTarget;
            activeDepthAttachment = createDepthRT ? cameraDepthRenderTexture : RenderTargetHandle.s_CameraTarget;
            var cameraRTDescriptor = cameraData.cameraTargetDescriptor;
            var createRTCmdbuf = CommandBufferPool.Get(k_CreateCameraRT);
            if (createColorRT) {
                var colorDescriptor = cameraRTDescriptor;
                colorDescriptor.depthBufferBits = createDepthRT ? 0 : k_DepthStencilBufferBits;
                createRTCmdbuf.GetTemporaryRT(activeColorAttachment.id, colorDescriptor, FilterMode.Bilinear);
            }
            if (createDepthRT) {
                var depthDescriptor = cameraRTDescriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                depthDescriptor.bindMS = depthDescriptor.msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
                createRTCmdbuf.GetTemporaryRT(activeDepthAttachment.id, depthDescriptor, FilterMode.Point);
            }
            context.ExecuteCommandBuffer(createRTCmdbuf);
            CommandBufferPool.Release(createRTCmdbuf);

            // if rendering to intermediate render texture, we don't have to create msaa backbuffer
            int backbufferMsaaSamples = (createColorRT || createDepthRT) ? 1 : cameraRTDescriptor.msaaSamples;
            if (Camera.main == camera && camera.cameraType == CameraType.Game) {
                QualitySettings.antiAliasing = backbufferMsaaSamples;
            }

            ConfigureRenderTarget(context, activeColorAttachment, RenderBufferLoadAction.DontCare, camera.backgroundColor, true, true);

            if (renderingData.useClusterLighting) {
                clusteredLighting.SetClusterLightingParams(context, ref renderingData);
            }

#if UNITY_EDITOR
            SetupPipelineDebug(context);
#endif

            DrawOpaque(context, ref renderingData);
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null) {
                DrawSkybox(context, ref renderingData);
            }
            if (cameraData.requireOpaqueTexture) {
                GenerateCameraOpaqueTexture(context, ref renderingData, pipelineSettings.opaqueDownsampling);
            }
            if (cameraData.requireDepthTexture) {
                GenerateCameraDepthTexture(context, ref renderingData);
            }
            // NOTE: When opaque/depth texture is required, we must start a new native renderpass with load action for preserving previous contents
            // because rendertarget is changed. This is bad for performance.
            // TODO: When only the current pixel's value is required, we should avoid this copy pass by using native subpass dependency
            if (cameraData.requireOpaqueTexture || cameraData.requireDepthTexture) {
                ConfigureRenderTarget(context, activeColorAttachment, RenderBufferLoadAction.Load, camera.backgroundColor, false, false);
            }
            DrawTransparent(context, ref renderingData);
            DrawDebugPrimitives(context, ref renderingData);
            DrawGizmos(context, camera, GizmoSubset.PreImageEffects);
            if (renderingData.usePostProcess && CoreUtils.IsPostProcessEnabled(camera)) {
                postProcessPass.Setup(renderingData.cameraData.cameraTargetDescriptor, activeColorAttachment, RenderTargetHandle.s_CameraTarget, colorGradingLutPass.internalLutRT);
                postProcessPass.RenderPostProcess(context, ref renderingData);
            } else if (activeColorAttachment != RenderTargetHandle.s_CameraTarget) {
                // If post-process pass is turned off and we need to resolve to camera target, execute final blit pass
                BlitToBackbuffer(context, ref renderingData);
            }
            DrawGizmos(context, camera, GizmoSubset.PostImageEffects);

            // clean up
            mainLightShadow.FrameCleanup();
            if (renderingData.usePostProcess) {
                colorGradingLutPass.FrameCleanup(context);
            }
            FinishRendering(context, ref renderingData);
        }

        void FinishRendering(ScriptableRenderContext context, ref RenderingData renderingData) {
           CommandBuffer cmdbuf = CommandBufferPool.Get(k_ReleaseResources);
           if (activeColorAttachment != RenderTargetHandle.s_CameraTarget) {
               cmdbuf.ReleaseTemporaryRT(activeColorAttachment.id);
               activeColorAttachment = RenderTargetHandle.s_CameraTarget;
           }
           if (activeDepthAttachment != RenderTargetHandle.s_CameraTarget) {
               cmdbuf.ReleaseTemporaryRT(activeDepthAttachment.id);
               activeDepthAttachment = RenderTargetHandle.s_CameraTarget;
           }
           if (renderingData.cameraData.requireOpaqueTexture) {
               cmdbuf.ReleaseTemporaryRT(cameraOpaqueTexture.id);
           }
           if (renderingData.cameraData.requireDepthTexture) {
               cmdbuf.ReleaseTemporaryRT(cameraDepthTexture.id);
           }
           context.ExecuteCommandBuffer(cmdbuf);
           CommandBufferPool.Release(cmdbuf);
        }

        // TODO: Manage render pass by frame graph

        void DrawOpaque(ScriptableRenderContext context, ref RenderingData renderingData) {
            SortingCriteria sortFlags = renderingData.cameraData.opaqueSortingFlags;
            Camera camera = renderingData.cameraData.camera;
            SortingSettings sortingSettings = new SortingSettings(camera) {
                criteria = sortFlags,
            };
            DrawingSettings drawSettings = new DrawingSettings(xrpForwardShaderTag, sortingSettings) {
                perObjectData = renderingData.perObjectData,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                enableDynamicBatching = renderingData.useDynamicBatching,
                // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
            };
            // NOTE: 0 slot is occupied by xrpForwardShaderTag, so we should start from index 1
            drawSettings.SetShaderPassName(1, xrpUnlitShaderTag);

            ref var customRenderingData = ref renderingData.customRenderingData;
            CommandBuffer cmdbuf = CommandBufferPool.Get(k_DrawObjects);
            bool shouldDrawOverridePass = customRenderingData.overrideCameraEnabled && customRenderingData.overrideOpaqueLayerMask != 0 && !renderingData.cameraData.isSceneViewCamera;
            if (shouldDrawOverridePass) {
                Matrix4x4 currentViewMat = camera.worldToCameraMatrix;
                Matrix4x4 currentProjMat = camera.projectionMatrix;
                Matrix4x4 overrideProjMat = Matrix4x4.Perspective(customRenderingData.overrideFieldOfView, camera.aspect, customRenderingData.nearClipPlane, customRenderingData.farClipPlane);
                Matrix4x4 overrideViewMat = currentViewMat;
                Vector4 cameraPosition = currentViewMat.GetColumn(3);
                Vector4 cameraPositionOffset = new Vector4(customRenderingData.cameraPositionOffset.x, customRenderingData.cameraPositionOffset.y, customRenderingData.cameraPositionOffset.z, 0);
                overrideViewMat.SetColumn(3, cameraPosition + cameraPositionOffset);
                cmdbuf.SetViewProjectionMatrices(overrideViewMat, overrideProjMat);
                Vector4 prevFogParams = CoreUtils.CalculateFogParamsFromRenderSettings();
                cmdbuf.SetGlobalVector("unity_FogParams", new Vector4(0,0,0,1));
                context.ExecuteCommandBuffer(cmdbuf);

                // draw override opaque
                FilteringSettings overrideOpaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, customRenderingData.overrideOpaqueLayerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref overrideOpaqueFilteringSettings, ref overrideOpaqueRenderStates);

                // draw override depth (set depth to nearest value for override opaque)
                FilteringSettings overrideDepthFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, customRenderingData.overrideOpaqueLayerMask);
                DrawingSettings overrideDepthNearestDrawSettings = new DrawingSettings(xrpOverrideDepthNearestTag, sortingSettings) {
                    enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
                };
                context.DrawRenderers(renderingData.cullResults, ref overrideDepthNearestDrawSettings, ref overrideDepthFilteringSettings);

                // restore view-proj matrix, fog settings
                cmdbuf.Clear();
                cmdbuf.SetViewProjectionMatrices(currentViewMat, currentProjMat);
                cmdbuf.SetGlobalVector("unity_FogParams", prevFogParams);
                context.ExecuteCommandBuffer(cmdbuf);
                CommandBufferPool.Release(cmdbuf);
                // TODO: should turn off override layermask automatically?
                // opaqueFilteringSettings.layerMask &= ~customRenderingData.overrideOpaqueLayerMask;

                // draw normal opaque
#if UNITY_EDITOR
                if (camera.cameraType == CameraType.Preview) {
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref previewCameraOpaqueFilteringSettings, ref opaqueRenderStates);
                } else {
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref opaqueFilteringSettings, ref opaqueRenderStates);
                }
#else
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref opaqueFilteringSettings, ref opaqueRenderStates);
#endif

            } else {
#if UNITY_EDITOR
                DrawRenderersAndDebugWireframe(context, ref renderingData, ref sortingSettings, ref opaqueFilteringSettings, ref opaqueRenderStates, ref drawSettings);
#else
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref opaqueFilteringSettings, ref opaqueRenderStates);
#endif
            }

            // Render objects that did not match any shader pass with error shader
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.Preview) {
                CoreUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, previewCameraOpaqueFilteringSettings, SortingCriteria.None);
            } else {
                CoreUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, opaqueFilteringSettings, SortingCriteria.None);
            }
#else
            CoreUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, opaqueFilteringSettings, SortingCriteria.None);
#endif
        }

        void DrawSkybox(ScriptableRenderContext context, ref RenderingData renderingData) {
            context.DrawSkybox(renderingData.cameraData.camera);
        }

        void DrawTransparent(ScriptableRenderContext context, ref RenderingData renderingData) {
            SortingCriteria sortFlags = SortingCriteria.CommonTransparent;
            Camera camera = renderingData.cameraData.camera;
            SortingSettings sortingSettings = new SortingSettings(camera) {
                criteria = sortFlags,
            };
            DrawingSettings drawSettings = new DrawingSettings(xrpForwardShaderTag, sortingSettings) {
                perObjectData = renderingData.perObjectData,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                enableDynamicBatching = renderingData.useDynamicBatching,
                // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
            };
            // NOTE: 0 slot is occupied by xrpForwardShaderTag, so we should start from index 1
            drawSettings.SetShaderPassName(1, xrpUnlitShaderTag);


            // TODO: if drawing wireframe here, skybox will also be rendered as wireframe, fix this issue later
// #if UNITY_EDITOR
//             DrawRenderersAndDebugWireframe(context, ref renderingData, ref sortingSettings, ref transparentFilteringSettings, ref transparentRenderStates, ref drawSettings);
// #else
//             context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref transparentFilteringSettings, ref transparentRenderStates);
// #endif
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.Preview) {
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref previewCameraTransparentFilteringSettings, ref transparentRenderStates);
            } else {
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref transparentFilteringSettings, ref transparentRenderStates);
            }
#else
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref transparentFilteringSettings, ref transparentRenderStates);
#endif

            ref var customRenderingData = ref renderingData.customRenderingData;
            CommandBuffer cmdbuf = CommandBufferPool.Get(k_DrawObjects);
            if (customRenderingData.overrideCameraEnabled && customRenderingData.overrideTransparentLayerMask != 0) {
                Matrix4x4 projMat = Matrix4x4.Perspective(customRenderingData.overrideFieldOfView, camera.aspect, customRenderingData.nearClipPlane, customRenderingData.farClipPlane);
                Matrix4x4 viewMat = camera.worldToCameraMatrix;
                Matrix4x4 currentViewMat = viewMat;
                Matrix4x4 currentProjMat = camera.projectionMatrix;
                Vector4 cameraPosition = viewMat.GetColumn(3);
                Vector4 cameraPositionOffset = new Vector4(customRenderingData.cameraPositionOffset.x, customRenderingData.cameraPositionOffset.y, customRenderingData.cameraPositionOffset.z, 0);
                viewMat.SetColumn(3, cameraPosition + cameraPositionOffset);
                cmdbuf.SetViewProjectionMatrices(viewMat, projMat);
                Vector4 prevFogParams = CoreUtils.CalculateFogParamsFromRenderSettings();
                cmdbuf.SetGlobalVector("unity_FogParams", new Vector4(0,0,0,1));
                context.ExecuteCommandBuffer(cmdbuf);
                // restore depth
                SortingSettings restoreDepthSortingSettings = new SortingSettings(camera) {
                    criteria = SortingCriteria.None,
                };
                FilteringSettings restoreDepthFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, customRenderingData.overrideOpaqueLayerMask);
                DrawingSettings overrideDepthFarestDrawSettings = new DrawingSettings(xrpOverrideDepthFarestTag, restoreDepthSortingSettings) {
                    enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
                };
                context.DrawRenderers(renderingData.cullResults, ref overrideDepthFarestDrawSettings, ref restoreDepthFilteringSettings);
                DrawingSettings restoreDepthDrawSettings = new DrawingSettings(xrpRestoreDepthTag, restoreDepthSortingSettings) {
                    enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
                };
                context.DrawRenderers(renderingData.cullResults, ref restoreDepthDrawSettings, ref restoreDepthFilteringSettings);
                // draw override transparent
                var overrideTransparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, customRenderingData.overrideTransparentLayerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref overrideTransparentFilteringSettings, ref overrideTransparentRenderStates_0);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref overrideTransparentFilteringSettings, ref overrideTransparentRenderStates_1);
                cmdbuf.Clear();
                cmdbuf.SetViewProjectionMatrices(currentViewMat, currentProjMat);
                cmdbuf.SetGlobalVector("unity_FogParams", prevFogParams);
                context.ExecuteCommandBuffer(cmdbuf);
                CommandBufferPool.Release(cmdbuf);
                // TODO: should turn off override layermask automatically?
                // transparentFilteringSettings.layerMask &= ~customRenderingData.overrideTransparentLayerMask;
            }

            // Render objects that did not match any shader pass with error shader
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.Preview) {
                CoreUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, previewCameraTransparentFilteringSettings, SortingCriteria.None);
            } else {
                CoreUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, transparentFilteringSettings, SortingCriteria.None);
            }
#else
            CoreUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, transparentFilteringSettings, SortingCriteria.None);
#endif
        }

        void BlitToBackbuffer(ScriptableRenderContext context, ref RenderingData renderingData) {
            bool requiresSRGBConversion = Display.main.requiresSrgbBlitToBackbuffer;
            ref var cameraData = ref renderingData.cameraData;
            var cmdbuf = CommandBufferPool.Get(k_BlitToBackbuffer);
            if (requiresSRGBConversion) {
                cmdbuf.EnableShaderKeyword(ShaderKeywords.LinearToSRGBConversion);
            } else {
                cmdbuf.DisableShaderKeyword(ShaderKeywords.LinearToSRGBConversion);
            }
            cmdbuf.SetGlobalTexture("_BlitTex", activeColorAttachment.Identifier());
            if (cameraData.isSceneViewCamera) {
                cmdbuf.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, // color
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
                cmdbuf.Blit(activeColorAttachment.Identifier(), BuiltinRenderTextureType.CameraTarget, blitMaterial);
            } else {
                cmdbuf.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Camera camera = cameraData.camera;
                cmdbuf.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmdbuf.SetViewport(camera.pixelRect);
                cmdbuf.DrawMesh(CoreUtils.FullscreenMesh, Matrix4x4.identity, blitMaterial);
            }
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        [Conditional("UNITY_EDITOR")]
        void DrawGizmos(ScriptableRenderContext context, Camera camera, GizmoSubset gizmoSubset) {
#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos())
                context.DrawGizmos(camera, gizmoSubset);
#endif
        }

        void DrawDebugPrimitives(ScriptableRenderContext context, ref RenderingData renderingData) {
            var cameraData = renderingData.cameraData;
            var cmdbuf = CommandBufferPool.Get(k_DebugDrawTag);
            foreach (var cmd in debugDrawCommandBuffer) {
                if (cmd.canViewInScene || !cameraData.isSceneViewCamera) {
                    cmdbuf.DrawMesh(cmd.mesh, cmd.matrix, cmd.material, 0, 0, cmd.materialPropertyBlock);
                }
            }
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        void DrawRenderersAndDebugWireframe(ScriptableRenderContext context, ref RenderingData renderingData, ref SortingSettings sortingSettings, ref FilteringSettings filteringSettings, ref RenderStateBlock renderStates, ref DrawingSettings drawSettings) {
            var camera = renderingData.cameraData.camera;
            DrawingSettings wireframeSettings = new DrawingSettings(xrpForwardShaderTag, sortingSettings) {
                perObjectData = renderingData.perObjectData,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                enableDynamicBatching = renderingData.useDynamicBatching,
                // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
                overrideMaterial = pipelineSettings.materials.wireframe,
            };
            wireframeSettings.overrideMaterial = pipelineSettings.materials.wireframe;
            RenderStateBlock wireframeRSBlock = new RenderStateBlock();
            wireframeRSBlock.rasterState = new RasterState(CullMode.Back, -1, -1, true);
            wireframeRSBlock.mask = RenderStateMask.Raster;
            var wireframeMode = pipelineSettings.wireframeMode;
            bool drawWireframe = wireframeMode == WireframeMode.WireframeOnly || wireframeMode == WireframeMode.ShadedWireframe || wireframeMode == WireframeMode.SolidColorWireframe;
            if (pipelineSettings.enablePipelineDebug && drawWireframe) {
                var cmdbuf = CommandBufferPool.Get("Wireframe");
                if (wireframeMode == WireframeMode.ShadedWireframe) {
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStates);
                    context.Submit();
                } else if (wireframeMode == WireframeMode.SolidColorWireframe) {
                    drawSettings.overrideMaterial = pipelineSettings.materials.wireframe;
                    drawSettings.overrideMaterialPassIndex = 1;
                    cmdbuf.SetGlobalColor("_DebugOverrideColor", pipelineSettings.wireframeOverrideColor);
                    context.ExecuteCommandBuffer(cmdbuf);
                    cmdbuf.Clear();
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStates);
                    context.Submit();
                }

                cmdbuf.SetGlobalColor("_WireframeColor", pipelineSettings.wireframeColor);
                context.ExecuteCommandBuffer(cmdbuf);
                CommandBufferPool.Release(cmdbuf);
                GL.wireframe = true;
                context.DrawRenderers(renderingData.cullResults, ref wireframeSettings, ref filteringSettings, ref wireframeRSBlock);
                context.Submit();
                GL.wireframe = false;
            } else {
#if UNITY_EDITOR
                if (renderingData.cameraData.camera.cameraType == CameraType.Preview) {
                    if (filteringSettings.renderQueueRange == RenderQueueRange.opaque) {
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref previewCameraOpaqueFilteringSettings, ref renderStates);
                    } else {
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref previewCameraTransparentFilteringSettings, ref renderStates);
                    }
                } else {
                    // For sceneview camera, don't draw objects in custom fov
                    if (renderingData.cameraData.isSceneViewCamera && renderingData.customRenderingData.overrideCameraEnabled) {
                        if (filteringSettings.renderQueueRange == RenderQueueRange.opaque) {
                            var sceneViewCameraFilteringSettings = filteringSettings;
                            sceneViewCameraFilteringSettings.layerMask |= renderingData.customRenderingData.overrideOpaqueLayerMask;
                            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref sceneViewCameraFilteringSettings, ref renderStates);
                        } else {
                            var sceneViewCameraFilteringSettings = filteringSettings;
                            sceneViewCameraFilteringSettings.layerMask |= renderingData.customRenderingData.overrideTransparentLayerMask;
                            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref sceneViewCameraFilteringSettings, ref renderStates);
                        }
                    } else {
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStates);
                    }
                }
#else
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStates);
#endif
            }
        }

        void SetupPipelineDebug(ScriptableRenderContext context) {
            if (!pipelineSettings.enablePipelineDebug) {
                return;
            }

            var cmdbuf = CommandBufferPool.Get("Setup Debug Mode");
            cmdbuf.EnableShaderKeyword(ShaderKeywords.DebugPipeline);
            cmdbuf.SetGlobalInt(Shader.PropertyToID("_PipelineMaterialDebugMode"), (int)pipelineSettings.materialDebugMode);
            Color pureMetalColor = pipelineSettings.validateMaterialPureMetalColor;
            cmdbuf.SetGlobalColor("_DebugValidatePureMetalColor", new Color(pipelineSettings.validateMaterialPureMetal ? 1 : 0, pureMetalColor.r, pureMetalColor.g, pureMetalColor.b));
            cmdbuf.SetGlobalColor("_DebugValidateHighColor", pipelineSettings.validateMaterialHighColor);
            cmdbuf.SetGlobalColor("_DebugValidateLowColor", pipelineSettings.validateMaterialLowColor);

            int lightingDebugMode = (int)LightingDebugMode.None;
            if (pipelineSettings.showDirectSpecular) {
                lightingDebugMode |= (int)LightingDebugMode.DirectSpecular;
            }
            if (pipelineSettings.showIndirectSpecular) {
                lightingDebugMode |= (int)LightingDebugMode.IndirectSpecular;
            }
            if (pipelineSettings.showDirectDiffuse) {
                lightingDebugMode |= (int)LightingDebugMode.DirectDiffuse;
            }
            if (pipelineSettings.showIndirectDiffuse) {
                lightingDebugMode |= (int)LightingDebugMode.IndirectDiffuse;
            }

            cmdbuf.SetGlobalInt(Shader.PropertyToID("_PipelineLightingDebugMode"), lightingDebugMode);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        void GenerateCameraOpaqueTexture(ScriptableRenderContext context, ref RenderingData renderingData, Downsampling downsamplingMethod) {
            var cmdbuf = CommandBufferPool.Get(k_CopyColor);
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            if (downsamplingMethod == Downsampling._2xBilinear) {
                desc.width /= 2;
                desc.height /= 2;
            } else if (downsamplingMethod == Downsampling._4xBox || downsamplingMethod == Downsampling._4xBilinear) {
                desc.width /= 4;
                desc.height /= 4;
            }
            cmdbuf.GetTemporaryRT(cameraOpaqueTexture.id, desc, downsamplingMethod == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
            if (downsamplingMethod == Downsampling._4xBox) {
                cmdbuf.Blit(activeColorAttachment.Identifier(), cameraOpaqueTexture.Identifier(), copyColorMaterial);
            } else {
                // for other downsampling method, use default blit shader
                cmdbuf.Blit(activeColorAttachment.Identifier(), cameraOpaqueTexture.Identifier());
            }
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        void GenerateCameraDepthTexture(ScriptableRenderContext context, ref RenderingData renderingData) {
            var cmdbuf = CommandBufferPool.Get(k_CopyDepth);
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            int cameraMsaaSamples = desc.msaaSamples;
            desc.colorFormat = RenderTextureFormat.Depth;
            desc.depthBufferBits = 32;
            desc.msaaSamples = 1;
            cmdbuf.GetTemporaryRT(cameraDepthTexture.id, desc, FilterMode.Point);
            cmdbuf.SetGlobalTexture(cameraDepthRenderTexture.id, cameraDepthRenderTexture.Identifier());
            if (cameraMsaaSamples > 1) {
                cmdbuf.DisableShaderKeyword(ShaderKeywords.DepthNoMsaa);
                if (cameraMsaaSamples == 4) {
                    cmdbuf.DisableShaderKeyword(ShaderKeywords.DepthMsaa2);
                    cmdbuf.EnableShaderKeyword(ShaderKeywords.DepthMsaa4);
                } else {
                    cmdbuf.EnableShaderKeyword(ShaderKeywords.DepthMsaa2);
                    cmdbuf.DisableShaderKeyword(ShaderKeywords.DepthMsaa4);
                }
            } else {
                cmdbuf.EnableShaderKeyword(ShaderKeywords.DepthNoMsaa);
                cmdbuf.DisableShaderKeyword(ShaderKeywords.DepthMsaa2);
                cmdbuf.DisableShaderKeyword(ShaderKeywords.DepthMsaa4);
            }
            cmdbuf.Blit(activeDepthAttachment.Identifier(), cameraDepthTexture.Identifier(), copyDepthMaterial);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        void ConfigureRenderTarget(ScriptableRenderContext context, RenderTargetHandle destRT, RenderBufferLoadAction colorLoadAction, Color backgroundColor, bool clearColor, bool clearDepth) {
            CommandBuffer setRTCmdbuf = CommandBufferPool.Get(k_SetRenderTarget);
            // NOTE: it feels confusing that color rt contains depth buffer. better to refact 'render texture' class in native engine.
            // when msaa is enabled, actually we need a separate multisampled depth texture, but unity native engine handle this for us. so we don't need to configure depth texture again
            if (activeDepthAttachment.Identifier() == BuiltinRenderTextureType.CameraTarget) {
                setRTCmdbuf.SetRenderTarget(destRT.Identifier(), colorLoadAction, RenderBufferStoreAction.Store);
            } else {
                setRTCmdbuf.SetRenderTarget(destRT.Identifier(), colorLoadAction, RenderBufferStoreAction.Store,
                    activeDepthAttachment.Identifier(), RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            }
            setRTCmdbuf.ClearRenderTarget(clearDepth, clearColor, ColorUtils.ConvertSRGBToActiveColorSpace(backgroundColor));
            context.ExecuteCommandBuffer(setRTCmdbuf);
            CommandBufferPool.Release(setRTCmdbuf);
        }
    }
}

