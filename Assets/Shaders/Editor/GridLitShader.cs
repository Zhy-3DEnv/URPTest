using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using SurfaceType = ShaderGUICommon.SurfaceType;
using CullMode = ShaderGUICommon.CullMode;
public class GridLitShader : ShaderGUI {

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

    enum DebugMode {
        Off = 0,
        BaseColor,
        Metallic,
        Roughness,
        AmbientOcclusion,
        Normal,
        IndirectDiffuse,
        IndirectSpecular,
        Count,
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
    const string useClusterLightingKeyword = "_USE_CLUSTER_LIGHTING";
    const string shadingQualityHighKeyword = "_SHADINGQUALITY_HIGH";
    const string shadingQualityMediumKeyword = "_SHADINGQUALITY_MEDIUM";
    const string shadingQualityLowKeyword = "_SHADINGQUALITY_LOW";
    const string envBRDFApproxV2Keyword = "_ENVBRDFAPPROX_V2";

    const int queueOffsetRange = 50;
    bool firstTimeApply = true;
    Vector2 tilingValue = new Vector2();

    static class Styles {
        public static readonly GUIContent surfaceType = new GUIContent("Surface Type", "Surface Type");
        public static readonly GUIContent blendMode = new GUIContent("Blend Mode", "Blend Mode");
        public static readonly GUIContent shadingQuality = new GUIContent("Shading Quality", "Shading Quality");
        public static readonly GUIContent envBRDFApprox = new GUIContent("EnvBRDF Approx Version", "EnvBRDF Approx Version");
        public static readonly GUIContent cullMode = new GUIContent("Cull Mode", "Cull Mode");
        public static readonly GUIContent alphaTest = new GUIContent("Alpha Test", "Alpha Test");
        public static readonly GUIContent worldPositionUV = new GUIContent("Enable World Position UV", "Enable World Position UV");
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
        public static readonly GUIContent tilingX = new GUIContent("Global Tiling", "Global Tiling");
        public static readonly GUIContent detailO1 = new GUIContent("Detail 01", "Overwite Base Color, Metallic & roughness by Detail 01");
        public static readonly GUIContent detailO2 = new GUIContent("Detail 02", "Overwite Base Color by Detail 02");
        public static readonly GUIContent detailO3 = new GUIContent("Detail 03", "Overwite Base Color by Detail 03");
        public static readonly GUIContent detail01Map = new GUIContent("Detail Map 01", "Detail Map 01");
        public static readonly GUIContent detail02Map = new GUIContent("Detail Map 02", "Detail Map 02");
        public static readonly GUIContent detail03Map = new GUIContent("Detail Map 03", "Detail Map 03");
    }


    public virtual void OnOpenGUI(Material material, MaterialEditor materialEditor) {
        foreach (var obj in  materialEditor.targets) {
                SetupMaterialBlendMode((Material)obj);
                SetupMaterialKeywords((Material)obj);
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
        var worldPositionUVProp = properties.First(x => x.name == "_WPOS");
        EditorGUI.showMixedValue = worldPositionUVProp.hasMixedValue;
        var worldPositionUVEnable = EditorGUILayout.Toggle(Styles.worldPositionUV, worldPositionUVProp.floatValue == 1);
        if (EditorGUI.EndChangeCheck()) {
            worldPositionUVProp.floatValue = worldPositionUVEnable ? 1 : 0;
            if(worldPositionUVEnable) {
                material.EnableKeyword("WPOS_ON");
            }else{
                material.DisableKeyword("WPOS_ON");
            }
        }
        EditorGUI.showMixedValue = false;
        
        var tilingXProp = properties.First(x => x.name == "_TilingX");
        materialEditor.ShaderProperty(tilingXProp, tilingXProp.displayName);
        var tilingYProp = properties.First(x => x.name == "_TilingY");
        materialEditor.ShaderProperty(tilingYProp, tilingYProp.displayName);

        EditorGUILayout.Space();
        var baseColorWhiteProp = properties.First(x => x.name == "_BaseColorWhite");
        materialEditor.ShaderProperty(baseColorWhiteProp, baseColorWhiteProp.displayName);
        var baseMetallicWhiteProp = properties.First(x => x.name == "_BaseMetallicWhite");
        materialEditor.ShaderProperty(baseMetallicWhiteProp, baseMetallicWhiteProp.displayName);
        var baseRoughnessWhiteProp = properties.First(x => x.name == "_BaseRoughnessWhite");
        materialEditor.ShaderProperty(baseRoughnessWhiteProp, baseRoughnessWhiteProp.displayName);     
        EditorGUILayout.Space();

        var baseColorBlackProp = properties.First(x => x.name == "_BaseColorBlack");
        materialEditor.ShaderProperty(baseColorBlackProp, baseColorBlackProp.displayName);
        var baseMetallicBlackProp = properties.First(x => x.name == "_BaseMetallicBlack");
        materialEditor.ShaderProperty(baseMetallicBlackProp, baseMetallicBlackProp.displayName);
        var baseRoughnessBlackProp = properties.First(x => x.name == "_BaseRoughnessBlack");
        materialEditor.ShaderProperty(baseRoughnessBlackProp, baseRoughnessBlackProp.displayName);     
        EditorGUILayout.Space();

        // surface inputs
        var baseMapProp = properties.First(x => x.name == "_BaseMap");
        materialEditor.TexturePropertySingleLine(Styles.baseMap, baseMapProp);
        materialEditor.TextureScaleOffsetProperty(baseMapProp);

        // Detail Setting
        using (new EditorGUILayout.VerticalScope("box")) {
            EditorGUI.BeginChangeCheck();
            var detail01Prop = properties.First(x => x.name == "_DETAIL_ON");
            EditorGUI.showMixedValue = detail01Prop.hasMixedValue;
            bool useDetail01 = EditorGUILayout.Toggle(Styles.detailO1, detail01Prop.floatValue == 1);
            if (EditorGUI.EndChangeCheck()) {
                detail01Prop.floatValue = useDetail01 ? 1 : 0;
            }
            EditorGUI.showMixedValue = false;
            if(useDetail01) {
                material.EnableKeyword("DETAIL_ON");
                var detailColorProp = properties.First(x => x.name == "_DetailColor");
                materialEditor.ShaderProperty(detailColorProp, detailColorProp.displayName);
                var detail01MetallicProp = properties.First(x => x.name == "_DetailMetallic");
                materialEditor.ShaderProperty(detail01MetallicProp, detail01MetallicProp.displayName);
                var detail01RoughnessProp = properties.First(x => x.name == "_DetailRoughness");
                materialEditor.ShaderProperty(detail01RoughnessProp, detail01RoughnessProp.displayName);
                var detail01MapProp = properties.First(x => x.name == "_DetailMap");
                materialEditor.TexturePropertySingleLine(Styles.detail01Map, detail01MapProp);
                materialEditor.TextureScaleOffsetProperty(detail01MapProp);
                EditorGUILayout.Space();
            }else{
                material.DisableKeyword("DETAIL_ON");
            }

            EditorGUI.BeginChangeCheck();
            var detail02Prop = properties.First(x => x.name == "_DETAIL2_ON");
            EditorGUI.showMixedValue = detail02Prop.hasMixedValue;
            bool useDetail02 = EditorGUILayout.Toggle(Styles.detailO2, detail02Prop.floatValue == 1);
            if (EditorGUI.EndChangeCheck()) {
                detail02Prop.floatValue = useDetail02 ? 1 : 0;
            }
            EditorGUI.showMixedValue = false;
            if(useDetail02) {
                material.EnableKeyword("DETAIL2_ON");
                var detail02ColorProp = properties.First(x => x.name == "_DetailColor2");
                materialEditor.ShaderProperty(detail02ColorProp, detail02ColorProp.displayName);
                var detail02MapProp = properties.First(x => x.name == "_DetailMap2");
                materialEditor.TexturePropertySingleLine(Styles.detail02Map, detail02MapProp);
                materialEditor.TextureScaleOffsetProperty(detail02MapProp);
                EditorGUILayout.Space();
            }else{
                material.DisableKeyword("DETAIL2_ON");
            }

            EditorGUI.BeginChangeCheck();
            var detail03Prop = properties.First(x => x.name == "_DETAIL3_ON");
            EditorGUI.showMixedValue = detail03Prop.hasMixedValue;
            bool useDetail03 = EditorGUILayout.Toggle(Styles.detailO3, detail03Prop.floatValue == 1);
            if (EditorGUI.EndChangeCheck()) {
                detail03Prop.floatValue = useDetail03 ? 1 : 0;
            }
            EditorGUI.showMixedValue = false;
            if(useDetail03) {
                material.EnableKeyword("DETAIL3_ON");
                var detail03ColorProp = properties.First(x => x.name == "_DetailColor3");
                materialEditor.ShaderProperty(detail03ColorProp, detail03ColorProp.displayName);
                var detail03MapProp = properties.First(x => x.name == "_DetailMap3");
                materialEditor.TexturePropertySingleLine(Styles.detail03Map, detail03MapProp);
                materialEditor.TextureScaleOffsetProperty(detail03MapProp);
                EditorGUILayout.Space();
            }else{
                material.DisableKeyword("DETAIL3_ON");
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
            if(useGlossyEnvReflection){
                material.EnableKeyword(useGlossyEnvReflectionKeyword);
            }else{
                material.DisableKeyword(useGlossyEnvReflectionKeyword);
            }
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
                if(useEnergyCompensation){
                    material.EnableKeyword(useEnergyCompensationKeyword);
                }else{
                    material.DisableKeyword(useEnergyCompensationKeyword);
                }
            }
            EditorGUI.showMixedValue = false;

            EditorGUI.BeginChangeCheck();
            var useSpecularAOProp = properties.First(x => x.name == "_UseSpecularAO");
            EditorGUI.showMixedValue = useSpecularAOProp.hasMixedValue;
            bool useSpecularAO = EditorGUILayout.Toggle("Use SpecularAO", useSpecularAOProp.floatValue == 1.0f);
            if (EditorGUI.EndChangeCheck()) {
                useSpecularAOProp.floatValue = useSpecularAO ? 1.0f : 0.0f;
                if(useSpecularAO){
                    material.EnableKeyword(useSpecularAOKeyword);
                }else{ 
                    material.DisableKeyword(useSpecularAOKeyword);
                }
            }
            EditorGUI.showMixedValue = false;
        }

        EditorGUI.BeginChangeCheck();
        if (material.GetFloat("_UseGlossyEnvReflection") == 0) {
            EditorGUI.BeginChangeCheck();
            var useFakeEnvSpecularProp = properties.First(x => x.name == "_UseFakeEnvSpecular");
            EditorGUI.showMixedValue = useFakeEnvSpecularProp.hasMixedValue;
            bool useFakeEnvSpecular = EditorGUILayout.Toggle("Use FakeEnvSpecular", useFakeEnvSpecularProp.floatValue == 1.0f);
            if (EditorGUI.EndChangeCheck()) {
                useFakeEnvSpecularProp.floatValue = useFakeEnvSpecular ? 1.0f : 0.0f;
                if(useFakeEnvSpecular){
                    material.EnableKeyword(useFakeEnvSpecularKeyword);
                }else{
                    material.DisableKeyword(useFakeEnvSpecularKeyword);
                }
            }
            EditorGUI.showMixedValue = false;
        }

        EditorGUI.BeginChangeCheck();
        var useClusterLightingFlagProp = properties.First(x => x.name == "_UseClusterLighting");
        EditorGUI.showMixedValue = useClusterLightingFlagProp.hasMixedValue;
        bool useClusterLightingFlag = EditorGUILayout.Toggle("Use Cluster Lighting", material.IsKeywordEnabled(useClusterLightingKeyword));
        if (EditorGUI.EndChangeCheck()) {
            useClusterLightingFlagProp.floatValue = useClusterLightingFlag ? 1.0f : 0.0f;
            if(useClusterLightingFlag){
                material.EnableKeyword(useClusterLightingKeyword);
            }else{
                material.DisableKeyword(useClusterLightingKeyword);
            }
        }
        EditorGUI.showMixedValue = false;

        EditorGUILayout.Space();

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

        if (EditorGUI.EndChangeCheck()) {
            foreach (var obj in materialEditor.targets) {
                SetupMaterialBlendMode((Material)obj);
            }
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

        if (material.HasProperty("_UseClusterLighting")) {
            if (material.GetFloat("_UseClusterLighting") == 1.0f) {
                material.EnableKeyword(useClusterLightingKeyword);
            } else {
                material.DisableKeyword(useClusterLightingKeyword);
            }
        }


    }

}