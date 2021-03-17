using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Framework.XRenderPipeline {

    public interface IPostProcess {
        bool IsActive();
        bool IsOnChipMemoryCompatible();
    }

    enum PostProcessProfileID {
        Bloom,
        UberPostProcess
    }

    public class PostProcessPass {
        const string k_RenderUberPost = "Render Uber PostProcess";
        readonly Material onChipCompatibleUberMaterial;
        readonly Material uberPostMaterial;
        readonly Material bloomMaterial;
        readonly XRenderPipelineAsset pipelineSettings;
        ColorLookup colorLookup;
        ColorAdjustments colorAdjustments;
        FilmGrain filmGrain;
        Tonemapping tonemapping;
        Bloom bloom;
        const int k_MaxBloomMipNum = 6;
        int bloomMipNum = 5;

        RenderTextureDescriptor rtDesc;
        RenderTargetHandle sourceRT;
        RenderTargetHandle destinationRT;
        RenderTargetHandle internalLutRT;

        readonly GraphicsFormat defaultHDRFormat;
        bool useRGBM;

        static class ShaderPropertyIDs {
            public static readonly int _Params             = Shader.PropertyToID("_Params");
            public static readonly int _Lut_Params         = Shader.PropertyToID("_Lut_Params");
            public static readonly int _UserLut_Params     = Shader.PropertyToID("_UserLut_Params");
            public static readonly int _InternalLut        = Shader.PropertyToID("_InternalLut");
            public static readonly int _UserLut            = Shader.PropertyToID("_UserLut");
            public static readonly int _Grain_Texture      = Shader.PropertyToID("_Grain_Texture");
            public static readonly int _Grain_Params       = Shader.PropertyToID("_Grain_Params");
            public static readonly int _Grain_TilingParams = Shader.PropertyToID("_Grain_TilingParams");
            public static readonly int _FullscreenProjMat  = Shader.PropertyToID("_FullscreenProjMat");

            public static readonly int _BloomInit          = Shader.PropertyToID("_BloomInit");
            public static readonly int _BloomInitBlurred   = Shader.PropertyToID("_BloomInitBlurred");
            public static readonly int _BloomFinal         = Shader.PropertyToID("_BloomFinal");
            public static readonly int _BloomLowMip        = Shader.PropertyToID("_BloomLowMip");
            public static readonly int _BloomParams        = Shader.PropertyToID("_Bloom_Params");
            public static readonly int _BloomTexture       = Shader.PropertyToID("_Bloom_Texture");
            public static int[] _BloomMipUp;
            public static int[] _BloomMipDown;
        }

        public PostProcessPass(XRenderPipelineAsset pipelineAsset) {
            onChipCompatibleUberMaterial = CoreUtils.CreateEngineMaterial(pipelineAsset.shaders.onChipCompatibleUberPostProcess);
            uberPostMaterial = CoreUtils.CreateEngineMaterial(pipelineAsset.shaders.uberPostProcess);
            bloomMaterial = CoreUtils.CreateEngineMaterial(pipelineAsset.shaders.bloom);
            pipelineSettings = pipelineAsset;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render)) {
                defaultHDRFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                useRGBM = false;
            } else {
                defaultHDRFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_SNorm;
                useRGBM = true;
            }

            ShaderPropertyIDs._BloomMipUp = new int[k_MaxBloomMipNum];
            ShaderPropertyIDs._BloomMipDown = new int[k_MaxBloomMipNum];
            for (int i = 0; i < k_MaxBloomMipNum; ++i) {
                ShaderPropertyIDs._BloomMipUp[i]   = Shader.PropertyToID("_BloomMipUp" + i);
                ShaderPropertyIDs._BloomMipDown[i] = Shader.PropertyToID("_BloomMipDown" + i);
            }
        }

        public void Setup(RenderTextureDescriptor desc, RenderTargetHandle source, RenderTargetHandle destination, RenderTargetHandle internalLut) {
            rtDesc = desc;
            sourceRT = source;
            destinationRT = destination;
            internalLutRT = internalLut;
        }

        public void Cleanup() {
            CoreUtils.Destroy(onChipCompatibleUberMaterial);
            CoreUtils.Destroy(uberPostMaterial);
        }

        public void RenderPostProcess(ScriptableRenderContext context, ref RenderingData renderingData) {
            var cmdbuf = CommandBufferPool.Get(k_RenderUberPost);
            var stack = VolumeManager.instance.stack;
            colorLookup = stack.GetComponent<ColorLookup>();
            colorAdjustments = stack.GetComponent<ColorAdjustments>();
            filmGrain = stack.GetComponent<FilmGrain>();
            tonemapping = stack.GetComponent<Tonemapping>();
            bloom = stack.GetComponent<Bloom>();
            bool onChipUberPost = true;
            if (bloom.IsActive()) {
                onChipUberPost = false;
            }
            Material uberMaterial;
            if (onChipUberPost) {
                uberMaterial = onChipCompatibleUberMaterial;
            } else {
                uberMaterial = uberPostMaterial;
            }
            uberMaterial.shaderKeywords = null;
            bool bloomActive = bloom.IsActive();
            if (bloomActive) {
                using (new ProfilingScope(cmdbuf, ProfilingSampler.Get(PostProcessProfileID.Bloom))) {
                    SetupBloom(cmdbuf);
                }
            }
            SetupColorGrading(cmdbuf, ref renderingData, uberMaterial);
            var camera = renderingData.cameraData.camera;
            SetupFilmGrain(camera.pixelWidth, camera.pixelHeight, uberMaterial);
            bool requiresSRGBConversion = Display.main.requiresSrgbBlitToBackbuffer;
            if (requiresSRGBConversion) {
                uberMaterial.EnableKeyword(ShaderKeywords.LinearToSRGBConversion);
            }
            cmdbuf.SetGlobalTexture("_BlitTex", sourceRT.Identifier());
            ref var cameraData = ref renderingData.cameraData;
            if (cameraData.isSceneViewCamera) {
                cmdbuf.SetRenderTarget(destinationRT.Identifier(),
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, // color
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
                cmdbuf.Blit(sourceRT.Identifier(), BuiltinRenderTextureType.CameraTarget, uberMaterial);
            } else {
                cmdbuf.SetRenderTarget(destinationRT.Identifier(), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmdbuf.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmdbuf.SetViewport(camera.pixelRect);
                cmdbuf.DrawMesh(CoreUtils.FullscreenMesh, Matrix4x4.identity, uberMaterial);
            }
            // clean up
            if (bloomActive) {
                cmdbuf.ReleaseTemporaryRT(ShaderPropertyIDs._BloomFinal);
            }
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

#region Color Grading
        void SetupColorGrading(CommandBuffer cmdbuf, ref RenderingData renderingData, Material uberMaterial) {
            ref var postProcessData = ref renderingData.postProcessData;
            bool hdr = postProcessData.colorGradingMode == ColorGradingMode.HighDynamicRange;
            int lutHeight = postProcessData.colorGradingLutSize;
            int lutWidth = lutHeight * lutHeight;

            // Source material setup
            float postExposureLinear = Mathf.Pow(2f, colorAdjustments.postExposure.value);
            cmdbuf.SetGlobalTexture(ShaderPropertyIDs._InternalLut, internalLutRT.Identifier());
            uberMaterial.SetVector(ShaderPropertyIDs._Lut_Params, new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f, postExposureLinear));
            uberMaterial.SetTexture(ShaderPropertyIDs._UserLut, colorLookup.texture.value);
            uberMaterial.SetVector(ShaderPropertyIDs._UserLut_Params, !colorLookup.IsActive()
                ? Vector4.zero
                : new Vector4(1f / colorLookup.texture.value.width,
                              1f / colorLookup.texture.value.height,
                              colorLookup.texture.value.height - 1f,
                              colorLookup.contribution.value)
            );

            if (hdr) {
                uberMaterial.EnableKeyword(ShaderKeywords.HDRGrading);
            } else {
                switch (tonemapping.mode.value) {
                    case TonemappingMode.Neutral:
                        uberMaterial.EnableKeyword(ShaderKeywords.TonemappingNeutral);
                        break;
                    case TonemappingMode.ACES:
                        uberMaterial.EnableKeyword(ShaderKeywords.TonemappingACES);
                        break;
                    case TonemappingMode.Uchimura:
                        uberMaterial.EnableKeyword(ShaderKeywords.TonemappingUchimura);
                        break;
                    default:
                        break; // None
                }
            }
        }
#endregion

#region Film Grain
        void SetupFilmGrain(int pixelWidth, int pixelHeight, Material uberMaterial) {
            if (filmGrain.IsActive()) {
                uberMaterial.EnableKeyword(ShaderKeywords.FilmGrain);
                var texture = filmGrain.texture.value;
                if (filmGrain.type.value != FilmGrainLookup.Custom) {
                    texture = pipelineSettings.textures.filmGrainTex[(int)filmGrain.type.value];
                }
                float offsetX = Random.value;
                float offsetY = Random.value;

                var tilingParams = texture == null
                    ? Vector4.zero
                    : new Vector4(pixelWidth / (float)texture.width, pixelHeight / (float)texture.height, offsetX, offsetY);

                uberMaterial.SetTexture(ShaderPropertyIDs._Grain_Texture, texture);
                uberMaterial.SetVector(ShaderPropertyIDs._Grain_Params, new Vector2(filmGrain.intensity.value * 4f, filmGrain.response.value));
                uberMaterial.SetVector(ShaderPropertyIDs._Grain_TilingParams, tilingParams);
            }
        }
#endregion

#region Bloom
        void SetupBloom(CommandBuffer cmdbuf) {
            int tw = rtDesc.width >> 2;
            int th = rtDesc.height >> 2;
            float threshold = Mathf.GammaToLinearSpace(bloom.threshold.value);
            float thresholdKnee = threshold * 0.5f;
            float scatter = Mathf.Lerp(0.05f, 0.95f, bloom.scatter.value);
            bloomMaterial.SetVector(ShaderPropertyIDs._Params, new Vector4(threshold, thresholdKnee, scatter, bloom.clamp.value));
            if (useRGBM) {
                uberPostMaterial.EnableKeyword(ShaderKeywords.UseRGBM);
            } else {
                uberPostMaterial.DisableKeyword(ShaderKeywords.UseRGBM);
            }
            RenderTextureDescriptor desc = rtDesc;
            desc.width = tw;
            desc.height = th;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.graphicsFormat = defaultHDRFormat;

            bool applyPrefilterBlur = bloom.prefilterBlur.value;
            // prefiltering, extract bright pixels
            cmdbuf.GetTemporaryRT(ShaderPropertyIDs._BloomInit, desc, FilterMode.Bilinear);
            if (applyPrefilterBlur) {
                cmdbuf.GetTemporaryRT(ShaderPropertyIDs._BloomInitBlurred, desc, FilterMode.Bilinear);
            }
            cmdbuf.GetTemporaryRT(ShaderPropertyIDs._BloomFinal, desc, FilterMode.Bilinear);
            cmdbuf.Blit(sourceRT.Identifier(), ShaderPropertyIDs._BloomInit, bloomMaterial, 0);

            // apply lightweight blur before downsampling to eliminate bloom flickering
            if (applyPrefilterBlur) {
                cmdbuf.Blit(ShaderPropertyIDs._BloomInit, ShaderPropertyIDs._BloomInitBlurred, bloomMaterial, 6);
            }

            // downsampling blur
            int lastDown = ShaderPropertyIDs._BloomInit;
            if (applyPrefilterBlur) {
                lastDown = ShaderPropertyIDs._BloomInitBlurred;
            }
            bloomMipNum = Mathf.Min(bloom.bloomMipCount.value, k_MaxBloomMipNum);
            for (int i = 0; i < bloomMipNum; ++i) {
                // NOTE: applying blur at BloomInit resolution is expensive
                tw = Mathf.Max(1, tw >> 1);
                th = Mathf.Max(1, th >> 1);
                int mipUp = ShaderPropertyIDs._BloomMipUp[i];
                int mipDown = ShaderPropertyIDs._BloomMipDown[i];
                desc.width = tw;
                desc.height = th;
                cmdbuf.GetTemporaryRT(mipUp, desc, FilterMode.Bilinear);
                cmdbuf.GetTemporaryRT(mipDown, desc, FilterMode.Bilinear);
                cmdbuf.Blit(lastDown, mipUp, bloomMaterial, 2); // downsampling blur horizontal
                cmdbuf.Blit(mipUp, mipDown, bloomMaterial, 3); // blur vertical

                lastDown = mipDown;
            }

            // upsampling
            if (bloom.highQualityUpsampling.value) {
                bloomMaterial.EnableKeyword(ShaderKeywords.UseUpsampleBlur);
            } else {
                bloomMaterial.DisableKeyword(ShaderKeywords.UseUpsampleBlur);
            }
            cmdbuf.SetGlobalTexture(ShaderPropertyIDs._BloomLowMip, ShaderPropertyIDs._BloomMipDown[bloomMipNum - 1]);
            cmdbuf.Blit(ShaderPropertyIDs._BloomMipDown[bloomMipNum - 2], BlitDstDiscardContent(cmdbuf, ShaderPropertyIDs._BloomMipUp[bloomMipNum - 2]), bloomMaterial, 4);
            for (int i = bloomMipNum - 3; i >= 0; --i) {
                cmdbuf.SetGlobalTexture(ShaderPropertyIDs._BloomLowMip, ShaderPropertyIDs._BloomMipUp[i + 1]);
                cmdbuf.Blit(ShaderPropertyIDs._BloomMipDown[i], BlitDstDiscardContent(cmdbuf, ShaderPropertyIDs._BloomMipUp[i]), bloomMaterial, 4);
            }
            cmdbuf.Blit(ShaderPropertyIDs._BloomMipUp[0], BlitDstDiscardContent(cmdbuf, ShaderPropertyIDs._BloomFinal), bloomMaterial, 5);

            // clean up
            cmdbuf.ReleaseTemporaryRT(ShaderPropertyIDs._BloomInit);
            if (applyPrefilterBlur) {
                cmdbuf.ReleaseTemporaryRT(ShaderPropertyIDs._BloomInitBlurred);
            }
            for (int i = 0; i < bloomMipNum; ++i) {
                cmdbuf.ReleaseTemporaryRT(ShaderPropertyIDs._BloomMipUp[i]);
                cmdbuf.ReleaseTemporaryRT(ShaderPropertyIDs._BloomMipDown[i]);
            }

            Color tint = bloom.tint.value.linear;
            float luma = ColorUtils.Luminance(tint);
            tint = luma > 0f ? tint * (1f / luma) : Color.white;
            var bloomParams = new Vector4(bloom.intensity.value, tint.r, tint.g, tint.b);
            uberPostMaterial.SetVector(ShaderPropertyIDs._BloomParams, bloomParams);
            if (useRGBM) {
                uberPostMaterial.EnableKeyword(ShaderKeywords.UseRGBM);
            } else {
                uberPostMaterial.DisableKeyword(ShaderKeywords.UseRGBM);
            }
            uberPostMaterial.EnableKeyword(ShaderKeywords.Bloom);
            cmdbuf.SetGlobalTexture(ShaderPropertyIDs._BloomTexture, ShaderPropertyIDs._BloomFinal);
        }
#endregion

        private BuiltinRenderTextureType BlitDstDiscardContent(CommandBuffer cmdbuf, RenderTargetIdentifier rt) {
            // We set depth to DontCare because rt might be the source of PostProcessing used as a temporary target
            // Source typically comes with a depth buffer and right now we don't have a way to only bind the color attachment of a RenderTargetIdentifier
            cmdbuf.SetRenderTarget(new RenderTargetIdentifier(rt, 0, CubemapFace.Unknown, -1),
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            return BuiltinRenderTextureType.CurrentActive;
        }
    }

}
