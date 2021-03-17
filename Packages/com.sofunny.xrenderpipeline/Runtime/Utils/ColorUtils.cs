using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.XRenderPipeline {

    public static class ColorUtils {

        public static Color ConvertSRGBToActiveColorSpace(Color color) {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
        }

        public static Color ConvertLinearToActiveColorSpace(Color color) {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color : color.gamma;
        }

        public static float Luminance(Color color) {
            return color.r * 0.2126729f + color.g * 0.7151522f + color.b * 0.072175f;
        }

        public static uint ToHex(Color c) {
            return ((uint)(c.a * 255) << 24) | ((uint)(c.r * 255) << 16) | ((uint)(c.g * 255) << 8) | (uint)(c.b * 255);
        }

        public static Color ToRGBA(uint hex) {
            return new Color(((hex >> 16) & 0xff) / 255f, ((hex >> 8) & 0xff) / 255f, (hex & 0xff) / 255f, ((hex >> 24) & 0xff) / 255f);
        }

        // An analytical model of chromaticity of the standard illuminant, by Judd et al.
        // http://en.wikipedia.org/wiki/Standard_illuminant#Illuminant_series_D
        // Slightly modifed to adjust it with the D65 white point (x=0.31271, y=0.32902).
        public static float StandardIlluminantY(float x) {
            return 2.87f * x - 3f * x * x - 0.27509507f;
        }

        // CIE xy chromaticity to CAT02 LMS.
        // http://en.wikipedia.org/wiki/LMS_color_space#CAT02
        public static Vector3 CIExyToLMS(float x, float y) {
            float Y = 1f;
            float X = Y * x / y;
            float Z = Y * (1f - x - y) / y;

            float L =  0.7328f * X + 0.4296f * Y - 0.1624f * Z;
            float M = -0.7036f * X + 1.6975f * Y + 0.0061f * Z;
            float S =  0.0030f * X + 0.0136f * Y + 0.9834f * Z;

            return new Vector3(L, M, S);
        }
    }
}
