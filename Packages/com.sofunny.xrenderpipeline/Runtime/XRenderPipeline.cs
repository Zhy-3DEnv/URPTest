using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.GlobalIllumination;
// solve typename clash
using Lightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;
using RenderSettings = UnityEngine.RenderSettings;
using LightType = UnityEngine.LightType;

namespace Framework.XRenderPipeline {

    public class XRenderPipeline : RenderPipeline {
        public XRenderPipelineAsset pipelineSettings;
        public ForwardRenderer forwardRenderer;

        static class PerFramePropertyIDs {
            public static int glossyEnvironmentColor;
            public static int subtractiveShadowColor;
            // TODO: add more
        }

        public XRenderPipeline(XRenderPipelineAsset pipelineAsset) {
            SetSupportedRenderingFeatures();
            pipelineSettings = pipelineAsset;
            forwardRenderer = new ForwardRenderer(pipelineAsset);
            PerFramePropertyIDs.glossyEnvironmentColor = Shader.PropertyToID("_GlossyEnvironmentColor");
            PerFramePropertyIDs.subtractiveShadowColor = Shader.PropertyToID("_SubtractiveShadowColor");
            Shader.globalRenderPipeline = "XRP";
            Lightmapping.SetDelegate(lightsDelegate);
            CoreUtils.ClearSystemInfoCache();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Shader.globalRenderPipeline = "";
            Lightmapping.ResetDelegate();
            forwardRenderer.Dispose();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            BeginFrameRendering(context, cameras);
            // should set GraphicsSettings every frame?
            GraphicsSettings.lightsUseLinearIntensity = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            GraphicsSettings.useScriptableRenderPipelineBatching = pipelineSettings.useSRPBatcher;
            // setup per frame properties
            SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe;
            Color linearGlossyEnvColor = new Color(ambientSH[0, 0], ambientSH[1, 0], ambientSH[2, 0]) * RenderSettings.reflectionIntensity;
            Color glossyEnvColor = ColorUtils.ConvertLinearToActiveColorSpace(linearGlossyEnvColor);
            Shader.SetGlobalVector(PerFramePropertyIDs.glossyEnvironmentColor, glossyEnvColor);
            Shader.SetGlobalVector(PerFramePropertyIDs.subtractiveShadowColor, ColorUtils.ConvertSRGBToActiveColorSpace(RenderSettings.subtractiveShadowColor));
#if UNITY_EDITOR
            int gameCameraCnt = 0;
            foreach (var camera in cameras) {
                if (camera.cameraType == CameraType.Game) {
                    gameCameraCnt++;
                    if (gameCameraCnt > 1) {
                        Debug.LogError("Multiple game cameras found! Bad for performance!");
                    }
                }
            }
#endif
            foreach (var camera in cameras) {
                BeginCameraRendering(context, camera);
                UpdateVolumeFramework(camera);
                forwardRenderer.RenderCamera(context, camera);
                EndCameraRendering(context, camera);
            }

            EndFrameRendering(context, cameras);
        }

        static void UpdateVolumeFramework(Camera camera) {
            // Default values when there's no additional camera data available
            LayerMask layerMask = 1; // "Default"
            Transform trigger = camera.transform;

            VolumeManager.instance.Update(trigger, layerMask);
        }

        static Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] requests, NativeArray<LightDataGI> lightsOutput) => {
#if UNITY_EDITOR
            LightDataGI lightData = new LightDataGI();

            for (int i = 0; i < requests.Length; i++) {
                Light light = requests[i];
                switch (light.type) {
                    case LightType.Directional:
                        DirectionalLight directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        PointLight pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        SpotLight spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        RectangleLight rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked;
                        lightData.Init(ref rectangleLight);
                        break;
                    case LightType.Disc:
                        DiscLight discLight = new DiscLight();
                        LightmapperUtils.Extract(light, ref discLight);
                        discLight.mode = LightMode.Baked;
                        lightData.Init(ref discLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                lightsOutput[i] = lightData;
            }
#else
            LightDataGI lightData = new LightDataGI();

            for (int i = 0; i < requests.Length; i++)
            {
                Light light = requests[i];
                lightData.InitNoBake(light.GetInstanceID());
                lightsOutput[i] = lightData;
            }
            Debug.LogWarning("Realtime GI is not supported in Universal Pipeline.");
#endif
        };

        static void SetSupportedRenderingFeatures() {
#if UNITY_EDITOR
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures() {
                reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.None,
                defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.Subtractive,
                mixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.Subtractive | SupportedRenderingFeatures.LightmapMixedBakeModes.IndirectOnly,
                lightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed,
                lightmapsModes = LightmapsMode.CombinedDirectional | LightmapsMode.NonDirectional,
                lightProbeProxyVolumes = false,
                motionVectors = false,
                receiveShadows = false,
                reflectionProbes = true
            };
            SceneViewDrawMode.SetupDrawMode();
#endif
        }

        public static XRenderPipelineAsset PipelineAsset {
            get => GraphicsSettings.currentRenderPipeline as XRenderPipelineAsset;
        }

        public static float MinRenderScale {
            get => 0.1f;
        }

        public static float MaxRenderScale {
            get => 2.0f;
        }

        public static float MaxShadowBias {
            get => 10.0f;
        }
    }

}
