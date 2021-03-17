using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.XRenderPipeline {

    public class MainLightShadow {
        static class ShadowPropertyIDs {
            public static int mainLightWorldToShadow;
            public static int cascadeShadowSplitSpheres0;
            public static int cascadeShadowSplitSpheres1;
            public static int cascadeShadowSplitSpheres2;
            public static int cascadeShadowSplitSpheres3;
            public static int cascadeShadowSplitSphereRadii;
            public static int mainLightShadowOffset01;
            public static int mainLightShadowOffset23;
            public static int mainLightShadowParams;
            public static int mainLightDirection;
            public static int shadowBias;
        }

        struct ShadowSliceData {
            public Matrix4x4 viewMatrix;
            public Matrix4x4 projectionMatrix;
            public Matrix4x4 shadowTransform;
            public int offsetX;
            public int offsetY;
            public int resolution;

            public void Clear() {
                viewMatrix = Matrix4x4.identity;
                projectionMatrix = Matrix4x4.identity;
                shadowTransform = Matrix4x4.identity;
                offsetX = offsetY = 0;
                resolution = 1024;
            }
        }

        const int k_ShadowmapBufferBits = 16;
        const int k_MaxCascades = 4;
        int shadowmapWidth;
        int shadowmapHeight;
        int shadowCascadesCount;
        RenderTargetHandle mainLightShadowmap;
        RenderTexture mainLightShadowmapTexture;
        readonly RenderTextureFormat shadowmapFormat;
        readonly bool forceShadowmapPointSampling;
        Matrix4x4 mainLightViewMatrix;
        Matrix4x4 mainLightProjectionMatrix;
        Matrix4x4[] mainLightShadowMatrices;
        ShadowSliceData[] cascadeSlices;
        Vector4[] cascadeSplitDistances;

        public MainLightShadow() {
            mainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            cascadeSlices = new ShadowSliceData[k_MaxCascades];
            cascadeSplitDistances = new Vector4[k_MaxCascades];
            ShadowPropertyIDs.mainLightWorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            ShadowPropertyIDs.cascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            ShadowPropertyIDs.cascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            ShadowPropertyIDs.cascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            ShadowPropertyIDs.cascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            ShadowPropertyIDs.cascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            ShadowPropertyIDs.mainLightShadowOffset01 = Shader.PropertyToID("_MainLightShadowOffset01");
            ShadowPropertyIDs.mainLightShadowOffset23 = Shader.PropertyToID("_MainLightShadowOffset23");
            ShadowPropertyIDs.mainLightShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            ShadowPropertyIDs.mainLightDirection = Shader.PropertyToID("_LightDirection");
            ShadowPropertyIDs.shadowBias = Shader.PropertyToID("_ShadowBias");
            mainLightShadowmap.Init("_MainLightShadowmap");
            shadowmapFormat = CoreUtils.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap) && (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2) ? RenderTextureFormat.Shadowmap : RenderTextureFormat.Depth;
            forceShadowmapPointSampling = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal && GraphicsSettings.HasShaderDefine(Graphics.activeTier, BuiltinShaderDefine.UNITY_METAL_SHADOWS_USE_POINT_FILTERING);
        }

        void Clear() {
            mainLightViewMatrix = Matrix4x4.identity;
            mainLightProjectionMatrix = Matrix4x4.identity;
            for (int i = 0; i < mainLightShadowMatrices.Length; ++i) {
                mainLightShadowMatrices[i] = Matrix4x4.identity;
            }
            for (int i = 0; i < cascadeSlices.Length; ++i) {
                cascadeSlices[i].Clear();
            }
            for (int i = 0; i < cascadeSplitDistances.Length; ++i) {
                cascadeSplitDistances[i] = Vector4.zero;
            }
            mainLightShadowmapTexture = null;
        }

        public bool PrepareShadowData(ref RenderingData renderingData) {
            if (!renderingData.shadowData.isMainLightShadowEnabled) {
                return false;
            }
            Clear();

            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            if (shadowLight.lightType != LightType.Directional) {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out var bounds)) {
                return false;
            }
            bool success = renderingData.cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex, 0, 1, new Vector3(1.0f, 0.0f, 0.0f), renderingData.shadowData.shadowmapResolution, light.shadowNearPlane, out mainLightViewMatrix, out mainLightProjectionMatrix, out var shadowSplitData);
            if (!success) {
                return false;
            }

            shadowCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;
            int maxTileResolution = GetMaxTileResolutionInAtlas(renderingData.shadowData.shadowmapResolution, renderingData.shadowData.shadowmapResolution, shadowCascadesCount);
            shadowmapWidth = renderingData.shadowData.shadowmapResolution;
            shadowmapHeight = (shadowCascadesCount == 2) ? renderingData.shadowData.shadowmapResolution >> 1 : renderingData.shadowData.shadowmapResolution;
            for (int i = 0; i < shadowCascadesCount; ++i) {
                bool extSuccess = ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData, shadowLightIndex, i, shadowmapWidth,
                shadowmapHeight, maxTileResolution, light.shadowNearPlane, out cascadeSplitDistances[i], out cascadeSlices[i], out cascadeSlices[i].viewMatrix, out cascadeSlices[i].projectionMatrix);
                if (!extSuccess) {
                    return false;
                }
            }
            return true;
        }

        public void DrawShadowMap(ScriptableRenderContext context, ref RenderingData renderingData) {
            ref LightData lightData = ref renderingData.lightData;
            ref ShadowData shadowData = ref renderingData.shadowData;
            ref CameraData cameraData = ref renderingData.cameraData;
            ref CullingResults cullResults = ref renderingData.cullResults;
            mainLightShadowmapTexture = RenderTexture.GetTemporary(shadowmapWidth, shadowmapHeight, k_ShadowmapBufferBits, shadowmapFormat);
            mainLightShadowmapTexture.filterMode = forceShadowmapPointSampling ? FilterMode.Point : FilterMode.Bilinear;
            mainLightShadowmapTexture.wrapMode = TextureWrapMode.Clamp;
            CommandBuffer setRTCmdbuf = CommandBufferPool.Get("Set Shadow RT");
            setRTCmdbuf.SetRenderTarget(new RenderTargetIdentifier(mainLightShadowmapTexture), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            setRTCmdbuf.ClearRenderTarget(true, true, Color.black);
            context.ExecuteCommandBuffer(setRTCmdbuf);
            CommandBufferPool.Release(setRTCmdbuf);

            int shadowLightIdx = lightData.mainLightIndex;
            if (shadowLightIdx == -1) {
                return;
            }

            CommandBuffer renderShadowCmdbuf = CommandBufferPool.Get("Render Shadowmap");
            VisibleLight shadowLight = lightData.visibleLights[shadowLightIdx];
            var settings = new ShadowDrawingSettings(cullResults, shadowLightIdx);
            for (int i = 0; i < shadowCascadesCount; ++i) {
                var splitData = settings.splitData;
                splitData.cullingSphere = cascadeSplitDistances[i];
                settings.splitData = splitData;

                Vector4 shadowBias = GetShadowBias(ref shadowLight, ref shadowData, cascadeSlices[i].projectionMatrix, cascadeSlices[i].resolution);
                Vector3 lightDirection = -shadowLight.localToWorldMatrix.GetColumn(2);
                renderShadowCmdbuf.SetGlobalVector(ShadowPropertyIDs.mainLightDirection, new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
                renderShadowCmdbuf.SetGlobalVector(ShadowPropertyIDs.shadowBias, GetShadowBias(ref shadowLight, ref shadowData, mainLightProjectionMatrix, cascadeSlices[i].resolution));
                RenderShadowSlice(renderShadowCmdbuf, ref context, ref cascadeSlices[i], ref settings, cascadeSlices[i].projectionMatrix, cascadeSlices[i].viewMatrix);
            }

            if (shadowData.mainLightShadowCascadesCount > 1) {
                renderShadowCmdbuf.EnableShaderKeyword(ShaderKeywords.MainLightShadowsCascade);
                renderShadowCmdbuf.DisableShaderKeyword(ShaderKeywords.MainLightShadows);
            } else {
                renderShadowCmdbuf.EnableShaderKeyword(ShaderKeywords.MainLightShadows);
                renderShadowCmdbuf.DisableShaderKeyword(ShaderKeywords.MainLightShadowsCascade);
            }

            if (shadowData.isSoftShadowEnabled) {
                renderShadowCmdbuf.EnableShaderKeyword(ShaderKeywords.SoftShadows);
            } else {
                renderShadowCmdbuf.DisableShaderKeyword(ShaderKeywords.SoftShadows);
            }

            SetupMainLightShadowReceiverConstants(renderShadowCmdbuf, shadowLight, shadowData.isSoftShadowEnabled);
            // reset view proj matrix
            renderShadowCmdbuf.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, cameraData.camera.projectionMatrix);

            context.ExecuteCommandBuffer(renderShadowCmdbuf);
            CommandBufferPool.Release(renderShadowCmdbuf);
        }

        public void FrameCleanup() {
            if (mainLightShadowmapTexture) {
                RenderTexture.ReleaseTemporary(mainLightShadowmapTexture);
                mainLightShadowmapTexture = null;
            }
        }

        Matrix4x4 GetShadowTransform(Matrix4x4 view, Matrix4x4 proj) {
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            if (SystemInfo.usesReversedZBuffer) {
                proj.m20 = -proj.m20;
                proj.m21 = -proj.m21;
                proj.m22 = -proj.m22;
                proj.m23 = -proj.m23;
            }

            Matrix4x4 worldToShadow = proj * view;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }

        Vector4 GetShadowBias(ref VisibleLight shadowLight, ref ShadowData shadowData, Matrix4x4 lightProjMatrix, float shadowmapResolution) {
            if (shadowLight.lightType != LightType.Directional) {
                Debug.LogWarning("Only supports directional light shadow.");
                return Vector4.zero;
            }
            float frustumSize;
            // Frustum size is guaranteed to be a cube as we wrap shadow frustum around a sphere
            frustumSize = 2.0f / lightProjMatrix.m00;
            float texelSize = frustumSize / shadowmapResolution;
            float depthBias = -shadowData.depthBias * texelSize;
            float normalBias = -shadowData.normalBias * texelSize;
            if (shadowData.isSoftShadowEnabled) {
                // TODO: depth and normal bias assume sample is no more than 1 texel away from shadowmap
                // This is not true with PCF. Ideally we need to do either
                // cone base bias (based on distance to center sample)
                // or receiver place bias based on derivatives.
                // For now we scale it by the PCF kernel size (5x5)
                const float kernelRadius = 2.5f;
                depthBias *= kernelRadius;
                normalBias *= kernelRadius;
            }
            return new Vector4(depthBias, normalBias, 0.0f, 0.0f);
        }

        int GetMaxTileResolutionInAtlas(int atlasWidth, int atlasHeight, int tileCount) {
            int resolution = Mathf.Min(atlasWidth, atlasHeight);
            int currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            while (currentTileCount < tileCount) {
                resolution = resolution >> 1;
                currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            }
            return resolution;
        }

        void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight) {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }

        bool ExtractDirectionalLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, int cascadeIndex, int shadowmapWidth, int shadowmapHeight, int shadowResolution, float shadowNearPlane,
            out Vector4 cascadeSplitDistance, out ShadowSliceData shadowSliceData, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix) {
            ShadowSplitData splitData;
            bool success = cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex,
                cascadeIndex, shadowData.mainLightShadowCascadesCount, shadowData.mainLightShadowCascadesSplit, shadowResolution, shadowNearPlane, out viewMatrix, out projMatrix,
                out splitData);

            cascadeSplitDistance = splitData.cullingSphere;
            shadowSliceData.offsetX = (cascadeIndex % 2) * shadowResolution;
            shadowSliceData.offsetY = (cascadeIndex / 2) * shadowResolution;
            shadowSliceData.resolution = shadowResolution;
            shadowSliceData.viewMatrix = viewMatrix;
            shadowSliceData.projectionMatrix = projMatrix;
            shadowSliceData.shadowTransform = GetShadowTransform(viewMatrix, projMatrix);

            // If we have shadow cascades baked into the atlas we bake cascade transform
            // in each shadow matrix to save shader ALU and L/S
            if (shadowData.mainLightShadowCascadesCount > 1) {
                ApplySliceTransform(ref shadowSliceData, shadowmapWidth, shadowmapHeight);
            }

            return success;
        }

        void RenderShadowSlice(CommandBuffer cmdbuf, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData, ref ShadowDrawingSettings settings,
            Matrix4x4 proj, Matrix4x4 view) {
            cmdbuf.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
            cmdbuf.SetViewProjectionMatrices(view, proj);
            cmdbuf.EnableScissorRect(new Rect(shadowSliceData.offsetX + 2, shadowSliceData.offsetY + 2, shadowSliceData.resolution - 4, shadowSliceData.resolution - 4));
            context.ExecuteCommandBuffer(cmdbuf);
            cmdbuf.Clear();
            context.DrawShadows(ref settings);
            cmdbuf.DisableScissorRect();
            context.ExecuteCommandBuffer(cmdbuf);
            cmdbuf.Clear();
        }

        void SetupMainLightShadowReceiverConstants(CommandBuffer cmdbuf, VisibleLight shadowLight, bool supportsSoftShadows) {
            for (int i = 0; i < shadowCascadesCount; ++i) {
                mainLightShadowMatrices[i] = cascadeSlices[i].shadowTransform;
            }

            // We setup and additional a no-op WorldToShadow matrix in the last index
            // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
            // out of bounds. (position not inside any cascade) and we want to avoid branching
            Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
            for (int i = shadowCascadesCount; i <= k_MaxCascades; ++i) {
                mainLightShadowMatrices[i] = noOpShadowMatrix;
            }

            float invShadowAtlasWidth = 1.0f / shadowmapWidth;
            float invShadowAtlasHeight = 1.0f / shadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;

            cmdbuf.SetGlobalTexture(mainLightShadowmap.id, mainLightShadowmapTexture);
            cmdbuf.SetGlobalMatrixArray(ShadowPropertyIDs.mainLightWorldToShadow, mainLightShadowMatrices);
            float softShadow = supportsSoftShadows ? 1.0f : 0.0f;
            cmdbuf.SetGlobalVector(ShadowPropertyIDs.mainLightShadowParams, new Vector4(shadowLight.light.shadowStrength, softShadow, 0.0f, 0.0f));

            if (shadowCascadesCount > 1) {
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.cascadeShadowSplitSpheres0, cascadeSplitDistances[0]);
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.cascadeShadowSplitSpheres1, cascadeSplitDistances[1]);
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.cascadeShadowSplitSpheres2, cascadeSplitDistances[2]);
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.cascadeShadowSplitSpheres3, cascadeSplitDistances[3]);
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.cascadeShadowSplitSphereRadii, new Vector4(
                    cascadeSplitDistances[0].w * cascadeSplitDistances[0].w,
                    cascadeSplitDistances[1].w * cascadeSplitDistances[1].w,
                    cascadeSplitDistances[2].w * cascadeSplitDistances[2].w,
                    cascadeSplitDistances[3].w * cascadeSplitDistances[3].w));
            }

            if (supportsSoftShadows) {
                float invShadowmapSize = 1.0f / shadowmapWidth;
                float invHalfShadowmapSize = 0.5f * invShadowmapSize;
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.mainLightShadowOffset01, new Vector4(-invHalfShadowmapSize, -invHalfShadowmapSize, invHalfShadowmapSize, -invHalfShadowmapSize));
                cmdbuf.SetGlobalVector(ShadowPropertyIDs.mainLightShadowOffset23, new Vector4(-invHalfShadowmapSize, invHalfShadowmapSize, invHalfShadowmapSize, invHalfShadowmapSize));
            }
        }
    }
}


