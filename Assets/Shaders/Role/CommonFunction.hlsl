#ifndef COMMONFUNCTION_HLSL
#define COMMONFUNCTION_HLSL

half4 funTest() {
    return half4(1, 1, 0, 1);
}

void desaturation_float(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}

// rgb to linear
void ColorspaceConversion_RGBtoLinear(float3 In, out float3 Out)
{
    float3 linearRGBLo = In / 12.92;;
    float3 linearRGBHi = pow(max(abs((In + 0.055) / 1.055), 1.192092896e-07), float3(2.4, 2.4, 2.4));
    Out = float3(In <= 0.04045) ? linearRGBLo : linearRGBHi;
}

// linear to rgb
void ColorspaceConversion_LineartoRGB(float3 In, out float3 Out)
{
    float3 sRGBLo = In * 12.92;
    float3 sRGBHi = (pow(max(abs(In), 1.192092896e-07), float3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) * 1.055) - 0.055;
    Out = float3(In <= 0.0031308) ? sRGBLo : sRGBHi;
}

half3 replaceColorViaMask(half3 mask, half3 originalColor, half3 newColor, half colorLerp) {
    // new Color
    half3 originalColor_Linear;
    ColorspaceConversion_RGBtoLinear(originalColor.g, originalColor_Linear);
    half3 originalColor_Linear_desaturated;
    desaturation_float(originalColor_Linear, 0, originalColor_Linear_desaturated);
    originalColor_Linear_desaturated *= mask;
    newColor *= originalColor_Linear_desaturated;
    half3 newColor_RGB;
    ColorspaceConversion_LineartoRGB(newColor, newColor_RGB);
    // originalColor
    half3 colorArea = 1 - mask;
    half3 addColor = originalColor * colorArea;

    colorLerp = floor(colorLerp);

    //return originalColor;

    return lerp(originalColor, addColor + newColor_RGB, colorLerp);
}

half3 replaceColorViaMaskAndBrightness(half3 mask, half3 originalColor, half3 newColor, half colorLerp, half brightness = 1) {
    // new Color
    half3 originalColor_Linear;
    ColorspaceConversion_RGBtoLinear(originalColor.g, originalColor_Linear);
    half3 originalColor_Linear_desaturated;
    desaturation_float(originalColor_Linear, 0, originalColor_Linear_desaturated);
    originalColor_Linear_desaturated *= brightness;
    originalColor_Linear_desaturated *= mask;
    newColor *= originalColor_Linear_desaturated;
    half3 newColor_RGB;
    ColorspaceConversion_LineartoRGB(newColor, newColor_RGB);
    // originalColor
    half3 colorArea = 1 - mask;
    half3 addColor = originalColor * colorArea;

    colorLerp = floor(colorLerp);

    //return originalColor;

    return lerp(originalColor, addColor + newColor_RGB, colorLerp);
}

half3 replaceMultipleColorViaMaskAndBrightness(half3 mask, half3 originalColor, half3 newColor01, half3 newColor02, half colorLerp, half brightness = 1) {
    // new Color
    half3 originalColor_Linear;
    ColorspaceConversion_RGBtoLinear(originalColor.g, originalColor_Linear);

    half3 originalColor_Linear_desaturated;
    desaturation_float(originalColor_Linear, 0, originalColor_Linear_desaturated);
    originalColor_Linear_desaturated *= brightness;

    half3 color01 = originalColor_Linear_desaturated * mask.r;
    color01 *= newColor01;
    half3 color01_RGB;
    ColorspaceConversion_LineartoRGB(color01, color01_RGB);
    color01_RGB *= mask.r;

    half3 color02 = originalColor_Linear_desaturated * mask.g;
    color02 *= newColor02;
    half3 color02_RGB;
    ColorspaceConversion_LineartoRGB(color02, color02_RGB);
    float mask_G = (1 - mask.g) * originalColor.g;
    color02_RGB += mask_G;
    color02_RGB *= mask.g;
    
    half3 blendColor = color01_RGB + color02_RGB;

    half fullMask = max(max(mask.r, mask.g), mask.b);
    half3 colorArea = 1 - fullMask;
    half3 addColor = originalColor * colorArea;

    return lerp(originalColor, addColor + blendColor, colorLerp);

}
#endif
