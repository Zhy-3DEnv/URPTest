Shader "Hidden/XRenderPipeline/OnChipCompatibleUberPostProcess" {
    HLSLINCLUDE
    #pragma multi_compile_local _ _HDR_GRADING _TONEMAPPING_ACES _TONEMAPPING_NEUTRAL _TONEMAPPING_UCHIMURA
    #pragma multi_compile_local _ _FILM_GRAIN
    #pragma multi_compile_local _ _LINEAR_TO_SRGB_CONVERSION

    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/PostProcessCommon.hlsl"

    TEXTURE2D(_BlitTex);
    TEXTURE2D(_Grain_Texture);
    TEXTURE2D(_InternalLut);
    TEXTURE2D(_UserLut);

    float4 _Lut_Params;
    float4 _UserLut_Params;
    float2 _Grain_Params;
    float4 _Grain_TilingParams;

    #define LutParams               _Lut_Params.xyz
    #define PostExposure            _Lut_Params.w
    #define UserLutParams           _UserLut_Params.xyz
    #define UserLutContribution     _UserLut_Params.w

    #define GrainIntensity          _Grain_Params.x
    #define GrainResponse           _Grain_Params.y
    #define GrainScale              _Grain_TilingParams.xy
    #define GrainOffset             _Grain_TilingParams.zw

    half4 Frag(Varyings input) : SV_Target {

        float2 uv = input.uv;
        half3 color = half3(0, 0, 0);

        color = SAMPLE_TEXTURE2D(_BlitTex, sampler_LinearClamp, uv).xyz;
        // Gamma space... Just do the rest of Uber in linear and convert back to sRGB at the end
        #if UNITY_COLORSPACE_GAMMA
        color = SRGBToLinear(color);
        #endif
        // Color grading is always enabled when post-processing/uber is active
        color = ApplyColorGrading(color, PostExposure, TEXTURE2D_ARGS(_InternalLut, sampler_LinearClamp), LutParams, TEXTURE2D_ARGS(_UserLut, sampler_LinearClamp), UserLutParams, UserLutContribution);
        #if _FILM_GRAIN
        color = ApplyGrain(color, uv, TEXTURE2D_ARGS(_Grain_Texture, sampler_LinearRepeat), GrainIntensity, GrainResponse, GrainScale, GrainOffset);
        #endif
        // Back to sRGB
        #if UNITY_COLORSPACE_GAMMA || _LINEAR_TO_SRGB_CONVERSION
        color = LinearToSRGB(color);
        #endif

        return half4(color, 1.0);
    }
    ENDHLSL

    SubShader {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "XRP"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass {
            Name "OnChipCompatibleUberPostProcess"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
