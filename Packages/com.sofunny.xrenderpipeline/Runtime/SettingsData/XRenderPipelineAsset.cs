using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

namespace Framework.XRenderPipeline {
    [Serializable, ReloadGroup]
    public class ShaderResources {
        [Reload("Shaders/Blit.shader")]
        public Shader blit;
        [Reload("Shaders/CopyColor.shader")]
        public Shader copyColor;
        [Reload("Shaders/CopyDepth.shader")]
        public Shader copyDepth;
        [Reload("Shaders/LutBuilderLdr.shader")]
        public Shader lutBuilderLdr;
        [Reload("Shaders/LutBuilderHdr.shader")]
        public Shader lutBuilderHdr;
        [Reload("Shaders/OnChipCompatibleUberPostProcess.shader")]
        public Shader onChipCompatibleUberPostProcess;
        [Reload("Shaders/UberPostProcess.shader")]
        public Shader uberPostProcess;
        [Reload("Shaders/Bloom.shader")]
        public Shader bloom;
        [Reload("Shaders/Cluster.compute")]
        public ComputeShader cluster;
        [Reload("Shaders/LightCulling.compute")]
        public ComputeShader lightCulling;
        [Reload("Shaders/FallbackError.shader")]
        public Shader fallbackError;
    }

    [Serializable, ReloadGroup]
    public class MaterialResources {
        [Reload("Materials/Lit.mat")]
        public Material lit;
        [Reload("Materials/ParticleUnlit.mat")]
        public Material particleUnlit;
        [Reload("Materials/UIDefault.mat")]
        public Material uiDefault;
        [Reload("Materials/DebugWireframe.mat")]
        public Material wireframe;
    }

    [Serializable, ReloadGroup]
    public class TextureResources {
        // Pre-baked noise
        [Reload("Textures/BlueNoise16/L/LDR_LLL1_{0}.png", 0, 32)]
        public Texture2D[] blueNoise16LTex;

        // Post-processing
        [Reload(new[] {
            "Textures/FilmGrain/Thin01.png",
            "Textures/FilmGrain/Thin02.png",
            "Textures/FilmGrain/Medium01.png",
            "Textures/FilmGrain/Medium02.png",
            "Textures/FilmGrain/Medium03.png",
            "Textures/FilmGrain/Medium04.png",
            "Textures/FilmGrain/Medium05.png",
            "Textures/FilmGrain/Medium06.png",
            "Textures/FilmGrain/Large01.png",
            "Textures/FilmGrain/Large02.png"
        })]
        public Texture2D[] filmGrainTex;
    }

    public enum MSAAQuality {
        Disabled = 1,
        MSAA2x = 2,
        MSAA4x = 4,
        MSAA8x = 8
    }

    public enum ShadowmapResolution {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    public enum ShadowCascadesOption {
        NoCascades,
        TwoCascades,
        FourCascades,
    }

    public enum Downsampling {
        None,
        _2xBilinear,
        _4xBox,
        _4xBilinear
    }

    public enum ColorGradingMode {
        // This mode follows a more classic workflow.
        // apply a limited range of color grading after tonemapping.
        LowDynamicRange,
        // This mode works best for high precision grading similar to movie production workflow.
        // apply color grading before tonemapping.
        HighDynamicRange
    }

    [Serializable]
    public class OverrideCameraSettings {
        public bool enabled = false;
        public LayerMask opaqueLayerMask = 0;
        public LayerMask transparentLayerMask = 0;
        public Vector3 positionOffset = Vector3.zero;
        public float fieldOfView = 60f;
        public float nearClipPlane = 0.01f;
        public float farClipPlane = 50f;
    }

    // keep in sync with Debug.hlsl
    public enum MaterialDebugMode : int {
        None                 = 0,
        BaseColor            = 1,
        Metallic             = 2,
        Roughness            = 3,
        AmbientOcclusion     = 4,
        Normal               = 5,
        ValidatePBRDiffuse   = 6,
        ValidatePBRSpecular  = 7,
    }

    // keep in sync with Debug.hlsl
    public enum LightingDebugMode : int {
        None = 0,
        DirectSpecular = (1 << 0),
        IndirectSpecular = (1 << 1),
        DirectDiffuse = (1 << 2),
        IndirectDiffuse = (1 << 3),
        All = (DirectSpecular | IndirectSpecular | DirectDiffuse | IndirectDiffuse),
    }

    public enum WireframeMode {
        None = 0,
        WireframeOnly,
        ShadedWireframe,
        SolidColorWireframe
    }

    public class XRenderPipelineAsset : RenderPipelineAsset {
        // general settings
        public LayerMask opaqueLayerMask = -1;
        public LayerMask transparentLayerMask = -1;
        public bool requireOpaqueTexture = false;
        public bool requireDepthTexture = false;
        public Downsampling opaqueDownsampling = Downsampling._2xBilinear;
        // quality settings
        public bool useHDR = false;
        public MSAAQuality msaaQuality = MSAAQuality.Disabled;
        public float renderScale = 1;
        // shadow settings
        public bool supportMainLightShadow = true;
        public bool supportSoftShadow = true;
        public float shadowDistance = 50.0f;
        public ShadowmapResolution shadowmapResolution = ShadowmapResolution._1024;
        public float shadowDepthBias = 1.0f;
        public float shadowNormalBias = 1.0f;
        public ShadowCascadesOption shadowCascadesOption;
        public float cascades2Split = 0.25f;
        public Vector3 cascades4Split = new Vector3(0.067f, 0.2f, 0.467f);
        // post-process settings
        public bool usePostProcess = false;
        public ColorGradingMode colorGradingMode = ColorGradingMode.LowDynamicRange;
        public int colorGradingLutSize = 32;
        // advanced settings
        public bool useSRPBatcher = true;
        public bool useDynamicBatching = false;
        public bool supportMixedLighting = true;
        public bool disableAdditionalLights = false;
        public bool useClusterLighting = false;
        public OverrideCameraSettings overrideCameraSettings;
        // debug settings
        public bool enablePipelineDebug = false;
        public MaterialDebugMode materialDebugMode = MaterialDebugMode.None;
        public WireframeMode wireframeMode = WireframeMode.None;
        public Color wireframeColor = Color.black;
        public Color wireframeOverrideColor = Color.gray;
        public bool validateMaterialPureMetal = false;
        public Color validateMaterialPureMetalColor = Color.magenta;
        public Color validateMaterialHighColor = Color.red;
        public Color validateMaterialLowColor = Color.cyan;
        public bool showDirectSpecular = true;
        public bool showIndirectSpecular = true;
        public bool showDirectDiffuse = true;
        public bool showIndirectDiffuse = true;
        // hidden settings
        public ShaderResources shaders = null;
        public MaterialResources materials = null;
        public TextureResources textures = null;
        public XRenderPipeline renderPipeline = null;
#if UNITY_EDITOR
        public static readonly string packagePath = "Packages/com.sofunny.xrenderpipeline";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateXRenderPipelineAction : EndNameEditAction {
            public override void Action(int instanceId, string pathName, string resourceFile) {
                var instance = CreateInstance<XRenderPipelineAsset>();
                AssetDatabase.CreateAsset(instance, pathName);
                ResourceReloader.ReloadAllNullIn(instance, packagePath);
                Selection.activeObject = instance;
            }
        }

        [MenuItem("Assets/Create/Rendering/XRenderPipeline/XRenderPipelineAsset", priority = CoreUtils.assetCreateMenuPriority1)]
        static void CreateXRenderPipelineAsset() {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateXRenderPipelineAction>(), "XRenderPipelineAsset.asset", null, null);
        }
#endif

        protected override RenderPipeline CreatePipeline() {
            renderPipeline =  new XRenderPipeline(this);
            return renderPipeline;
        }

        Shader _defaultShader;
        public override Shader defaultShader {
            get {
                if (_defaultShader == null)
                    _defaultShader = Shader.Find(ShaderUtils.GetShaderPath(ShaderPathID.Lit));

                return _defaultShader;
            }
        }

        public override Material defaultMaterial {
            get {
                return materials.lit;
            }
        }

        public override Material defaultLineMaterial {
            get {
                return materials.particleUnlit;
            }
        }

        public override Material defaultParticleMaterial {
            get {
                return materials.particleUnlit;
            }
        }

        public override Material defaultUIMaterial {
            get {
                return materials.uiDefault;
            }
        }

        public override Material defaultUIOverdrawMaterial {
            get {
                return materials.uiDefault;
            }
        }

        public override Material defaultUIETC1SupportedMaterial {
            get {
                return materials.uiDefault;
            }
        }
    }

}
