using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework.XRenderPipeline {

    public class ClusteredLighting : IDisposable {

// suppress warning 0649: "field is never assigned to", we assign fields in compute shader
#pragma warning disable 0649
        struct ClusterAABB {
            public float4 minPt;
            public float4 maxPt;
        };

        // NOTE: mobile gpu cache line size (64byte or 128byte?), we need to pack struct carefully
        struct PointLight {
            public float4 positionAndRange; // xyz: world space position, w: range
            public float4 colorIntensity;
        }

        struct SpotLight {
            public float4 positionAndRange;
            public float4 colorIntensity;
            public float4 spotDirAndAngle;
            public float4 attenuation;
        }

        struct LightGrid {
            public uint offset;
            public uint count;
        }
#pragma warning restore 0649

        static class ShaderPropertyIDs {
            public static readonly int clusters = Shader.PropertyToID("_Clusters");
            public static readonly int clusterDimensions = Shader.PropertyToID("_ClusterDimensions");
            public static readonly int tileSize = Shader.PropertyToID("_TileSize");
            public static readonly int punctualLightCount = Shader.PropertyToID("_PunctualLightCount");
            public static readonly int pointLights = Shader.PropertyToID("_PointLights");
            public static readonly int cPointLights = Shader.PropertyToID("CPointLights");
            public static readonly int spotLights = Shader.PropertyToID("_SpotLights");
            public static readonly int cSpotLights = Shader.PropertyToID("CSpotLights");
            public static readonly int lightIndexList = Shader.PropertyToID("_LightIndexList");
            public static readonly int lightGrids = Shader.PropertyToID("_LightGrids");
            public static readonly int cLightGrids = Shader.PropertyToID("CLightGrids");
            public static readonly int globalLightIndexCount = Shader.PropertyToID("_GlobalLightIndexCount");
            public static readonly int cameraInvProjMatrix = Shader.PropertyToID("_CameraInvProjMatrix");
            public static readonly int cameraViewMatrix = Shader.PropertyToID("_CameraViewMatrix");

            public static readonly int clusterDimXYAndTileSize = Shader.PropertyToID("_ClusterDimXYAndTileSize");
            public static readonly int sliceScale = Shader.PropertyToID("_SliceScale");
            public static readonly int sliceBias = Shader.PropertyToID("_SliceBias");
        }

        bool isClustersDirty;
        // keep in sync with USE_CBUFFER_FOR_CLUSTERED_SHADING in ClusterCommon.hlsl
        bool useConstantBufferForClusteredShading;

        #region ClusterSettings
        int tilePixelWidth;
        int tilePixelHeight;
        int tileXCount;
        int tileYCount;
        int zSliceCount;
        int totalClusterCount;
        // keep in sync with MAX_VISIBLE_LIGHTS_CLUSTER in LightCulling.compute
        int maxVisibleLightsPerCluster;
        float maxFarPlane;
        #endregion

        const string k_SetComputeBufferParam = "Set Compute Buffer Param";
        const string k_SetClusterLightingParams = "Set ClusterLightingParams";

        #region ClusteringPassData
        const string k_ClusteringTag = "Generate Clusters";
        ComputeShader clusteringShader;
        int clusteringKernelIdx;
        // public NativeArray<ClusterAABB> clusterAABBArray;
        ComputeBuffer clusterAABBBuffer;
        #endregion

        #region LightCullingPassData
        const string k_LightCullingTag = "Light Culling";
        ComputeShader lightCullingShader;
        int lightCullingKernelIdx;
        int visiblePointLightCount;
        int visibleSpotLightCount;
        int maxVisiblePointLightCount;
        int maxVisibleSpotLightCount;
        NativeArray<PointLight> pointLightArray;
        NativeArray<SpotLight> spotLightArray;
        ComputeBuffer pointLightBuffer;
        ComputeBuffer spotLightBuffer;
        // NativeArray<LightGrid> lightGridArray;
        ComputeBuffer lightGridBuffer;
        // NativeArray<uint> lightIndexArray;
        ComputeBuffer lightIndexBuffer;
        // TODO: check if we can use a single uint instead of StructuredBuffer<uint> for this
        // NativeArray<uint> globalLightIndexCountArray;
        ComputeBuffer globalLightIndexCountBuffer;
        #endregion



        int cachedCameraPixelWidth;
        int cachedCameraPixelHeight;
        Matrix4x4 cachedCameraProjectionMatrix;

        public ClusteredLighting() {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) {
                useConstantBufferForClusteredShading = false;
            } else {
                useConstantBufferForClusteredShading = true;
            }
            tileXCount = 16;
            tileYCount = 8;
            zSliceCount = 16;
            totalClusterCount = tileXCount * tileYCount * zSliceCount;
            maxVisibleLightsPerCluster = 16;
            maxFarPlane = 500f;
            visiblePointLightCount = 0;
            visibleSpotLightCount = 0;
            maxVisiblePointLightCount = 256;
            maxVisibleSpotLightCount = 256;
        }

        public void InitializeClusteredLightingData(ComputeShader clusteringShader, ComputeShader lightCullingShader, Camera camera) {
            // Debug.Log("InitializeClusteredLightingData");
            isClustersDirty = true;

            tilePixelWidth = (camera.scaledPixelWidth + tileXCount - 1) / tileXCount;
            tilePixelHeight = (camera.scaledPixelHeight + tileYCount - 1) / tileYCount;

            this.clusteringShader = clusteringShader;
            clusteringKernelIdx = clusteringShader.FindKernel("CSMain");
            this.lightCullingShader = lightCullingShader;
            lightCullingKernelIdx = lightCullingShader.FindKernel("CSMain");

            if (clusterAABBBuffer == null) {
                clusterAABBBuffer = new ComputeBuffer(totalClusterCount, Marshal.SizeOf(typeof(ClusterAABB)));
            }
            if (!pointLightArray.IsCreated) {
                pointLightArray = new NativeArray<PointLight>(maxVisiblePointLightCount, Allocator.Persistent);
            }
            if (!spotLightArray.IsCreated) {
                spotLightArray = new NativeArray<SpotLight>(maxVisibleSpotLightCount, Allocator.Persistent);
            }
            if (pointLightBuffer == null) {
                pointLightBuffer = new ComputeBuffer(maxVisiblePointLightCount, Marshal.SizeOf(typeof(PointLight)));
            }
            if (spotLightBuffer == null) {
                spotLightBuffer = new ComputeBuffer(maxVisibleSpotLightCount, Marshal.SizeOf(typeof(SpotLight)));
            }
            if (lightGridBuffer == null) {
                lightGridBuffer = new ComputeBuffer(totalClusterCount, Marshal.SizeOf(typeof(LightGrid)));
            }
            if (lightIndexBuffer == null) {
                lightIndexBuffer = new ComputeBuffer(maxVisibleLightsPerCluster * totalClusterCount, sizeof(uint));
            }
            if (globalLightIndexCountBuffer == null) {
                globalLightIndexCountBuffer = new ComputeBuffer(1, sizeof(uint));
            }

            cachedCameraPixelWidth = camera.scaledPixelWidth;
            cachedCameraPixelHeight = camera.scaledPixelHeight;
            cachedCameraProjectionMatrix = camera.projectionMatrix;

            // debug data
            // clusterAABBArray = new NativeArray<ClusterAABB>(totalClusterCount, Allocator.Persistent);
            // lightGridArray = new NativeArray<LightGrid>(totalClusterCount, Allocator.Persistent);
            // lightIndexArray = new NativeArray<uint>(maxVisibleLightsPerCluster * totalClusterCount, Allocator.Persistent);
            // globalLightIndexCountArray = new NativeArray<uint>(1, Allocator.Persistent);
        }

        public void CleanupClusteredLightingData() {
            // Debug.Log("CleanupClusteredLightingData");
            isClustersDirty = true;
            clusterAABBBuffer?.Dispose();
            clusterAABBBuffer = null;
            pointLightBuffer?.Dispose();
            pointLightBuffer = null;
            if (pointLightArray.IsCreated) {
                pointLightArray.Dispose();
            }
            spotLightBuffer?.Dispose();
            spotLightBuffer = null;
            if (spotLightArray.IsCreated) {
                spotLightArray.Dispose();
            }
            lightGridBuffer?.Dispose();
            lightGridBuffer = null;
            lightIndexBuffer?.Dispose();
            lightIndexBuffer = null;
            globalLightIndexCountBuffer?.Dispose();
            globalLightIndexCountBuffer = null;
        }

        public void SetClustersDirty() {
            isClustersDirty = true;
        }

        public void CheckClustersDirty(ref RenderingData renderingData) {
            Camera currentCamera = renderingData.cameraData.camera;
            if (currentCamera.scaledPixelWidth != cachedCameraPixelWidth
                || currentCamera.scaledPixelHeight != cachedCameraPixelHeight
                || currentCamera.projectionMatrix != cachedCameraProjectionMatrix) {

                isClustersDirty = true;
                cachedCameraPixelWidth = currentCamera.scaledPixelWidth;
                cachedCameraPixelHeight = currentCamera.scaledPixelHeight;
                cachedCameraProjectionMatrix = currentCamera.projectionMatrix;
                tilePixelWidth = (currentCamera.scaledPixelWidth + tileXCount - 1) / tileXCount;
                tilePixelHeight = (currentCamera.scaledPixelHeight + tileYCount - 1) / tileYCount;
            }
        }

        public void CollectVisiblePunctualLights(ref NativeArray<VisibleLight> visibleLights, ref RenderingData renderingData) {
            visiblePointLightCount = 0;
            visibleSpotLightCount = 0;
            Camera camera = renderingData.cameraData.camera;
            float3 cameraForward = camera.transform.forward;
            foreach (VisibleLight light in visibleLights) {
                if (light.lightType == LightType.Point && visiblePointLightCount < maxVisiblePointLightCount) {
                    float4 lightPosWS = light.localToWorldMatrix.GetColumn(3);
                    if (math.dot(lightPosWS.xyz, cameraForward) > maxFarPlane) {
                        // Debug.Log("Light Culled: " + light.light.gameObject.name);
                        continue;
                    }
                    PointLight pointLight;
                    pointLight.positionAndRange = new float4(lightPosWS.xyz, light.range);
                    float3 lightColor = new float3(light.light.color.r, light.light.color.g, light.light.color.b);
                    pointLight.colorIntensity = new float4(lightColor, light.light.intensity);
                    pointLightArray[visiblePointLightCount] = pointLight;
                    visiblePointLightCount++;
                }

                if (light.lightType == LightType.Spot && visibleSpotLightCount < maxVisibleSpotLightCount) {
                    float4 lightPosWS = light.localToWorldMatrix.GetColumn(3);
                    if (math.dot(lightPosWS.xyz, cameraForward) > maxFarPlane) {
                        // Debug.Log("Light Culled: " + light.light.gameObject.name);
                        continue;
                    }
                    SpotLight spotLight;
                    spotLight.positionAndRange = new float4(lightPosWS.xyz, light.range);
                    float3 lightColor = new float3(light.light.color.r, light.light.color.g, light.light.color.b);
                    spotLight.colorIntensity = new float4(lightColor, light.light.intensity);
                    // NOTE: reuse the same logic from ForwardLights, refine this.
                    float4 dir = light.localToWorldMatrix.GetColumn(2);
                    // NOTE: spotDir is reverted for calculating spot light attenuation, so if you want to get correct spot direction from shader side, must revert it again, feels confusing...
                    float4 spotDirAngle = new float4(-dir.xyz, math.radians(light.spotAngle) * 0.5f);
                    spotLight.spotDirAndAngle = spotDirAngle;
                    float cosOuterAngle = math.cos(math.radians(light.spotAngle) * 0.5f);
                    float cosInnerAngle;
                    if (light.light != null) {
                        cosInnerAngle = math.cos(math.radians(light.light.innerSpotAngle) * 0.5f);
                    } else {
                        cosInnerAngle = math.cos((2.0f * math.atan(math.tan(math.radians(light.spotAngle) * 0.5f) * (64.0f - 18.0f) / 64.0f)) * 0.5f);
                    }
                    float smoothAngleRange = math.max(0.001f, cosInnerAngle - cosOuterAngle);
                    float invAngleRange = 1.0f / smoothAngleRange;
                    float add = -cosOuterAngle * invAngleRange;
                    float4 lightAttenuation;
                    float lightRangeSqr = light.range * light.range;
                    float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
                    float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
                    float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
                    float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
                    float oneOverLightRangeSqr = 1.0f / math.max(0.0001f, light.range * light.range);
                    // NOTE: URP use different fading mode for mobile and pc platform, in XRP, we adopt the same fading mode for consistence
                    // lightAttenuation.x = Application.isMobilePlatform ? oneOverFadeRangeSqr : oneOverLightRangeSqr;
                    lightAttenuation.x = oneOverFadeRangeSqr;
                    lightAttenuation.y = lightRangeSqrOverFadeRangeSqr;
                    lightAttenuation.z = invAngleRange;
                    lightAttenuation.w = add;
                    spotLight.attenuation = lightAttenuation;
                    spotLightArray[visibleSpotLightCount] = spotLight;
                    visibleSpotLightCount++;
                }
            }
        }

        public void Dispose() {
            CleanupClusteredLightingData();
        }

        public void ExecuteClusteringPass(ScriptableRenderContext context, Camera camera) {
            if (!isClustersDirty) {
                return;
            }
            // Debug.Log("ExecuteClusteringPass");
            isClustersDirty = false;
            CommandBuffer cmdbuf = CommandBufferPool.Get(k_ClusteringTag);
            cmdbuf.SetComputeBufferParam(clusteringShader, clusteringKernelIdx, ShaderPropertyIDs.clusters, clusterAABBBuffer);
            cmdbuf.SetComputeIntParams(clusteringShader, ShaderPropertyIDs.clusterDimensions, new int[] {tileXCount, tileYCount, zSliceCount, 0});
            cmdbuf.SetComputeIntParams(clusteringShader, ShaderPropertyIDs.tileSize, new int[] { tilePixelWidth, tilePixelHeight, 0, 0});
            var projectionMatrix = camera.projectionMatrix;
            if (SystemInfo.usesReversedZBuffer) {
                projectionMatrix[2, 0] = -projectionMatrix[2, 0];
                projectionMatrix[2, 1] = -projectionMatrix[2, 1];
                projectionMatrix[2, 2] = -projectionMatrix[2, 2];
                projectionMatrix[2, 3] = -projectionMatrix[2, 3];
            }
            cmdbuf.SetComputeMatrixParam(clusteringShader, ShaderPropertyIDs.cameraInvProjMatrix, projectionMatrix.inverse);
            cmdbuf.DispatchCompute(clusteringShader, clusteringKernelIdx, tileXCount, tileYCount, zSliceCount);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);

            // debug code
            // context.Submit();
            // clusterAABBBuffer.GetData(clusterAABBArray);
        }

        public void ExecuteLightCullingPass(ScriptableRenderContext context, Camera camera) {
            CommandBuffer cmdbuf = CommandBufferPool.Get(k_LightCullingTag);
            // NOTE: cannot use SetGlobalBuffer for compute shader
            cmdbuf.SetComputeBufferParam(lightCullingShader, lightCullingKernelIdx, ShaderPropertyIDs.clusters, clusterAABBBuffer);
            cmdbuf.SetComputeBufferParam(lightCullingShader, lightCullingKernelIdx, ShaderPropertyIDs.pointLights, pointLightBuffer);
            cmdbuf.SetComputeIntParams(lightCullingShader, ShaderPropertyIDs.punctualLightCount, new int[] { visiblePointLightCount, visibleSpotLightCount, 0, 0 });
            // NOTE: must pass complete array here to avoid light flickering issues, this is a restriction which is not reasonable for binding compute buffer in unity.
            // TODO: find out why this confusing restriction exist
            cmdbuf.SetComputeBufferData(pointLightBuffer, pointLightArray, 0, 0, pointLightArray.Length);
            cmdbuf.SetComputeBufferParam(lightCullingShader, lightCullingKernelIdx, ShaderPropertyIDs.spotLights, spotLightBuffer);
            cmdbuf.SetComputeBufferData(spotLightBuffer, spotLightArray, 0, 0, spotLightArray.Length);
            cmdbuf.SetComputeBufferParam(lightCullingShader, lightCullingKernelIdx, ShaderPropertyIDs.lightIndexList, lightIndexBuffer);
            cmdbuf.SetComputeBufferParam(lightCullingShader, lightCullingKernelIdx, ShaderPropertyIDs.lightGrids, lightGridBuffer);
            cmdbuf.SetComputeBufferParam(lightCullingShader, lightCullingKernelIdx, ShaderPropertyIDs.globalLightIndexCount, globalLightIndexCountBuffer);
            cmdbuf.SetComputeBufferData(globalLightIndexCountBuffer, new uint[] { 0 });
            cmdbuf.SetComputeMatrixParam(lightCullingShader, ShaderPropertyIDs.cameraViewMatrix, camera.worldToCameraMatrix);
            cmdbuf.DispatchCompute(lightCullingShader, lightCullingKernelIdx, 1, 1, 16);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);

            // debug code
            // context.Submit();
            // lightGridBuffer.GetData(lightGridArray);
            // lightIndexBuffer.GetData(lightIndexArray);
            // globalLightIndexCountBuffer.GetData(globalLightIndexCountArray);
        }

        public void SetClusterLightingParams(ScriptableRenderContext context, ref RenderingData renderingData) {
            CommandBuffer cmdbuf = CommandBufferPool.Get(k_SetClusterLightingParams);
            cmdbuf.EnableShaderKeyword(ShaderKeywords.UseClusterLighting);
            // cmdbuf.SetComputeBufferData(pointLightBuffer, pointLightArray);
            if (useConstantBufferForClusteredShading) {
                cmdbuf.SetGlobalConstantBuffer(pointLightBuffer, ShaderPropertyIDs.cPointLights, 0, visiblePointLightCount * Marshal.SizeOf(typeof(PointLight)));
                cmdbuf.SetGlobalConstantBuffer(spotLightBuffer, ShaderPropertyIDs.cSpotLights, 0, visibleSpotLightCount * Marshal.SizeOf(typeof(SpotLight)));
            } else {
                cmdbuf.SetGlobalBuffer(ShaderPropertyIDs.pointLights, pointLightBuffer);
                cmdbuf.SetGlobalBuffer(ShaderPropertyIDs.spotLights, spotLightBuffer);
            }
            // cmdbuf.SetComputeBufferData(lightIndexBuffer, lightIndexArray);
            // NOTE: LightIndexList cannot use constant buffer because "maxVisibleLightsPerCluster * totalClusterCount * sizeof(uint)" exceeds maximum allowed size 64kb on d3d11
            // if (useConstantBufferForClusteredShading) {
            //     cmdbuf.SetGlobalConstantBuffer(lightIndexBuffer, "CLightIndexList", 0, maxVisibleLightsPerCluster * totalClusterCount * sizeof(uint));
            // } else {
            //     cmdbuf.SetGlobalBuffer(ShaderPropertyIDs.lightIndexList, lightIndexBuffer);
            // }
            cmdbuf.SetGlobalBuffer(ShaderPropertyIDs.lightIndexList, lightIndexBuffer);
            // cmdbuf.SetComputeBufferData(lightGridBuffer, lightGridArray);
            if (useConstantBufferForClusteredShading) {
                cmdbuf.SetGlobalConstantBuffer(lightGridBuffer, ShaderPropertyIDs.cLightGrids, 0, totalClusterCount * Marshal.SizeOf(typeof(LightGrid)));
            } else {
                cmdbuf.SetGlobalBuffer(ShaderPropertyIDs.lightGrids, lightGridBuffer);
            }

            float zFar = math.max(renderingData.cameraData.camera.farClipPlane, maxFarPlane);
            float zNear = renderingData.cameraData.camera.nearClipPlane;

            cmdbuf.SetGlobalVector(ShaderPropertyIDs.clusterDimXYAndTileSize, new Vector4(tileXCount, tileYCount, tilePixelWidth, tilePixelHeight));
            cmdbuf.SetGlobalFloat(ShaderPropertyIDs.sliceScale, (float)zSliceCount / math.log2(zFar / zNear));
            cmdbuf.SetGlobalFloat(ShaderPropertyIDs.sliceBias, -(float)zSliceCount * math.log2(zNear) / math.log2(zFar / zNear));
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }
    }
}
