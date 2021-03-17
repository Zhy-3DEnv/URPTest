using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.XRenderPipeline {

    public static class CoreUtils {

        public const int editMenuPriority1 = 320;
        public const int editMenuPriority2 = 331;
        public const int editMenuPriority3 = 342;
        public const int assetCreateMenuPriority1 = 230;
        public const int assetCreateMenuPriority2 = 241;
        public const int assetCreateMenuPriority3 = 300;
        public const int gameObjectMenuPriority = 10;

        public static void ClearRenderTarget(CommandBuffer cmdbuf, ClearFlag clearFlag, Color clearColor) {
            if (clearFlag != ClearFlag.None) {
                cmdbuf.ClearRenderTarget((clearFlag & ClearFlag.Depth) != 0, (clearFlag & ClearFlag.Color) != 0, clearColor);
            }
        }

        public static void SetRenderTarget(CommandBuffer cmdbuf, RenderTargetIdentifier[] colorBuffers, RenderTargetIdentifier depthBuffer, ClearFlag clearFlag, Color clearColor) {
            cmdbuf.SetRenderTarget(colorBuffers, depthBuffer);
            ClearRenderTarget(cmdbuf, clearFlag, clearColor);
        }

        public static void SetRenderTarget(CommandBuffer cmdbuf, RenderTargetIdentifier buffer, RenderBufferLoadAction loadAction, RenderBufferStoreAction storeAction, ClearFlag clearFlag, Color clearColor) {
            cmdbuf.SetRenderTarget(buffer, loadAction, storeAction);
            ClearRenderTarget(cmdbuf, clearFlag, clearColor);
        }

        public static void SetRenderTarget(CommandBuffer cmdbuf, RenderTargetIdentifier colorBuffer, RenderBufferLoadAction colorLoadAction, RenderBufferStoreAction colorStoreAction, RenderTargetIdentifier depthBuffer, RenderBufferLoadAction depthLoadAction, RenderBufferStoreAction depthStoreAction, ClearFlag clearFlag, Color clearColor) {
            cmdbuf.SetRenderTarget(colorBuffer, colorLoadAction, colorStoreAction, depthBuffer, depthLoadAction, depthStoreAction);
            ClearRenderTarget(cmdbuf, clearFlag, clearColor);
        }

        public static Material CreateEngineMaterial(Shader shader) {
            if (shader == null) {
                UnityEngine.Debug.LogError("Cannot create material because shader is null");
            }
            var mat = new Material (shader) {
                hideFlags = HideFlags.HideAndDontSave
            };
            return mat;
        }

        public static Material CreateEngineMaterial(string shaderPath) {
            Shader shader = Shader.Find(shaderPath);
            return CreateEngineMaterial(shader);
        }

        static Mesh s_FullscreenMesh = null;
        public static Mesh FullscreenMesh {
            get {
                if (s_FullscreenMesh != null) {
                    return s_FullscreenMesh;
                }

                s_FullscreenMesh = new Mesh {
                    name = "Fullscreen Quad"
                };
                s_FullscreenMesh.SetVertices(new List<Vector3> {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f, 1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, 0.0f)
                });
                s_FullscreenMesh.SetUVs(0, new List<Vector2> {
                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),
                    new Vector2(1.0f, 1.0f)
                });
                s_FullscreenMesh.SetIndices(new int[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false );
                s_FullscreenMesh.UploadMeshData(true);
                return s_FullscreenMesh;
            }
        }

        static Material s_ErrorMaterial;
        static Material ErrorMaterial {
            get {
                if (s_ErrorMaterial == null) {
                    // TODO: When importing project, AssetPreviewUpdater::CreatePreviewForAsset will be called multiple times.
                    // This might be in a point that some resources required for the pipeline are not finished importing yet.
                    // Proper fix is to add a fence on asset import.
                    try {
                        s_ErrorMaterial = new Material(Shader.Find("Hidden/XRenderPipeline/FallbackError"));
                    } catch {}
                }

                return s_ErrorMaterial;
            }
        }

        static List<ShaderTagId> s_LegacyShaderPassNames = new List<ShaderTagId>() {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        internal static void RenderObjectsWithError(ScriptableRenderContext context, ref CullingResults cullResults, Camera camera, FilteringSettings filterSettings, SortingCriteria sortFlags) {
            // TODO: When importing project, AssetPreviewUpdater::CreatePreviewForAsset will be called multiple times.
            // This might be in a point that some resources required for the pipeline are not finished importing yet.
            // Proper fix is to add a fence on asset import.
            if (ErrorMaterial == null)
                return;

            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortFlags };
            DrawingSettings errorSettings = new DrawingSettings(s_LegacyShaderPassNames[0], sortingSettings) {
                perObjectData = PerObjectData.None,
                overrideMaterial = ErrorMaterial,
                overrideMaterialPassIndex = 0
            };
            for (int i = 1; i < s_LegacyShaderPassNames.Count; ++i)
                errorSettings.SetShaderPassName(i, s_LegacyShaderPassNames[i]);

            context.DrawRenderers(cullResults, ref errorSettings, ref filterSettings);
        }

        // Caches render texture format support. SystemInfo.SupportsRenderTextureFormat allocates memory due to boxing.
        static Dictionary<RenderTextureFormat, bool> s_RenderTextureFormatSupport = new Dictionary<RenderTextureFormat, bool>();

        internal static void ClearSystemInfoCache() {
            s_RenderTextureFormatSupport.Clear();
        }

        public static bool SupportsRenderTextureFormat(RenderTextureFormat format) {
            if (!s_RenderTextureFormatSupport.TryGetValue(format, out var support)) {
                support = SystemInfo.SupportsRenderTextureFormat(format);
                s_RenderTextureFormatSupport.Add(format, support);
            }

            return support;
        }

        public static RenderTextureDescriptor CreateRenderTextureDescriptor(Camera camera, float renderScale, bool isHdrEnabled, int msaaSamples) {
            RenderTextureDescriptor desc;
            RenderTextureFormat renderTextureFormatDefault = RenderTextureFormat.Default;
            desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            desc.width = (int)((float)desc.width * renderScale);
            desc.height = (int)((float)desc.height * renderScale);

            bool use32BitHDR = !Graphics.preserveFramebufferAlpha && SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float);
            RenderTextureFormat hdrFormat = (use32BitHDR) ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.DefaultHDR;
            if (camera.targetTexture != null) {
                if (camera.cameraType == CameraType.SceneView) {
                    desc.colorFormat = isHdrEnabled ? hdrFormat : renderTextureFormatDefault;
                } else {
                    desc.colorFormat = camera.targetTexture.descriptor.colorFormat;
                }
                desc.depthBufferBits = camera.targetTexture.descriptor.depthBufferBits;
                desc.msaaSamples = camera.targetTexture.descriptor.msaaSamples;
                desc.sRGB = camera.targetTexture.descriptor.sRGB;
            } else {
                desc.colorFormat = isHdrEnabled ? hdrFormat : renderTextureFormatDefault;
                desc.depthBufferBits = 32;
                desc.msaaSamples = msaaSamples;
                desc.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            }

            desc.enableRandomWrite = false;
            desc.bindMS = false;
            desc.useDynamicScale = camera.allowDynamicResolution;
            return desc;
        }

        public static void Destroy(UnityEngine.Object obj) {
            if (obj != null) {
#if UNITY_EDITOR
                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(obj);
                } else {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
#else
                UnityEngine.Object.Destroy(obj);
#endif
            }
        }

        static IEnumerable<Type> s_AssemblyTypes;
        public static IEnumerable<Type> GetAllAssemblyTypes() {
            if (s_AssemblyTypes == null) {
                s_AssemblyTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(t => {
                        // Ugly hack to handle mis-versioned dlls
                        var innerTypes = new Type[0];
                        try {
                            innerTypes = t.GetTypes();
                        }
                        catch {}
                        return innerTypes;
                    });
            }

            return s_AssemblyTypes;
        }

        public static IEnumerable<Type> GetAllTypesDerivedFrom<T>() {
#if UNITY_EDITOR && UNITY_2019_2_OR_NEWER
            return UnityEditor.TypeCache.GetTypesDerivedFrom<T>();
#else
            return GetAllAssemblyTypes().Where(t => t.IsSubclassOf(typeof(T)));
#endif
        }

        public static Vector4 CalculateFogParamsFromRenderSettings() {
            Vector4 fogParams = Vector4.zero;
            bool linear = (RenderSettings.fogMode == FogMode.Linear);
            float diff = linear ? (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance) : 0.0f;
            float invDiff = Mathf.Abs(diff) > 0.0001f ? 1.0f / diff : 0.0f;
            fogParams.Set(
                RenderSettings.fogDensity * 1.2011224087f, // density / sqrt(ln(2)), used by Exp2 fog mode
                RenderSettings.fogDensity * 1.4426950408f, // density / ln(2), used by Exp fog mode
                linear ? -invDiff : 0.0f,
                linear ? RenderSettings.fogEndDistance * invDiff : 0.0f
            );
            return fogParams;
        }

        public static bool IsPostProcessEnabled(Camera camera) {
            bool enabled = true;
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView) {
                enabled = false;
                // Determine whether the "Post Processes" checkbox is checked for the current view.
                for (int i = 0; i < UnityEditor.SceneView.sceneViews.Count; i++) {
                    var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                    // Post-processing is disabled in scene view if either showImageEffects is disabled or we are
                    // rendering in wireframe mode.
                    if (sv.camera == camera && (sv.sceneViewState.showImageEffects && sv.cameraMode.drawMode != UnityEditor.DrawCameraMode.Wireframe)) {
                        enabled = true;
                        break;
                    }
                }
            }
#endif
            return enabled;
        }

    }
}

