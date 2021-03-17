using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Framework.XRenderPipeline {

    public class ColorGradingLutPass {
        const string k_RenderColorGradingLutTag = "Color Grading LUT";
        readonly Material lutBuilderLdrMaterial;
        readonly Material lutBuilderHdrMaterial;
        readonly GraphicsFormat ldrLutFormat;
        readonly GraphicsFormat hdrLutFormat;

        public RenderTargetHandle internalLutRT;

        static class ShaderPropertyIDs {
            public static readonly int _Lut_Params        = Shader.PropertyToID("_Lut_Params");
            public static readonly int _ColorBalance      = Shader.PropertyToID("_ColorBalance");
            public static readonly int _ColorFilter       = Shader.PropertyToID("_ColorFilter");
            public static readonly int _ChannelMixerRed   = Shader.PropertyToID("_ChannelMixerRed");
            public static readonly int _ChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
            public static readonly int _ChannelMixerBlue  = Shader.PropertyToID("_ChannelMixerBlue");
            public static readonly int _HueSatCon         = Shader.PropertyToID("_HueSatCon");
            public static readonly int _Lift              = Shader.PropertyToID("_Lift");
            public static readonly int _Gamma             = Shader.PropertyToID("_Gamma");
            public static readonly int _Gain              = Shader.PropertyToID("_Gain");
            public static readonly int _Shadows           = Shader.PropertyToID("_Shadows");
            public static readonly int _Midtones          = Shader.PropertyToID("_Midtones");
            public static readonly int _Highlights        = Shader.PropertyToID("_Highlights");
            public static readonly int _ShaHiLimits       = Shader.PropertyToID("_ShaHiLimits");
            public static readonly int _SplitShadows      = Shader.PropertyToID("_SplitShadows");
            public static readonly int _SplitHighlights   = Shader.PropertyToID("_SplitHighlights");
            public static readonly int _CurveMaster       = Shader.PropertyToID("_CurveMaster");
            public static readonly int _CurveRed          = Shader.PropertyToID("_CurveRed");
            public static readonly int _CurveGreen        = Shader.PropertyToID("_CurveGreen");
            public static readonly int _CurveBlue         = Shader.PropertyToID("_CurveBlue");
            public static readonly int _CurveHueVsHue     = Shader.PropertyToID("_CurveHueVsHue");
            public static readonly int _CurveHueVsSat     = Shader.PropertyToID("_CurveHueVsSat");
            public static readonly int _CurveLumVsSat     = Shader.PropertyToID("_CurveLumVsSat");
            public static readonly int _CurveSatVsSat     = Shader.PropertyToID("_CurveSatVsSat");
        }

        public ColorGradingLutPass(Shader lutBuilderLdrShader, Shader lutBuilderHdrShader) {
            internalLutRT.Init("_InternalGradingLut");
            lutBuilderLdrMaterial = CoreUtils.CreateEngineMaterial(lutBuilderLdrShader);
            lutBuilderHdrMaterial = CoreUtils.CreateEngineMaterial(lutBuilderHdrShader);

            const FormatUsage kFlags = FormatUsage.Linear | FormatUsage.Render;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, kFlags)) {
                hdrLutFormat = GraphicsFormat.R16G16B16A16_SFloat;
            } else if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, kFlags)) {
                hdrLutFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            } else {
                // Obviously using this for log lut encoding is a very bad idea for precision but we
                // need it for compatibility reasons and avoid black screens on platforms that don't
                // support floating point formats. Expect banding and posterization artifact if this
                // ends up being used.
                hdrLutFormat = GraphicsFormat.R8G8B8A8_UNorm;
            }
            ldrLutFormat = GraphicsFormat.R8G8B8A8_UNorm;
        }

        public void RenderColorGradingLut(ScriptableRenderContext context, ColorGradingMode colorGradingMode, int lutSize) {
            var stack = VolumeManager.instance.stack;
            var channelMixer = stack.GetComponent<ChannelMixer>();
            var colorAdjustments = stack.GetComponent<ColorAdjustments>();
            var colorCurves = stack.GetComponent<ColorCurves>();
            var liftGammaGain = stack.GetComponent<LiftGammaGain>();
            var shadowsMidtonesHighlights = stack.GetComponent<ShadowsMidtonesHighlights>();
            var splitToning = stack.GetComponent<SplitToning>();
            var tonemapping = stack.GetComponent<Tonemapping>();
            var whiteBalance = stack.GetComponent<WhiteBalance>();

            bool isHdrMode = colorGradingMode == ColorGradingMode.HighDynamicRange;
            int lutHeight = lutSize;
            int lutWidth = lutHeight * lutHeight;
            var lutFormat = isHdrMode ? hdrLutFormat : ldrLutFormat;
            var lutMaterial = isHdrMode ? lutBuilderHdrMaterial : lutBuilderLdrMaterial;
            var lutRTDesc = new RenderTextureDescriptor(lutWidth, lutHeight, lutFormat, 0);
            // Prepare data
            var lmsColorBalance = ColorBalanceToLMSCoeffs(whiteBalance.temperature.value, whiteBalance.tint.value);
            var hueSatCon = new Vector4(colorAdjustments.hueShift.value / 360f, colorAdjustments.saturation.value / 100f + 1f, colorAdjustments.contrast.value / 100f + 1f, 0f);
            var channelMixerR = new Vector4(channelMixer.redOutRedIn.value / 100f, channelMixer.redOutGreenIn.value / 100f, channelMixer.redOutBlueIn.value / 100f, 0f);
            var channelMixerG = new Vector4(channelMixer.greenOutRedIn.value / 100f, channelMixer.greenOutGreenIn.value / 100f, channelMixer.greenOutBlueIn.value / 100f, 0f);
            var channelMixerB = new Vector4(channelMixer.blueOutRedIn.value / 100f, channelMixer.blueOutGreenIn.value / 100f, channelMixer.blueOutBlueIn.value / 100f, 0f);

            var shadowsHighlightsLimits = new Vector4(
                shadowsMidtonesHighlights.shadowsStart.value,
                shadowsMidtonesHighlights.shadowsEnd.value,
                shadowsMidtonesHighlights.highlightsStart.value,
                shadowsMidtonesHighlights.highlightsEnd.value
            );

            var (shadows, midtones, highlights) = PrepareShadowsMidtonesHighlights(
                shadowsMidtonesHighlights.shadows.value,
                shadowsMidtonesHighlights.midtones.value,
                shadowsMidtonesHighlights.highlights.value
            );

            var (lift, gamma, gain) = PrepareLiftGammaGain(
                liftGammaGain.lift.value,
                liftGammaGain.gamma.value,
                liftGammaGain.gain.value
            );

            var (splitShadows, splitHighlights) = PrepareSplitToning(
                splitToning.shadows.value,
                splitToning.highlights.value,
                splitToning.balance.value
            );

            var lutParameters = new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f));
            // Fill in constants
            lutMaterial.SetVector(ShaderPropertyIDs._Lut_Params, lutParameters);
            lutMaterial.SetVector(ShaderPropertyIDs._ColorBalance, lmsColorBalance);
            lutMaterial.SetVector(ShaderPropertyIDs._ColorFilter, colorAdjustments.colorFilter.value.linear);
            lutMaterial.SetVector(ShaderPropertyIDs._ChannelMixerRed, channelMixerR);
            lutMaterial.SetVector(ShaderPropertyIDs._ChannelMixerGreen, channelMixerG);
            lutMaterial.SetVector(ShaderPropertyIDs._ChannelMixerBlue, channelMixerB);
            lutMaterial.SetVector(ShaderPropertyIDs._HueSatCon, hueSatCon);
            lutMaterial.SetVector(ShaderPropertyIDs._Lift, lift);
            lutMaterial.SetVector(ShaderPropertyIDs._Gamma, gamma);
            lutMaterial.SetVector(ShaderPropertyIDs._Gain, gain);
            lutMaterial.SetVector(ShaderPropertyIDs._Shadows, shadows);
            lutMaterial.SetVector(ShaderPropertyIDs._Midtones, midtones);
            lutMaterial.SetVector(ShaderPropertyIDs._Highlights, highlights);
            lutMaterial.SetVector(ShaderPropertyIDs._ShaHiLimits, shadowsHighlightsLimits);
            lutMaterial.SetVector(ShaderPropertyIDs._SplitShadows, splitShadows);
            lutMaterial.SetVector(ShaderPropertyIDs._SplitHighlights, splitHighlights);

            // YRGB curves
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveMaster, colorCurves.master.value.GetTexture());
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveRed, colorCurves.red.value.GetTexture());
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveGreen, colorCurves.green.value.GetTexture());
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveBlue, colorCurves.blue.value.GetTexture());

            // Secondary curves
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveHueVsHue, colorCurves.hueVsHue.value.GetTexture());
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveHueVsSat, colorCurves.hueVsSat.value.GetTexture());
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveLumVsSat, colorCurves.lumVsSat.value.GetTexture());
            lutMaterial.SetTexture(ShaderPropertyIDs._CurveSatVsSat, colorCurves.satVsSat.value.GetTexture());

            // Tonemapping (baked into the lut for HDR)
            if (isHdrMode) {
                lutMaterial.shaderKeywords = null;
                switch (tonemapping.mode.value) {
                    case TonemappingMode.Neutral:
                        lutMaterial.EnableKeyword(ShaderKeywords.TonemappingNeutral);
                        break;
                    case TonemappingMode.ACES:
                        lutMaterial.EnableKeyword(ShaderKeywords.TonemappingACES);
                        break;
                    case TonemappingMode.Uchimura:
                        lutMaterial.EnableKeyword(ShaderKeywords.TonemappingUchimura);
                        break;
                    default:
                        break; // None
                }
            }
            var cmdbuf = CommandBufferPool.Get(k_RenderColorGradingLutTag);
            cmdbuf.GetTemporaryRT(internalLutRT.id, lutRTDesc, FilterMode.Bilinear);
            cmdbuf.SetRenderTarget(internalLutRT.Identifier(), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmdbuf.Blit(internalLutRT.Identifier(), internalLutRT.Identifier(), lutMaterial, 0);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        public void FrameCleanup(ScriptableRenderContext context) {
            var cmdbuf = CommandBufferPool.Get("Release lutRT");
            cmdbuf.ReleaseTemporaryRT(internalLutRT.id);
            context.ExecuteCommandBuffer(cmdbuf);
            CommandBufferPool.Release(cmdbuf);
        }

        public void Cleanup() {
            CoreUtils.Destroy(lutBuilderLdrMaterial);
            CoreUtils.Destroy(lutBuilderHdrMaterial);
        }

        public static Vector3 ColorBalanceToLMSCoeffs(float temperature, float tint) {
            // Range ~[-1.5;1.5] works best
            float t1 = temperature / 65f;
            float t2 = tint / 65f;

            // Get the CIE xy chromaticity of the reference white point.
            // Note: 0.31271 = x value on the D65 white point
            float x = 0.31271f - t1 * (t1 < 0f ? 0.1f : 0.05f);
            float y = ColorUtils.StandardIlluminantY(x) + t2 * 0.05f;

            // Calculate the coefficients in the LMS space.
            var w1 = new Vector3(0.949237f, 1.03542f, 1.08728f); // D65 white point
            var w2 = ColorUtils.CIExyToLMS(x, y);
            return new Vector3(w1.x / w2.x, w1.y / w2.y, w1.z / w2.z);
        }

        public static (Vector4, Vector4, Vector4) PrepareShadowsMidtonesHighlights(in Vector4 inShadows, in Vector4 inMidtones, in Vector4 inHighlights) {
            float weight;

            var shadows = inShadows;
            shadows.x = Mathf.GammaToLinearSpace(shadows.x);
            shadows.y = Mathf.GammaToLinearSpace(shadows.y);
            shadows.z = Mathf.GammaToLinearSpace(shadows.z);
            weight = shadows.w * (Mathf.Sign(shadows.w) < 0f ? 1f : 4f);
            shadows.x = Mathf.Max(shadows.x + weight, 0f);
            shadows.y = Mathf.Max(shadows.y + weight, 0f);
            shadows.z = Mathf.Max(shadows.z + weight, 0f);
            shadows.w = 0f;

            var midtones = inMidtones;
            midtones.x = Mathf.GammaToLinearSpace(midtones.x);
            midtones.y = Mathf.GammaToLinearSpace(midtones.y);
            midtones.z = Mathf.GammaToLinearSpace(midtones.z);
            weight = midtones.w * (Mathf.Sign(midtones.w) < 0f ? 1f : 4f);
            midtones.x = Mathf.Max(midtones.x + weight, 0f);
            midtones.y = Mathf.Max(midtones.y + weight, 0f);
            midtones.z = Mathf.Max(midtones.z + weight, 0f);
            midtones.w = 0f;

            var highlights = inHighlights;
            highlights.x = Mathf.GammaToLinearSpace(highlights.x);
            highlights.y = Mathf.GammaToLinearSpace(highlights.y);
            highlights.z = Mathf.GammaToLinearSpace(highlights.z);
            weight = highlights.w * (Mathf.Sign(highlights.w) < 0f ? 1f : 4f);
            highlights.x = Mathf.Max(highlights.x + weight, 0f);
            highlights.y = Mathf.Max(highlights.y + weight, 0f);
            highlights.z = Mathf.Max(highlights.z + weight, 0f);
            highlights.w = 0f;

            return (shadows, midtones, highlights);
        }

        public (Vector4, Vector4, Vector4) PrepareLiftGammaGain(in Vector4 inLift, in Vector4 inGamma, in Vector4 inGain) {
            var lift = inLift;
            lift.x = Mathf.GammaToLinearSpace(lift.x) * 0.15f;
            lift.y = Mathf.GammaToLinearSpace(lift.y) * 0.15f;
            lift.z = Mathf.GammaToLinearSpace(lift.z) * 0.15f;

            float lumLift = ColorUtils.Luminance(lift);
            lift.x = lift.x - lumLift + lift.w;
            lift.y = lift.y - lumLift + lift.w;
            lift.z = lift.z - lumLift + lift.w;
            lift.w = 0f;

            var gamma = inGamma;
            gamma.x = Mathf.GammaToLinearSpace(gamma.x) * 0.8f;
            gamma.y = Mathf.GammaToLinearSpace(gamma.y) * 0.8f;
            gamma.z = Mathf.GammaToLinearSpace(gamma.z) * 0.8f;

            float lumGamma = ColorUtils.Luminance(gamma);
            gamma.w += 1f;
            gamma.x = 1f / Mathf.Max(gamma.x - lumGamma + gamma.w, 1e-03f);
            gamma.y = 1f / Mathf.Max(gamma.y - lumGamma + gamma.w, 1e-03f);
            gamma.z = 1f / Mathf.Max(gamma.z - lumGamma + gamma.w, 1e-03f);
            gamma.w = 0f;

            var gain = inGain;
            gain.x = Mathf.GammaToLinearSpace(gain.x) * 0.8f;
            gain.y = Mathf.GammaToLinearSpace(gain.y) * 0.8f;
            gain.z = Mathf.GammaToLinearSpace(gain.z) * 0.8f;

            float lumGain = ColorUtils.Luminance(gain);
            gain.w += 1f;
            gain.x = gain.x - lumGain + gain.w;
            gain.y = gain.y - lumGain + gain.w;
            gain.z = gain.z - lumGain + gain.w;
            gain.w = 0f;

            return (lift, gamma, gain);
        }

        public (Vector4, Vector4) PrepareSplitToning(in Vector4 inShadows, in Vector4 inHighlights, float balance) {
            // As counter-intuitive as it is, to make split-toning work the same way it does in
            // Adobe products we have to do all the maths in sRGB... So do not convert these to
            // linear before sending them to the shader, this isn't a bug!
            var shadows = inShadows;
            var highlights = inHighlights;

            // Balance is stored in `shadows.w`
            shadows.w = balance / 100f;
            highlights.w = 0f;

            return (shadows, highlights);
        }
    }
}
