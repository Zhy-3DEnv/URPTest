using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;
using UnityEditorInternal;
using UnityEditor;

namespace Framework.XRenderPipeline {
    [CustomEditor(typeof(XRenderPipelineAsset))]
    public class XRenderPipelineAssetEditor : Editor {
        internal class Styles {
            // Groups
            public static GUIContent generalSettingsText = EditorGUIUtility.TrTextContent("General");
            public static GUIContent qualitySettingsText = EditorGUIUtility.TrTextContent("Quality");
            public static GUIContent shadowSettingsText = EditorGUIUtility.TrTextContent("Shadows");
            public static GUIContent postProcessingSettingsText = EditorGUIUtility.TrTextContent("Post Processing");
            public static GUIContent advancedSettingsText = EditorGUIUtility.TrTextContent("Advanced");
            public static GUIContent debugSettingsText = EditorGUIUtility.TrTextContent("Pipeline Debug");

            // General
            public static GUIContent opaqueLayerMaskText = EditorGUIUtility.TrTextContent("Opaque Layer Mask", "Opaque rendering pass culling mask");
            public static GUIContent transparentLayerMaskText = EditorGUIUtility.TrTextContent("Transparent Layer Mask", "Transparent rendering pass culling mask");
            public static GUIContent requireDepthTextureText = EditorGUIUtility.TrTextContent("Depth Texture", "If enabled the pipeline will generate camera's depth that can be bound in shaders as _CameraDepthTexture");
            public static GUIContent requireOpaqueTextureText = EditorGUIUtility.TrTextContent("Opaque Texture", "If enabled the pipeline will copy the screen to texture after opaque objects are drawn. For transparent objects this can be bound in shaders as _CameraOpaqueTexture");
            public static GUIContent opaqueDownsamplingText = EditorGUIUtility.TrTextContent("Opaque Downsampling", "The downsampling method that is used for the opaque texture");

            // Quality
            public static GUIContent hdrText = EditorGUIUtility.TrTextContent("HDR", "Controls the global HDR settings.");
            public static GUIContent msaaText = EditorGUIUtility.TrTextContent("Anti Aliasing (MSAA)", "Controls the global anti aliasing settings.");
            public static GUIContent renderScaleText = EditorGUIUtility.TrTextContent("Render Scale", "Scales the camera render target allowing the game to render at a resolution different than native resolution. UI is always rendered at native resolution. When VR is enabled, this is overridden by XRSettings.");

            // Shadow settings
            public static GUIContent supportMainLightShadowText = EditorGUIUtility.TrTextContent("Main Light Shadow", "If main light can cast shadows");
            public static GUIContent supportSoftShadowText = EditorGUIUtility.TrTextContent("Soft Shadow", "If enabled pipeline will perform shadow filtering. Otherwise all lights that cast shadows will fallback to perform a single shadow sample");
            public static GUIContent shadowDistanceText = EditorGUIUtility.TrTextContent("Distance", "Maximum shadow rendering distance");
            public static GUIContent mainLightShadowmapResolutionText = EditorGUIUtility.TrTextContent("Shadow Resolution", "Resolution of the main light shadowmap texture. If cascades are enabled, cascades will be packed into an atlas and this setting controls the maximum shadows atlas resolution");
            public static GUIContent shadowDepthBiasText = EditorGUIUtility.TrTextContent("Depth Bias", "Controls the distance at which the shadows will be pushed away from the light. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent shadowNormalBiasText = EditorGUIUtility.TrTextContent("Normal Bias", "Controls distance at which the shadow casting surfaces will be shrunk along the surface normal. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent shadowCascadesText = EditorGUIUtility.TrTextContent("Cascades", "Number of cascade splits used in for directional shadows");

            // Post Processing settings
            public static GUIContent postprocessText = EditorGUIUtility.TrTextContent("Post Process", "Controls the global post process settings");
            public static GUIContent colorGradingModeText = EditorGUIUtility.TrTextContent("Color Grading Mode", "Color Grading Mode");
            public static GUIContent colorGradingLutSizeText = EditorGUIUtility.TrTextContent("Color Grading LUT Size", "Color Grading LUT Size");

            // Advanced settings
            public static GUIContent useSRPBatcherText = EditorGUIUtility.TrTextContent("SRP Batcher", "If enabled, the render pipeline uses the SRP batcher.");
            public static GUIContent useDynamicBatchingText = EditorGUIUtility.TrTextContent("Dynamic Batching", "If enabled, the render pipeline will batch drawcalls with few triangles together by copying their vertex buffers into a shared buffer on a per-frame basis.");
            public static GUIContent supportMixedLightingText = EditorGUIUtility.TrTextContent("Mixed Lighting", "Makes the render pipeline include mixed-lighting Shader Variants in the build.");
            public static GUIContent disableAdditionalLightsText = EditorGUIUtility.TrTextContent("Disable AddtionalLights", "If enabled, additional lights will be ignored during rendering");
            public static GUIContent useClusterLightingText = EditorGUIUtility.TrTextContent("Cluster Lighting", "If enabled, cluster lighting wiil be used to handle multiple realtime lights instead of addtional lighting");
            public static GUIContent overrideCameraEnabledText = EditorGUIUtility.TrTextContent("Enable Override Camera", "If enabled, override camera render pass will be added to current renderer");
            public static GUIContent overrideOpaqueLayerMaskText = EditorGUIUtility.TrTextContent("Opaque LayerMask", "Opaque LayerMask used by override camera render pass");
            public static GUIContent overrideTransparentLayerMaskText = EditorGUIUtility.TrTextContent("Transparent LayerMask", "Transparent LayerMask used by override camera render pass");
            public static GUIContent overridePositionOffsetText = EditorGUIUtility.TrTextContent("Position Offset", "Camera's position offset used by override camera render pass");
            public static GUIContent overrideFieldOfViewText = EditorGUIUtility.TrTextContent("Field Of View", "Camera's field of view used by override camera render pass");
            public static GUIContent overrideNearClipPlaneText = EditorGUIUtility.TrTextContent("Near Clip Plane", "Camera's near clip plane used by override camera render pass");
            public static GUIContent overrideFarClipPlaneText = EditorGUIUtility.TrTextContent("Far Clip Plane", "Camera's far clip plane used by override camera render pass");

            // Debug settings
            public static GUIContent enablePipelineDebugText = EditorGUIUtility.TrTextContent("Pipeline Debug", "Enable pipeline debug");
            public static GUIContent materialDebugModeText = EditorGUIUtility.TrTextContent("Material Debug", "Material debug mode");
            public static GUIContent wireframeModeText = EditorGUIUtility.TrTextContent("Wireframe Mode", "Wireframe Mode");
            public static GUIContent wireframeColorText = EditorGUIUtility.TrTextContent("Wireframe Color", "Wireframe Color");
            public static GUIContent wireframeOverrideColorText = EditorGUIUtility.TrTextContent("Wireframe Override Color", "Override color for SolidColorWireframe mode");
            public static GUIContent validateMaterialPureMetalText = EditorGUIUtility.TrTextContent("Validate Pure Metal", "Validate material pure metal");
            public static GUIContent validateMaterialPureMetalColorText = EditorGUIUtility.TrTextContent("Validate Pure Metal Color", "Show this color if material is not a pure metal");
            public static GUIContent validateMaterialHighColorText = EditorGUIUtility.TrTextContent("Validate High Color", "Show this color if material's diffuse or specular color is too high");
            public static GUIContent validateMaterialLowColorText = EditorGUIUtility.TrTextContent("Validate Low Color", "Show this color if material's diffuse or specular color is too low");
            public static GUIContent showDirectSpecularText = EditorGUIUtility.TrTextContent("Direct Specular Lighting", "Show realtime direct specular lighting");
            public static GUIContent showIndirectSpecularText = EditorGUIUtility.TrTextContent("Indirect Specular Lighting", "Show indirect specular lighting");
            public static GUIContent showDirectDiffuseText = EditorGUIUtility.TrTextContent("Direct Diffuse Lighting", "Show realtime direct diffuse lighting");
            public static GUIContent showIndirectDiffuseText = EditorGUIUtility.TrTextContent("Indirect Diffuse Lighting", "Show indirect diffuse lighting");


            // Dropdown menu options
            public static string[] shadowCascadeOptions = { "No Cascades", "Two Cascades", "Four Cascades" };
            public static string[] opaqueDownsamplingOptions = { "None", "2x (Bilinear)", "4x (Box)", "4x (Bilinear)" };
        }

        SavedBool generalSettingsFoldout;
        SavedBool qualitySettingsFoldout;
        SavedBool shadowSettingsFoldout;
        SavedBool postProcessingSettingsFoldout;
        SavedBool advancedSettingsFoldout;
        SavedBool debugSettingsFoldout;

        // general
        SerializedProperty opaqueLayerMaskProp;
        SerializedProperty transparentLayerMaskProp;
        SerializedProperty requireDepthTextureProp;
        SerializedProperty requireOpaqueTextureProp;
        SerializedProperty opaqueDownsamplingProp;
        // quality
        SerializedProperty useHDRProp;
        SerializedProperty msaaQualityProp;
        SerializedProperty renderScaleProp;
        // shadow
        SerializedProperty supportMainLightShadowProp;
        SerializedProperty supportSoftShadowProp;
        SerializedProperty shadowDistanceProp;
        SerializedProperty shadowmapResolutionProp;
        SerializedProperty shadowDepthBiasProp;
        SerializedProperty shadowNormalBiasProp;
        SerializedProperty shadowCascadesOptionProp;
        SerializedProperty shadowCascade2SplitProp;
        SerializedProperty shadowCascade4SplitProp;
        // post processing
        SerializedProperty usePostProcessProp;
        SerializedProperty colorGradingModeProp;
        SerializedProperty colorGradingLutSizeProp;
        // advanced
        SerializedProperty useSRPBatcherProp;
        SerializedProperty useDynamicBatchingProp;
        SerializedProperty supportMixedLightingProp;
        SerializedProperty disableAdditionalLightsProp;
        SerializedProperty useClusterLightingProp;
        SerializedProperty overrideCameraSettings;
        SerializedProperty overrideCameraEnabled;
        SerializedProperty overrideOpaqueLayerMask;
        SerializedProperty overrideTransparentLayerMask;
        SerializedProperty overridePositionOffset;
        SerializedProperty overrideFieldOfView;
        SerializedProperty overrideNearClipPlane;
        SerializedProperty overrideFarClipPlane;
        // debug
        SerializedProperty enablePipelineDebugProp;
        SerializedProperty materialDebugModeProp;
        SerializedProperty wireframeModeProp;
        SerializedProperty wireframeColorProp;
        SerializedProperty wireframeOverrideColorProp;
        SerializedProperty validateMaterialPureMetalProp;
        SerializedProperty validateMaterialPureMetalColorProp;
        SerializedProperty validateMaterialHighColorProp;
        SerializedProperty validateMaterialLowColorProp;
        SerializedProperty showDirectSpecularProp;
        SerializedProperty showIndirectSpecularProp;
        SerializedProperty showDirectDiffuseProp;
        SerializedProperty showIndirectDiffuseProp;

        public override void OnInspectorGUI() {
            serializedObject.Update();

            DrawGeneralSettings();
            DrawQualitySettings();
            DrawShadowSettings();
            DrawPostProcessingSettings();
            DrawAdvancedSettings();
            DrawDebugSettings();

            serializedObject.ApplyModifiedProperties();
        }

        void OnEnable() {
            // general
            generalSettingsFoldout = new SavedBool($"{target.GetType()}.GeneralSettingsFoldout", false);
            qualitySettingsFoldout = new SavedBool($"{target.GetType()}.QualitySettingsFoldout", false);
            shadowSettingsFoldout = new SavedBool($"{target.GetType()}.ShadowSettingsFoldout", false);
            postProcessingSettingsFoldout = new SavedBool($"{target.GetType()}.PostProcessingSettingsFoldout", false);
            advancedSettingsFoldout = new SavedBool($"{target.GetType()}.AdvancedSettingsFoldout", false);
            debugSettingsFoldout = new SavedBool($"{target.GetType()}.DebugSettingsFoldout", false);

            opaqueLayerMaskProp = serializedObject.FindProperty("opaqueLayerMask");
            transparentLayerMaskProp = serializedObject.FindProperty("transparentLayerMask");
            requireOpaqueTextureProp = serializedObject.FindProperty("requireOpaqueTexture");
            requireDepthTextureProp = serializedObject.FindProperty("requireDepthTexture");
            opaqueDownsamplingProp = serializedObject.FindProperty("opaqueDownsampling");

            // quality
            useHDRProp = serializedObject.FindProperty("useHDR");
            msaaQualityProp = serializedObject.FindProperty("msaaQuality");
            renderScaleProp = serializedObject.FindProperty("renderScale");

            // shadow
            supportMainLightShadowProp = serializedObject.FindProperty("supportMainLightShadow");
            supportSoftShadowProp = serializedObject.FindProperty("supportSoftShadow");
            shadowDistanceProp = serializedObject.FindProperty("shadowDistance");
            shadowmapResolutionProp = serializedObject.FindProperty("shadowmapResolution");
            shadowDepthBiasProp = serializedObject.FindProperty("shadowDepthBias");
            shadowNormalBiasProp = serializedObject.FindProperty("shadowNormalBias");
            shadowCascadesOptionProp = serializedObject.FindProperty("shadowCascadesOption");
            shadowCascade2SplitProp = serializedObject.FindProperty("cascades2Split");
            shadowCascade4SplitProp = serializedObject.FindProperty("cascades4Split");

            // post processing
            usePostProcessProp = serializedObject.FindProperty("usePostProcess");
            colorGradingModeProp = serializedObject.FindProperty("colorGradingMode");
            colorGradingLutSizeProp = serializedObject.FindProperty("colorGradingLutSize");

            // advanced
            useSRPBatcherProp = serializedObject.FindProperty("useSRPBatcher");
            useDynamicBatchingProp = serializedObject.FindProperty("useDynamicBatching");
            supportMixedLightingProp = serializedObject.FindProperty("supportMixedLighting");
            disableAdditionalLightsProp = serializedObject.FindProperty("disableAdditionalLights");
            useClusterLightingProp = serializedObject.FindProperty("useClusterLighting");
            overrideCameraSettings = serializedObject.FindProperty("overrideCameraSettings");
            overrideCameraEnabled = overrideCameraSettings.FindPropertyRelative("enabled");
            overrideOpaqueLayerMask = overrideCameraSettings.FindPropertyRelative("opaqueLayerMask");
            overrideTransparentLayerMask = overrideCameraSettings.FindPropertyRelative("transparentLayerMask");
            overridePositionOffset = overrideCameraSettings.FindPropertyRelative("positionOffset");
            overrideFieldOfView = overrideCameraSettings.FindPropertyRelative("fieldOfView");
            overrideNearClipPlane = overrideCameraSettings.FindPropertyRelative("nearClipPlane");
            overrideFarClipPlane = overrideCameraSettings.FindPropertyRelative("farClipPlane");

            // debug
            enablePipelineDebugProp = serializedObject.FindProperty("enablePipelineDebug");
            materialDebugModeProp = serializedObject.FindProperty("materialDebugMode");
            wireframeModeProp = serializedObject.FindProperty("wireframeMode");
            wireframeColorProp = serializedObject.FindProperty("wireframeColor");
            wireframeOverrideColorProp = serializedObject.FindProperty("wireframeOverrideColor");
            validateMaterialPureMetalProp = serializedObject.FindProperty("validateMaterialPureMetal");
            validateMaterialPureMetalColorProp = serializedObject.FindProperty("validateMaterialPureMetalColor");
            validateMaterialHighColorProp = serializedObject.FindProperty("validateMaterialHighColor");
            validateMaterialLowColorProp = serializedObject.FindProperty("validateMaterialLowColor");
            showDirectSpecularProp = serializedObject.FindProperty("showDirectSpecular");
            showIndirectSpecularProp = serializedObject.FindProperty("showIndirectSpecular");
            showDirectDiffuseProp = serializedObject.FindProperty("showDirectDiffuse");
            showIndirectDiffuseProp = serializedObject.FindProperty("showIndirectDiffuse");
        }

        void DrawGeneralSettings() {
            generalSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(generalSettingsFoldout.value, Styles.generalSettingsText);
            if (generalSettingsFoldout.value) {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space();
                XRenderPipelineAsset asset = target as XRenderPipelineAsset;
                EditorGUILayout.PropertyField(opaqueLayerMaskProp, Styles.opaqueLayerMaskText);
                EditorGUILayout.PropertyField(transparentLayerMaskProp, Styles.transparentLayerMaskText);
                EditorGUILayout.PropertyField(requireDepthTextureProp, Styles.requireDepthTextureText);
                EditorGUILayout.PropertyField(requireOpaqueTextureProp, Styles.requireOpaqueTextureText);
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(!requireOpaqueTextureProp.boolValue);
                EditorGUILayout.PropertyField(opaqueDownsamplingProp, Styles.opaqueDownsamplingText);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawQualitySettings() {
            qualitySettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(qualitySettingsFoldout.value, Styles.qualitySettingsText);
            if (qualitySettingsFoldout.value) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(useHDRProp, Styles.hdrText);
                EditorGUILayout.PropertyField(msaaQualityProp, Styles.msaaText);
                renderScaleProp.floatValue = EditorGUILayout.Slider(Styles.renderScaleText, renderScaleProp.floatValue, XRenderPipeline.MinRenderScale, XRenderPipeline.MaxRenderScale);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawShadowSettings() {
            shadowSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(shadowSettingsFoldout.value, Styles.shadowSettingsText);
            if (shadowSettingsFoldout.value) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(supportMainLightShadowProp, Styles.supportMainLightShadowText);
                EditorGUILayout.PropertyField(supportSoftShadowProp, Styles.supportSoftShadowText);
                shadowDistanceProp.floatValue = Mathf.Max(0.0f, EditorGUILayout.FloatField(Styles.shadowDistanceText, shadowDistanceProp.floatValue));
                EditorGUILayout.PropertyField(shadowmapResolutionProp, Styles.mainLightShadowmapResolutionText);
                shadowDepthBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowDepthBiasText, shadowDepthBiasProp.floatValue, 0.0f, XRenderPipeline.MaxShadowBias);
                shadowNormalBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowNormalBiasText, shadowNormalBiasProp.floatValue, 0.0f, XRenderPipeline.MaxShadowBias);

                CoreEditorUtils.DrawPopup(Styles.shadowCascadesText, shadowCascadesOptionProp, Styles.shadowCascadeOptions);
                ShadowCascadesOption cascades = (ShadowCascadesOption)shadowCascadesOptionProp.intValue;
                if (cascades == ShadowCascadesOption.FourCascades) {
                    DrawCascadeSplitGUI<Vector3>(ref shadowCascade4SplitProp);
                } else if (cascades == ShadowCascadesOption.TwoCascades) {
                    DrawCascadeSplitGUI<float>(ref shadowCascade2SplitProp);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawPostProcessingSettings() {
            postProcessingSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(postProcessingSettingsFoldout.value, Styles.postProcessingSettingsText);
            if (postProcessingSettingsFoldout.value) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(usePostProcessProp, Styles.postprocessText);
                EditorGUI.BeginDisabledGroup(!usePostProcessProp.boolValue);
                EditorGUILayout.PropertyField(colorGradingModeProp, Styles.colorGradingModeText);
                EditorGUILayout.PropertyField(colorGradingLutSizeProp, Styles.colorGradingLutSizeText);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawAdvancedSettings() {
            advancedSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(advancedSettingsFoldout.value, Styles.advancedSettingsText);
            if (advancedSettingsFoldout.value) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(useSRPBatcherProp, Styles.useSRPBatcherText);
                EditorGUILayout.PropertyField(useDynamicBatchingProp, Styles.useDynamicBatchingText);
                EditorGUILayout.PropertyField(supportMixedLightingProp, Styles.supportMixedLightingText);
                EditorGUILayout.PropertyField(disableAdditionalLightsProp, Styles.disableAdditionalLightsText);
                EditorGUILayout.PropertyField(useClusterLightingProp, Styles.useClusterLightingText);
                // override camera
                EditorGUILayout.PropertyField(overrideCameraEnabled, Styles.overrideCameraEnabledText);
                EditorGUI.BeginDisabledGroup(!overrideCameraEnabled.boolValue);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(overrideOpaqueLayerMask, Styles.overrideOpaqueLayerMaskText);
                EditorGUILayout.PropertyField(overrideTransparentLayerMask, Styles.overrideTransparentLayerMaskText);
                EditorGUILayout.PropertyField(overridePositionOffset, Styles.overridePositionOffsetText);
                EditorGUILayout.PropertyField(overrideFieldOfView, Styles.overrideFieldOfViewText);
                EditorGUILayout.PropertyField(overrideNearClipPlane, Styles.overrideNearClipPlaneText);
                EditorGUILayout.PropertyField(overrideFarClipPlane, Styles.overrideFarClipPlaneText);
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawDebugSettings() {
            debugSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(debugSettingsFoldout.value, Styles.debugSettingsText);
            if (debugSettingsFoldout.value) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(enablePipelineDebugProp, Styles.enablePipelineDebugText);
                EditorGUI.BeginDisabledGroup(!enablePipelineDebugProp.boolValue);
                EditorGUILayout.PropertyField(materialDebugModeProp, Styles.materialDebugModeText);
                EditorGUILayout.PropertyField(wireframeModeProp, Styles.wireframeModeText);
                EditorGUILayout.PropertyField(wireframeColorProp, Styles.wireframeColorText);
                EditorGUILayout.PropertyField(wireframeOverrideColorProp, Styles.wireframeOverrideColorText);
                EditorGUILayout.PropertyField(validateMaterialPureMetalProp, Styles.validateMaterialPureMetalText);
                EditorGUILayout.PropertyField(validateMaterialPureMetalColorProp, Styles.validateMaterialPureMetalColorText);
                EditorGUILayout.PropertyField(validateMaterialHighColorProp, Styles.validateMaterialHighColorText);
                EditorGUILayout.PropertyField(validateMaterialLowColorProp, Styles.validateMaterialLowColorText);
                EditorGUILayout.PropertyField(showDirectSpecularProp, Styles.showDirectSpecularText);
                EditorGUILayout.PropertyField(showIndirectSpecularProp, Styles.showIndirectSpecularText);
                EditorGUILayout.PropertyField(showDirectDiffuseProp, Styles.showDirectDiffuseText);
                EditorGUILayout.PropertyField(showIndirectDiffuseProp, Styles.showIndirectDiffuseText);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }

        void DrawCascadeSplitGUI<T>(ref SerializedProperty shadowCascadeSplit) {
            float[] cascadePartitionSizes = null;
            Type type = typeof(T);
            if (type == typeof(float)) {
                cascadePartitionSizes = new float[] { shadowCascadeSplit.floatValue };
            } else if (type == typeof(Vector3)) {
                Vector3 splits = shadowCascadeSplit.vector3Value;
                cascadePartitionSizes = new float[] {
                    Mathf.Clamp(splits[0], 0.0f, 1.0f),
                    Mathf.Clamp(splits[1] - splits[0], 0.0f, 1.0f),
                    Mathf.Clamp(splits[2] - splits[1], 0.0f, 1.0f)
                };
            }
            if (cascadePartitionSizes != null) {
                EditorGUI.BeginChangeCheck();
                ShadowCascadeSplitGUI.HandleCascadeSliderGUI(ref cascadePartitionSizes);
                if (EditorGUI.EndChangeCheck()) {
                    if (type == typeof(float)) {
                        shadowCascadeSplit.floatValue = cascadePartitionSizes[0];
                    } else {
                        Vector3 updatedValue = new Vector3();
                        updatedValue[0] = cascadePartitionSizes[0];
                        updatedValue[1] = updatedValue[0] + cascadePartitionSizes[1];
                        updatedValue[2] = updatedValue[1] + cascadePartitionSizes[2];
                        shadowCascadeSplit.vector3Value = updatedValue;
                    }
                }
            }
        }
    }
}
