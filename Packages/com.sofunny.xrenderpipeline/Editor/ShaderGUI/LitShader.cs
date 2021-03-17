using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Framework.XRenderPipeline {
    using SurfaceType = ShaderGUICommon.SurfaceType;
    using CullMode = ShaderGUICommon.CullMode;

    public class LitShader : ShaderGUI {

        enum BlendMode {
            Alpha = 0,
            Premultiply,
            Additive,
            Multiply
        }

        enum MetallicAORoughnessMode {
            CombinedTexture = 0,
            CombinedTextureWithOffset,
            SeparateTextureWithOffset
        }

        enum ShadingQuality {
            High = 0,
            Medium,
            Low
        }

        enum EnvBRDFApprox {
            V1 = 0,
            V2
        }

        const string useSeparateMapKeyword = "_USE_SEPARATEMAP";
        const string useMetallicAORoughnessMapKeyword = "_USE_METALLICAOROUGHNESSMAP";
        const string useMetallicAORoughnessOffsetKeyword = "_USE_METALLICAOROUGHNESSOFFSET";
        const string useNormalMapKeyword = "_USE_NORMALMAP";
        const string useEmissionKeyword = "_USE_EMISSION";
        const string useAlphaTestKeyword = "_USE_ALPHATEST";
        const string receiveShadowsKeyword = "_RECEIVE_SHADOWS";
        const string useGlossyEnvReflectionKeyword = "_USE_GLOSSYENVREFLECTION";
        const string useEnergyCompensationKeyword = "_USE_ENERGYCOMPENSATION";
        const string useSpecularAOKeyword = "_USE_SPECULARAO";
        const string useFakeEnvSpecularKeyword = "_USE_FAKEENVSPECULAR";
        const string shadingQualityHighKeyword = "_SHADINGQUALITY_HIGH";
        const string shadingQualityMediumKeyword = "_SHADINGQUALITY_MEDIUM";
        const string shadingQualityLowKeyword = "_SHADINGQUALITY_LOW";
        const string envBRDFApproxV2Keyword = "_ENVBRDFAPPROX_V2";
        const string debugMaterialKeyword = "_DEBUG_MATERIAL";

        const int queueOffsetRange = 50;

        bool firstTimeApply = true;

        static class Styles {
            public static readonly GUIContent surfaceType = new GUIContent("Surface Type", "Surface Type");
            public static readonly GUIContent blendMode = new GUIContent("Blend Mode", "Blend Mode");
            public static readonly GUIContent shadingQuality = new GUIContent("Shading Quality", "Shading Quality");
            public static readonly GUIContent envBRDFApprox = new GUIContent("EnvBRDF Approx Version", "EnvBRDF Approx Version");
            public static readonly GUIContent cullMode = new GUIContent("Cull Mode", "Cull Mode");
            public static readonly GUIContent alphaTest = new GUIContent("Alpha Test", "Alpha Test");
            public static readonly GUIContent receiveShadows = new GUIContent("Receive Shadows", "Receive Shadows");
            public static readonly GUIContent baseMap = new GUIContent("Base Map", "Base Map");
            public static readonly GUIContent metallicAORoughnessMode = new GUIContent("MetallicAORoughness Mode", "Please use CombinedTexture Mode in Release for performance reason");
            public static readonly GUIContent ambientOcclusionMap = new GUIContent("Ambient Occlusion Map", "Ambient Occlusion Map");
            public static readonly GUIContent roughnessMap = new GUIContent("Roughness Map", "Roughness Map");
            public static readonly GUIContent metallicMap = new GUIContent("Metallic Map", "Metallic Map");
            public static readonly GUIContent metallicAORoughnessMap = new GUIContent("MetallicAORoughness Map", "r: metallic, g: ao, b: roughness");
            public static readonly GUIContent normalMap = new GUIContent("Normal Map", "Tangent Space Normal Map");
            public static readonly GUIContent emissionMap = new GUIContent("Emission Map", "Emission Map");
            public static readonly GUIContent queueSlider = new GUIContent("Priority", "Determines the chronological rendering order for a Material. High values are rendered first.");
            public static readonly GUIContent depthOffset = new GUIContent("Depth Offset", "Enable depth offset to eliminate z-fighting for polygons in the same position");
            public static readonly GUIContent debugMode = new GUIContent("Debug Mode", "Debug Mode");
        }

        public virtual void OnOpenGUI(Material material, MaterialEditor materialEditor) {
            foreach (var obj in  materialEditor.targets) {
                    SetupMaterialBlendMode((Material)obj);
                    SetupMaterialKeywords((Material)obj);
                    SetDebugParams((Material)obj);
            }
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) {
            var material = (Material)materialEditor.target;

            if (firstTimeApply) {
                OnOpenGUI(material, materialEditor);
                firstTimeApply = false;
            }

            EditorGUI.BeginChangeCheck();

            // surface options
            var surfaceTypeProp = properties.First(x => x.name == "_SurfaceType");
            ShaderGUICommon.DoPopup(Styles.surfaceType, surfaceTypeProp, Enum.GetNames(typeof(SurfaceType)), materialEditor);

            if ((SurfaceType)material.GetFloat("_SurfaceType") == SurfaceType.Transparent) {
                var blendModeProp = properties.First(x => x.name == "_BlendMode");
                ShaderGUICommon.DoPopup(Styles.blendMode, blendModeProp, Enum.GetNames(typeof(BlendMode)), materialEditor);
            }

            var shadingQualityProp = properties.First(x => x.name == "_ShadingQuality");
            ShaderGUICommon.DoPopup(Styles.shadingQuality, shadingQualityProp, Enum.GetNames(typeof(ShadingQuality)), materialEditor);

            var envBRDFApproxProp = properties.First(x => x.name == "_EnvBRDFApprox");
            ShaderGUICommon.DoPopup(Styles.envBRDFApprox, envBRDFApproxProp, Enum.GetNames(typeof(EnvBRDFApprox)), materialEditor);

            EditorGUI.BeginChangeCheck();
            var cullModeProp = properties.First(x => x.name == "_Cull");
            EditorGUI.showMixedValue = cullModeProp.hasMixedValue;
            var cullMode = (CullMode)cullModeProp.floatValue;
            cullMode = (CullMode)EditorGUILayout.EnumPopup(Styles.cullMode, cullMode);
            if (EditorGUI.EndChangeCheck()) {
                materialEditor.RegisterPropertyChangeUndo(Styles.cullMode.text);
                cullModeProp.floatValue = (float)cullMode;
                material.doubleSidedGI = (CullMode)cullModeProp.floatValue != CullMode.Back;
            }
            EditorGUI.showMixedValue = false;

            EditorGUI.BeginChangeCheck();
            var alphaTestProp = properties.First(x => x.name == "_AlphaTest");
            EditorGUI.showMixedValue = alphaTestProp.hasMixedValue;
            var alphaClipEnabled = EditorGUILayout.Toggle(Styles.alphaTest, alphaTestProp.floatValue == 1);
            if (EditorGUI.EndChangeCheck()) {
                alphaTestProp.floatValue = alphaClipEnabled ? 1 : 0;
            }
            EditorGUI.showMixedValue = false;

            if (alphaTestProp.floatValue == 1) {
                var cutoffProp = properties.First(x => x.name == "_Cutoff");
                materialEditor.ShaderProperty(cutoffProp, cutoffProp.displayName);
            }

            EditorGUI.BeginChangeCheck();
            var receiveShadowsProp = properties.First(x => x.name == "_ReceiveShadows");
            EditorGUI.showMixedValue = receiveShadowsProp.hasMixedValue;
            var receiveShadows = EditorGUILayout.Toggle(Styles.receiveShadows, receiveShadowsProp.floatValue == 1.0f);
            if (EditorGUI.EndChangeCheck()) {
                receiveShadowsProp.floatValue = receiveShadows ? 1.0f : 0.0f;
            }
            EditorGUI.showMixedValue = false;

            EditorGUILayout.Space();

            // surface inputs
            var baseColorProp = properties.First(x => x.name == "_BaseColor");
            var baseMapProp = properties.First(x => x.name == "_BaseMap");
            materialEditor.TexturePropertySingleLine(Styles.baseMap, baseMapProp, baseColorProp);
            materialEditor.TextureScaleOffsetProperty(baseMapProp);

            // ao roughness metallic settings
            using (new EditorGUILayout.VerticalScope("box")) {
                var metallicAORoughnessTextureMode = properties.First(x => x.name == "_MetallicAORoughnessTextureMode");
                ShaderGUICommon.DoPopup(Styles.metallicAORoughnessMode, metallicAORoughnessTextureMode, Enum.GetNames(typeof(MetallicAORoughnessMode)), materialEditor);
                bool useSeparateMap = (MetallicAORoughnessMode)metallicAORoughnessTextureMode.floatValue == MetallicAORoughnessMode.SeparateTextureWithOffset;
                bool useCombinedMapWithOffset = (MetallicAORoughnessMode)metallicAORoughnessTextureMode.floatValue == MetallicAORoughnessMode.CombinedTextureWithOffset;

                if (useSeparateMap) {
                    var metallicMapProp = properties.First(x => x.name == "_MetallicMap");
                    var metallicProp = properties.First(x => x.name == "_MetallicOffset");
                    materialEditor.TexturePropertySingleLine(Styles.metallicMap, metallicMapProp, metallicProp);
                    var ambientOcclusionMapProp = properties.First(x => x.name == "_AmbientOcclusionMap");
                    var ambientOcclusionProp = properties.First(x => x.name == "_AmbientOcclusionOffset");
                    materialEditor.TexturePropertySingleLine(Styles.ambientOcclusionMap, ambientOcclusionMapProp, ambientOcclusionProp);
                    var roughnessMapProp = properties.First(x => x.name == "_RoughnessMap");
                    var roughnessProp = properties.First(x => x.name == "_RoughnessOffset");
                    materialEditor.TexturePropertySingleLine(Styles.roughnessMap, roughnessMapProp, roughnessProp);
                } else {
                    var metallicAORoughnessMapProp = properties.First(x => x.name == "_MetallicAORoughnessMap");
                    materialEditor.TexturePropertySingleLine(Styles.metallicAORoughnessMap, metallicAORoughnessMapProp);
                    if (useCombinedMapWithOffset) {
                        var metallicProp = properties.First(x => x.name == "_MetallicOffset");
                        materialEditor.ShaderProperty(metallicProp, metallicProp.displayName);
                        var ambientOcclusionProp = properties.First(x => x.name == "_AmbientOcclusionOffset");
                        materialEditor.ShaderProperty(ambientOcclusionProp, ambientOcclusionProp.displayName);
                        var roughnessProp = properties.First(x => x.name == "_RoughnessOffset");
                        materialEditor.ShaderProperty(roughnessProp, roughnessProp.displayName);
                    }
                }
            }

            var reflectanceProp = properties.First(x => x.name == "_Reflectance");
            materialEditor.ShaderProperty(reflectanceProp, reflectanceProp.displayName);

            var normalMapProp = properties.First(x => x.name == "_NormalMap");
            materialEditor.TexturePropertySingleLine(Styles.normalMap, normalMapProp);

            bool emissive = true;
            var emissionColorProp = properties.First(x => x.name == "_EmissionColor");
            var emissionMapProp = properties.First(x => x.name == "_EmissionMap");
            var hasEmissionMap = emissionMapProp.textureValue != null;
            // emission for GI?
            emissive = materialEditor.EmissionEnabledProperty();
            EditorGUI.BeginDisabledGroup(!emissive);
            // texture and HDR color controls
            materialEditor.TexturePropertyWithHDRColor(Styles.emissionMap, emissionMapProp, emissionColorProp, false);
            EditorGUI.EndDisabledGroup();

            // if texture was assigned and color was black set color to white
            var brightness = emissionColorProp.colorValue.maxColorComponent;
            if (emissionMapProp.textureValue != null && !hasEmissionMap && brightness <= 0f) {
                emissionColorProp.colorValue = Color.white;
            }

            // xrp does not support RealtimeEmissive. We set it to bake emissive and handle the emissive is black right.
            if (emissive) {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                if (brightness <= 0f) {
                    material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            }

            EditorGUI.BeginChangeCheck();
            var useGlossyEnvReflectionProp = properties.First(x => x.name == "_UseGlossyEnvReflection");
            EditorGUI.showMixedValue = useGlossyEnvReflectionProp.hasMixedValue;
            bool useGlossyEnvReflection = EditorGUILayout.Toggle("Use GlossyEnvReflection", useGlossyEnvReflectionProp.floatValue == 1.0f);
            if (EditorGUI.EndChangeCheck()) {
                useGlossyEnvReflectionProp.floatValue = useGlossyEnvReflection ? 1.0f : 0.0f;
            }
            EditorGUI.showMixedValue = false;

            // energy compensation and specular ao feature only enabled in high quality shading mode
            if((ShadingQuality)material.GetFloat("_ShadingQuality") == ShadingQuality.High) {
                EditorGUI.BeginChangeCheck();
                var useEnergyCompensationProp = properties.First(x => x.name == "_UseEnergyCompensation");
                EditorGUI.showMixedValue = useEnergyCompensationProp.hasMixedValue;
                bool useEnergyCompensation = EditorGUILayout.Toggle("Use EnergyCompensation", useEnergyCompensationProp.floatValue == 1.0f);
                if (EditorGUI.EndChangeCheck()) {
                    useEnergyCompensationProp.floatValue = useEnergyCompensation ? 1.0f : 0.0f;
                }
                EditorGUI.showMixedValue = false;

                EditorGUI.BeginChangeCheck();
                var useSpecularAOProp = properties.First(x => x.name == "_UseSpecularAO");
                EditorGUI.showMixedValue = useSpecularAOProp.hasMixedValue;
                bool useSpecularAO = EditorGUILayout.Toggle("Use SpecularAO", useSpecularAOProp.floatValue == 1.0f);
                if (EditorGUI.EndChangeCheck()) {
                    useSpecularAOProp.floatValue = useSpecularAO ? 1.0f : 0.0f;
                }
                EditorGUI.showMixedValue = false;
            }

            // fake env specular should not be used when glossyEnvReflection is enabled
            if (material.GetFloat("_UseGlossyEnvReflection") == 0) {
                EditorGUI.BeginChangeCheck();
                var useFakeEnvSpecularProp = properties.First(x => x.name == "_UseFakeEnvSpecular");
                EditorGUI.showMixedValue = useFakeEnvSpecularProp.hasMixedValue;
                bool useFakeEnvSpecular = EditorGUILayout.Toggle("Use FakeEnvSpecular", useFakeEnvSpecularProp.floatValue == 1.0f);
                if (EditorGUI.EndChangeCheck()) {
                    useFakeEnvSpecularProp.floatValue = useFakeEnvSpecular ? 1.0f : 0.0f;
                }
                EditorGUI.showMixedValue = false;
            }

            // EditorGUI.BeginChangeCheck();
            // var useTestFlagProp = properties.First(x => x.name == "_Test");
            // EditorGUI.showMixedValue = useTestFlagProp.hasMixedValue;
            // bool useTestFlag = EditorGUILayout.Toggle("Use Test", material.IsKeywordEnabled("_TEST"));
            // if (EditorGUI.EndChangeCheck()) {
            //     useTestFlagProp.floatValue = useTestFlag ? 1.0f : 0.0f;
            // }
            // EditorGUI.showMixedValue = false;

            // EditorGUILayout.Space();

            // advanced options
            materialEditor.EnableInstancingField();
            var queueOffsetProp = properties.First(x => x.name == "_QueueOffset");
            if (queueOffsetProp != null) {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = queueOffsetProp.hasMixedValue;
                var queue = EditorGUILayout.IntSlider(Styles.queueSlider, (int)queueOffsetProp.floatValue, -queueOffsetRange, queueOffsetRange);
                if (EditorGUI.EndChangeCheck())
                    queueOffsetProp.floatValue = queue;
                EditorGUI.showMixedValue = false;
            }

            EditorGUI.BeginChangeCheck();
            var depthOffsetProp = properties.First(x => x.name == "_DepthOffset");
            EditorGUI.showMixedValue = depthOffsetProp.hasMixedValue;
            var depthOffsetEnabled = EditorGUILayout.Toggle(Styles.depthOffset, depthOffsetProp.floatValue == -1);
            if (EditorGUI.EndChangeCheck()) {
                depthOffsetProp.floatValue = depthOffsetEnabled ? -1 : 0;
            }
            EditorGUI.showMixedValue = false;

            var debugModeProp = properties.First(x => x.name == "_DebugMode");
            string[] debugModeNames = Enum.GetNames(typeof(MaterialDebugMode));
            // NOTE: currently not support pbr validation via material inspector
            ShaderGUICommon.DoPopup(Styles.debugMode, debugModeProp, debugModeNames.Take(debugModeNames.Count() - 2).ToArray(), materialEditor);

            if (EditorGUI.EndChangeCheck()) {
                foreach (var obj in materialEditor.targets) {
                    SetupMaterialBlendMode((Material)obj);
                    SetupMaterialKeywords((Material)obj);
                    SetDebugParams((Material)obj);
                }
            }
        }

        static void SetDebugParams(Material material) {
            if (material.HasProperty("_DebugMode")) {
                material.SetInt("_IndividualMaterialDebugMode", (int)material.GetFloat("_DebugMode"));
            }
        }

        static void SetShadingQualityKeyword(Material material, ShadingQuality shadingQuality) {
            switch (shadingQuality) {
                case ShadingQuality.High:
                    material.EnableKeyword(shadingQualityHighKeyword);
                    material.DisableKeyword(shadingQualityMediumKeyword);
                    material.DisableKeyword(shadingQualityLowKeyword);
                    break;
                case ShadingQuality.Medium:
                    material.DisableKeyword(shadingQualityHighKeyword);
                    material.EnableKeyword(shadingQualityMediumKeyword);
                    material.DisableKeyword(shadingQualityLowKeyword);
                    break;
                case ShadingQuality.Low:
                    material.DisableKeyword(shadingQualityHighKeyword);
                    material.DisableKeyword(shadingQualityMediumKeyword);
                    material.EnableKeyword(shadingQualityLowKeyword);
                    break;
                default:
                    Debug.LogError("Unknown ShadingQuality");
                    break;
            }
        }

        public static void SetupMaterialBlendMode(Material material) {
            bool alphaTest = material.GetFloat("_AlphaTest") == 1;
            if (alphaTest) {
                material.EnableKeyword("_USE_ALPHATEST");
            } else {
                material.DisableKeyword("_USE_ALPHATEST");
            }
            var queueOffset = 0;
            if (material.HasProperty("_QueueOffset")) {
                queueOffset = queueOffsetRange - (int)material.GetFloat("_QueueOffset");
            }

            SurfaceType surfaceType = (SurfaceType)material.GetFloat("_SurfaceType");
            if (surfaceType == SurfaceType.Opaque) {
                if (alphaTest) {
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                } else {
                    material.renderQueue = (int)RenderQueue.Geometry;
                    material.SetOverrideTag("RenderType", "Opaque");
                }
                material.renderQueue += queueOffset;
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetShaderPassEnabled("ShadowCaster", true);
            } else {
                BlendMode blendMode = (BlendMode)material.GetFloat("_BlendMode");
                var queue = (int)RenderQueue.Transparent;

                // Specific Transparent Mode Settings
                switch (blendMode) {
                    case BlendMode.Alpha:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        break;
                    case BlendMode.Premultiply:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        // material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        break;
                    case BlendMode.Additive:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        break;
                    case BlendMode.Multiply:
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        // material.EnableKeyword("_ALPHAMODULATE_ON");
                        break;
                }
                // General Transparent Material Settings
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_ZWrite", 0);
                material.renderQueue = queue + queueOffset;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
        }

        public static void SetupMaterialKeywords(Material material) {
            if (material.HasProperty("_ShadingQuality")) {
                float shadingQuality = material.GetFloat("_ShadingQuality");
                SetShadingQualityKeyword(material, (ShadingQuality)shadingQuality);
            }

            if (material.HasProperty("_EnvBRDFApprox")) {
                if ((EnvBRDFApprox)material.GetFloat("_EnvBRDFApprox") == EnvBRDFApprox.V2) {
                    material.EnableKeyword(envBRDFApproxV2Keyword);
                } else {
                    material.DisableKeyword(envBRDFApproxV2Keyword);
                }
            }

            if (material.HasProperty("_ReceiveShadows")) {
                if (material.GetFloat("_ReceiveShadows") == 1.0f) {
                    material.EnableKeyword(receiveShadowsKeyword);
                } else {
                    material.DisableKeyword(receiveShadowsKeyword);
                }
            }

            if (material.HasProperty("_MetallicAORoughnessTextureMode")) {
                float textureMode = material.GetFloat("_MetallicAORoughnessTextureMode");
                if ((MetallicAORoughnessMode)textureMode == MetallicAORoughnessMode.CombinedTexture) {
                    material.EnableKeyword(useMetallicAORoughnessMapKeyword);
                    material.DisableKeyword(useSeparateMapKeyword);
                    material.DisableKeyword(useMetallicAORoughnessOffsetKeyword);
                } else if ((MetallicAORoughnessMode)textureMode == MetallicAORoughnessMode.CombinedTextureWithOffset) {
                    material.EnableKeyword(useMetallicAORoughnessMapKeyword);
                    material.EnableKeyword(useMetallicAORoughnessOffsetKeyword);
                    material.DisableKeyword(useSeparateMapKeyword);
                } else if ((MetallicAORoughnessMode)textureMode == MetallicAORoughnessMode.SeparateTextureWithOffset) {
                    material.EnableKeyword(useSeparateMapKeyword);
                    material.EnableKeyword(useMetallicAORoughnessOffsetKeyword);
                    material.DisableKeyword(useMetallicAORoughnessMapKeyword);
                } else {
                    Debug.LogError("Unknown PBR Texture Mode");
                }
            }

            if (material.GetTexture("_NormalMap")) {
                material.EnableKeyword(useNormalMapKeyword);
            } else {
                material.DisableKeyword(useNormalMapKeyword);
            }

            // Emission
            if (material.HasProperty("_EmissionColor")) {
                MaterialEditor.FixupEmissiveFlag(material);
            }
            bool shouldEmissionBeEnabled = (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
            if (shouldEmissionBeEnabled) {
                material.EnableKeyword(useEmissionKeyword);
            } else {
                material.DisableKeyword(useEmissionKeyword);
            }

            if (material.HasProperty("_UseGlossyEnvReflection")) {
                if (material.GetFloat("_UseGlossyEnvReflection") == 1.0f) {
                    material.EnableKeyword(useGlossyEnvReflectionKeyword);
                } else {
                    material.DisableKeyword(useGlossyEnvReflectionKeyword);
                }
            }

            if ((ShadingQuality)material.GetFloat("_ShadingQuality") == ShadingQuality.High) {
                if (material.HasProperty("_UseEnergyCompensation")) {
                    if (material.GetFloat("_UseEnergyCompensation") == 1.0f) {
                        material.EnableKeyword(useEnergyCompensationKeyword);
                    } else {
                        material.DisableKeyword(useEnergyCompensationKeyword);
                    }
                }
                if (material.HasProperty("_UseSpecularAO")) {
                    if (material.GetFloat("_UseSpecularAO") == 1.0f) {
                        material.EnableKeyword(useSpecularAOKeyword);
                    } else {
                        material.DisableKeyword(useSpecularAOKeyword);
                    }
                }
            } else {
                material.DisableKeyword(useEnergyCompensationKeyword);
                material.DisableKeyword(useSpecularAOKeyword);
            }

            if (material.GetFloat("_UseGlossyEnvReflection") == 0.0f) {
                if (material.HasProperty("_UseFakeEnvSpecular")) {
                    if (material.GetFloat("_UseFakeEnvSpecular") == 1.0f) {
                        material.EnableKeyword(useFakeEnvSpecularKeyword);
                    } else {
                        material.DisableKeyword(useFakeEnvSpecularKeyword);
                    }
                }
            } else {
                material.DisableKeyword(useFakeEnvSpecularKeyword);
            }

            if (material.HasProperty("_DebugMode")) {
                if (material.GetFloat("_DebugMode") != 0.0f) {
                    material.EnableKeyword(debugMaterialKeyword);
                } else {
                    material.DisableKeyword(debugMaterialKeyword);
                }
            }

            // if (material.HasProperty("_Test")) {
            //     if (material.GetFloat("_Test") == 1.0f) {
            //         material.EnableKeyword("_TEST");
            //     } else {
            //         material.DisableKeyword("_TEST");
            //     }
            // }
        }

    }

}
