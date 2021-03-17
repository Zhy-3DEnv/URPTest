using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.XRenderPipeline {

    public enum MixedLightingSetup {
        None,
        Subtractive,
    };

    public class ForwardLights {
        const int k_MaxVisibleAdditionalLights = 32;
        const string k_SetupLightConstants = "Setup Light Constants";
        // Holds light direction for directional lights or position for punctual lights.
        // When w is set to 1.0, it means it's a punctual light.
        Vector4 k_DefaultLightPosition = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
        Vector4 k_DefaultLightColorIntensity = Color.black;

        // Default light attenuation is setup in a particular way that it causes
        // directional lights to return 1.0 for both distance and angle attenuation
        Vector4 k_DefaultLightAttenuation = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        Vector4 k_DefaultLightSpotDirection = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
        Vector4 k_DefaultLightsProbeChannel = new Vector4(-1.0f, 1.0f, -1.0f, -1.0f);

        Vector4[] additionalLightPositions;
        Vector4[] additionalLightColorIntensities;
        Vector4[] additionalLightAttenuations;
        Vector4[] additionalLightSpotDirections;
        Vector4[] additionalLightOcclusionProbes;

        MixedLightingSetup mixedLightingSetup;

        static class LightPropertyIDs {
            public static int mainLightPosition;
            public static int mainLightColorIntensity;
            // TODO: additional lights
            public static int additionalLightsCount;
            public static int additionalLightsPositions;
            public static int additionalLightsColorIntensities;
            public static int additionalLightsAttenuations;
            public static int additionalLightsSpotDirs;
            public static int additionalLightOcclusionProbes;
        }

        public ForwardLights() {
            LightPropertyIDs.mainLightPosition = Shader.PropertyToID("_MainLightPosition");
            LightPropertyIDs.mainLightColorIntensity = Shader.PropertyToID("_MainLightColorIntensity");
            LightPropertyIDs.additionalLightsCount = Shader.PropertyToID("_AdditionalLightsCount");
            LightPropertyIDs.additionalLightsPositions = Shader.PropertyToID("_AdditionalLightsPositions");
            LightPropertyIDs.additionalLightsColorIntensities = Shader.PropertyToID("_AdditionalLightsColorIntensities");
            LightPropertyIDs.additionalLightsAttenuations = Shader.PropertyToID("_AdditionalLightsAttenuations");
            LightPropertyIDs.additionalLightsSpotDirs = Shader.PropertyToID("_AdditionalLightsSpotDirs");
            LightPropertyIDs.additionalLightOcclusionProbes = Shader.PropertyToID("_AdditionalLightsOcclusionProbes");

            additionalLightPositions = new Vector4[k_MaxVisibleAdditionalLights];
            additionalLightColorIntensities = new Vector4[k_MaxVisibleAdditionalLights];
            additionalLightAttenuations = new Vector4[k_MaxVisibleAdditionalLights];
            additionalLightSpotDirections = new Vector4[k_MaxVisibleAdditionalLights];
            additionalLightOcclusionProbes = new Vector4[k_MaxVisibleAdditionalLights];
        }

        public void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData) {
            mixedLightingSetup = MixedLightingSetup.None;

            CommandBuffer cmdbuf = CommandBufferPool.Get(k_SetupLightConstants);

            ref LightData lightData = ref renderingData.lightData;
            var lights = lightData.visibleLights;
            int mainLightIndex = lightData.mainLightIndex;
            if (mainLightIndex != -1) {
                InitializeLightConstants(lights, mainLightIndex, lightData.cameraExposure,
                    out var mainLightPos,
                    out var mainLightColorIntensity,
                    out var mainLightAttenuation,
                    out var mainLightDir,
                    out var mainLightOcclusionProbe);
                cmdbuf.SetGlobalVector(LightPropertyIDs.mainLightPosition, mainLightPos);
                cmdbuf.SetGlobalVector(LightPropertyIDs.mainLightColorIntensity, mainLightColorIntensity);
            }

            var cullResults = renderingData.cullResults;
            int additionalLightsCount = SetupPerObjectLightIndices(cullResults, ref lightData);
            if (additionalLightsCount > 0) {
                cmdbuf.EnableShaderKeyword(ShaderKeywords.AdditionalLights);
                for (int i = 0, lightIter = 0; i < lights.Length && lightIter < k_MaxVisibleAdditionalLights; ++i) {
                    VisibleLight light = lights[i];
                    if (lightData.mainLightIndex != i) {
                        InitializeLightConstants(lights, i, lightData.cameraExposure, out additionalLightPositions[lightIter],
                            out additionalLightColorIntensities[lightIter],
                            out additionalLightAttenuations[lightIter],
                            out additionalLightSpotDirections[lightIter],
                            out additionalLightOcclusionProbes[lightIter]);
                        lightIter++;
                    }
                }

                cmdbuf.SetGlobalVectorArray(LightPropertyIDs.additionalLightsPositions, additionalLightPositions);
                cmdbuf.SetGlobalVectorArray(LightPropertyIDs.additionalLightsColorIntensities, additionalLightColorIntensities);
                cmdbuf.SetGlobalVectorArray(LightPropertyIDs.additionalLightsAttenuations, additionalLightAttenuations);
                cmdbuf.SetGlobalVectorArray(LightPropertyIDs.additionalLightsSpotDirs, additionalLightSpotDirections);
                cmdbuf.SetGlobalVectorArray(LightPropertyIDs.additionalLightOcclusionProbes, additionalLightOcclusionProbes);
                cmdbuf.SetGlobalVector(LightPropertyIDs.additionalLightsCount, new Vector4(lightData.maxPerObjectAdditionalLightsCount, 0.0f, 0.0f, 0.0f));
            } else {
                cmdbuf.DisableShaderKeyword(ShaderKeywords.AdditionalLights);
                cmdbuf.SetGlobalVector(LightPropertyIDs.additionalLightsCount, Vector4.zero);
            }

            if (lightData.supportMixedLighting && mixedLightingSetup == MixedLightingSetup.Subtractive) {
                cmdbuf.EnableShaderKeyword(ShaderKeywords.MixedLightingSubtractive);
            } else {
                cmdbuf.DisableShaderKeyword(ShaderKeywords.MixedLightingSubtractive);
            }
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        int SetupPerObjectLightIndices(CullingResults cullResults, ref LightData lightData) {
            if (lightData.additionalLightsCount == 0) {
                return 0;
            }

            var visibleLights = lightData.visibleLights;
            var perObjectLightIndexMap = cullResults.GetLightIndexMap(Allocator.Temp);
            int globalDirectionalLightsCount = 0;
            int additionalLightsCount = 0;

            // Disable all directional lights from the perobject light indices
            // Pipeline handles main light globally and there's no support for additional directional lights atm.
            for (int i = 0; i < visibleLights.Length; ++i) {
                if (additionalLightsCount >= k_MaxVisibleAdditionalLights) {
                    break;
                }

                VisibleLight light = visibleLights[i];
                if (i == lightData.mainLightIndex) {
                    perObjectLightIndexMap[i] = -1;
                    ++globalDirectionalLightsCount;
                } else {
                    perObjectLightIndexMap[i] -= globalDirectionalLightsCount;
                    ++additionalLightsCount;
                }
            }

            // Disable all remaining lights we cannot fit into the global light buffer.
            for (int i = globalDirectionalLightsCount + additionalLightsCount; i < perObjectLightIndexMap.Length; ++i) {
                perObjectLightIndexMap[i] = -1;
            }

            cullResults.SetLightIndexMap(perObjectLightIndexMap);

            perObjectLightIndexMap.Dispose();
            return additionalLightsCount;
        }

        void InitializeLightConstants(NativeArray<VisibleLight> lights, int lightIndex, float cameraExposure, out Vector4 lightPos, out Vector4 lightColorIntensity, out Vector4 lightAttenuation, out Vector4 lightSpotDir, out Vector4 lightOcclusionProbeChannel) {
            lightPos = k_DefaultLightPosition;
            lightColorIntensity = k_DefaultLightColorIntensity;
            lightAttenuation = k_DefaultLightAttenuation;
            lightSpotDir = k_DefaultLightSpotDirection;
            lightOcclusionProbeChannel = k_DefaultLightsProbeChannel;

            // When no lights are visible, main light will be set to -1.
            // In this case we initialize it to default values and return
            if (lightIndex < 0)
                return;

            VisibleLight lightData = lights[lightIndex];
            if (lightData.lightType == LightType.Directional) {
                Vector4 dir = -lightData.localToWorldMatrix.GetColumn(2);
                lightPos = new Vector4(dir.x, dir.y, dir.z, 0.0f);
            } else {
                Vector4 pos = lightData.localToWorldMatrix.GetColumn(3);
                lightPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
            }

            lightColorIntensity = lightData.light.color;
            lightColorIntensity.w = lightData.light.intensity * cameraExposure; // compute pre-exposed intensity

            // Directional Light attenuation is initialize so distance attenuation always be 1.0
            if (lightData.lightType != LightType.Directional) {
                // Light attenuation in universal matches the unity vanilla one.
                // attenuation = 1.0 / distanceToLightSqr
                // We offer two different smoothing factors.
                // The smoothing factors make sure that the light intensity is zero at the light range limit.
                // The first smoothing factor is a linear fade starting at 80 % of the light range.
                // smoothFactor = (lightRangeSqr - distanceToLightSqr) / (lightRangeSqr - fadeStartDistanceSqr)
                // We rewrite smoothFactor to be able to pre compute the constant terms below and apply the smooth factor
                // with one MAD instruction
                // smoothFactor =  distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
                //                 distanceSqr *           oneOverFadeRangeSqr             +              lightRangeSqrOverFadeRangeSqr

                // The other smoothing factor matches the one used in the Unity lightmapper but is slower than the linear one.
                // smoothFactor = (1.0 - saturate((distanceSqr * 1.0 / lightrangeSqr)^2))^2
                float lightRangeSqr = lightData.range * lightData.range;
                float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
                float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
                float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
                float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
                float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightData.range * lightData.range);
                // NOTE: URP use different fading mode for mobile and pc platform, in XRP, we adopt the same fading mode for consistence
                // On mobile: Use the faster linear smoothing factor.
                // On other devices: Use the smoothing factor that matches the GI.
                // lightAttenuation.x = Application.isMobilePlatform ? oneOverFadeRangeSqr : oneOverLightRangeSqr;
                lightAttenuation.x = oneOverFadeRangeSqr;
                lightAttenuation.y = lightRangeSqrOverFadeRangeSqr;
            }

            if (lightData.lightType == LightType.Spot) {
                Vector4 dir = lightData.localToWorldMatrix.GetColumn(2);
                // NOTE: spotDir is reverted for calculating spot light attenuation, so if you want to get correct spot direction from shader side, must revert it again, feels confusing...
                lightSpotDir = new Vector4(-dir.x, -dir.y, -dir.z, 0.0f);

                // Spot Attenuation with a linear falloff can be defined as
                // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
                // This can be rewritten as
                // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
                // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
                // If we precompute the terms in a MAD instruction
                float cosOuterAngle = Mathf.Cos(Mathf.Deg2Rad * lightData.spotAngle * 0.5f);
                // We need to do a null check for particle lights
                // This should be changed in the future
                // Particle lights will use an inline function
                float cosInnerAngle;
                if (lightData.light != null) {
                    cosInnerAngle = Mathf.Cos(lightData.light.innerSpotAngle * Mathf.Deg2Rad * 0.5f);
                } else {
                    cosInnerAngle = Mathf.Cos((2.0f * Mathf.Atan(Mathf.Tan(lightData.spotAngle * 0.5f * Mathf.Deg2Rad) * (64.0f - 18.0f) / 64.0f)) * 0.5f);
                }
                float smoothAngleRange = Mathf.Max(0.001f, cosInnerAngle - cosOuterAngle);
                float invAngleRange = 1.0f / smoothAngleRange;
                float add = -cosOuterAngle * invAngleRange;
                lightAttenuation.z = invAngleRange;
                lightAttenuation.w = add;
            }

            Light light = lightData.light;

            // Set the occlusion probe channel.
            int occlusionProbeChannel = light != null ? light.bakingOutput.occlusionMaskChannel : -1;

            // If we have baked the light, the occlusion channel is the index we need to sample in 'unity_ProbesOcclusion'
            // If we have not baked the light, the occlusion channel is -1.
            // In case there is no occlusion channel is -1, we set it to zero, and then set the second value in the
            // input to one. We then, in the shader max with the second value for non-occluded lights.
            lightOcclusionProbeChannel.x = occlusionProbeChannel == -1 ? 0f : occlusionProbeChannel;
            lightOcclusionProbeChannel.y = occlusionProbeChannel == -1 ? 1f : 0f;

            if (light != null && light.bakingOutput.mixedLightingMode == MixedLightingMode.Subtractive && light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed) {
                if (mixedLightingSetup == MixedLightingSetup.None && lightData.light.shadows != LightShadows.None) {
                    mixedLightingSetup = MixedLightingSetup.Subtractive;
                }
            }
        }

    }

}

