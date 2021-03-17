Shader "Hidden/XRenderPipeline/Bloom" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE

    #pragma multi_compile_local _ _USE_RGBM
    #pragma multi_compile_local _ _USE_UPSAMPLE_BLUR

    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/PostProcessCommon.hlsl"

    TEXTURE2D(_MainTex);
    TEXTURE2D(_BloomLowMip);

    float4 _MainTex_TexelSize;
    float4 _Params; // x: threshold, y: threshold knee, z: scatter, w: clamp max
    #define Threshold       _Params.x
    #define ThresholdKnee   _Params.y
    #define Scatter         _Params.z
    #define ClampMax        _Params.w

    half4 EncodeHDR(half3 color) {
#if _USE_RGBM
        half4 outColor = EncodeRGBM(color);
#else
        half4 outColor = half4(color, 1.0);
#endif
#if UNITY_COLORSPACE_GAMMA
        return half4(sqrt(outColor.xyz), outColor.w); // linear to γ
#else
        return outColor;
#endif
    }

    half3 DecodeHDR(half4 color) {
#if UNITY_COLORSPACE_GAMMA
        color.xyz *= color.xyz; // γ to linear
#endif
#if _USE_RGBM
        return DecodeRGBM(color);
#else
        return color.xyz;
#endif
    }

    half4 FragPrefilter(Varyings input) : SV_Target {
        float2 uv = input.uv;
        half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv).xyz;

#if UNITY_COLORSPACE_GAMMA
        color = SRGBToLinear(color);
#endif
        color = min(ClampMax, color);
        // Thresholding
        half brightness = Max3(color.r, color.g, color.b);
        half softness = clamp(brightness - Threshold + ThresholdKnee, 0.0, 2.0 * ThresholdKnee);
        softness = (softness * softness) / (4.0 * ThresholdKnee + 1e-4);
        half multiplier = max(brightness - Threshold, softness) / max(brightness, 1e-4);
        color *= multiplier;

        return EncodeHDR(color);
    }

    half4 BlurH(float2 uv, float texelSize) {
        // 9-tap gaussian blur on the downsampled source
        half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - float2(texelSize * 4.0, 0.0)));
        half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - float2(texelSize * 3.0, 0.0)));
        half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - float2(texelSize * 2.0, 0.0)));
        half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - float2(texelSize * 1.0, 0.0)));
        half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv                               ));
        half3 c5 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(texelSize * 1.0, 0.0)));
        half3 c6 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(texelSize * 2.0, 0.0)));
        half3 c7 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(texelSize * 3.0, 0.0)));
        half3 c8 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(texelSize * 4.0, 0.0)));

        half3 color = c0 * 0.01621622 + c1 * 0.05405405 + c2 * 0.12162162 + c3 * 0.19459459
                    + c4 * 0.22702703
                    + c5 * 0.19459459 + c6 * 0.12162162 + c7 * 0.05405405 + c8 * 0.01621622;

        return EncodeHDR(color);
    }

    half4 FragDownBlurH(Varyings input) : SV_Target {
        float texelSize = _MainTex_TexelSize.x * 2.0; // horizonal blur to low resolution, so mulitipled by 2.0
        return BlurH(input.uv, texelSize);
    }

    half4 FragBlurH(Varyings input) : SV_Target {
        float texelSize = _MainTex_TexelSize.x;
        return BlurH(input.uv, texelSize);
    }

    half4 FragBlurV(Varyings input) : SV_Target {
        float texelSize = _MainTex_TexelSize.y;
        float2 uv = input.uv;

        // Optimized bilinear 5-tap gaussian on the same-sized source (9-tap equivalent)
        half3 c0 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - float2(0.0, texelSize * 3.23076923)));
        half3 c1 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv - float2(0.0, texelSize * 1.38461538)));
        half3 c2 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv                                      ));
        half3 c3 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(0.0, texelSize * 1.38461538)));
        half3 c4 = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(0.0, texelSize * 3.23076923)));

        half3 color = c0 * 0.07027027 + c1 * 0.31621622
                    + c2 * 0.22702703
                    + c3 * 0.31621622 + c4 * 0.07027027;

        return EncodeHDR(color);
    }

    half4 FragOnePassBlur(Varyings input) : SV_Target {
        float2 uv = input.uv;
        float2 texelSize = _MainTex_TexelSize.xy * 0.5;
        half3 color = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(-texelSize.x, texelSize.y)));
        color += DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(-texelSize.x, -texelSize.y)));
        color += DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(texelSize.x, -texelSize.y)));
        color += DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + texelSize.xy));
        color = color * 0.25;
        return EncodeHDR(color);
    }

    half4 FragUpsample(Varyings input) : SV_Target {
        float2 uv = input.uv;
        half3 highMip = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv));
#if _USE_UPSAMPLE_BLUR
        float2 texelSize = _MainTex_TexelSize.xy;
        half3 lowMip = DecodeHDR(SAMPLE_TEXTURE2D(_BloomLowMip, sampler_LinearClamp, uv + float2(-texelSize.x, texelSize.y)));
        lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_BloomLowMip, sampler_LinearClamp, uv + float2(-texelSize.x, -texelSize.y)));
        lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_BloomLowMip, sampler_LinearClamp, uv + float2(texelSize.x, -texelSize.y)));
        lowMip += DecodeHDR(SAMPLE_TEXTURE2D(_BloomLowMip, sampler_LinearClamp, uv + texelSize.xy));
        lowMip = lowMip * 0.25;
#else
        half3 lowMip = DecodeHDR(SAMPLE_TEXTURE2D(_BloomLowMip, sampler_LinearClamp, uv));
#endif
        return EncodeHDR(lerp(highMip, lowMip, Scatter));
    }

    half4 FragFinalUpsample(Varyings input) : SV_Target {
        float2 uv = input.uv;
#if _USE_UPSAMPLE_BLUR
        float2 texelSize = _MainTex_TexelSize.xy / 2;
        half3 color = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(-texelSize.x, texelSize.y)));
        color += DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(-texelSize.x, -texelSize.y)));
        color += DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + float2(texelSize.x, -texelSize.y)));
        color += DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + texelSize.xy));
        color = color * 0.25;
#else
        half3 color = DecodeHDR(SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv));
#endif

        return EncodeHDR(color);
    }

    ENDHLSL

    SubShader {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "XRP"}
        ZTest Always ZWrite Off Cull Off

        // 0
        Pass {
            Name "Bloom Prefilter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL
        }

        // 1
        Pass {
            Name "Bloom Blur Horizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }

        // 2
        Pass {
            Name "Bloom Down Blur Horizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownBlurH
            ENDHLSL
        }

        // 3
        Pass {
            Name "Bloom Blur Vertical"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }

        // 4
        Pass {
            Name "Bloom Upsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            ENDHLSL
        }

        // 5
        Pass {
            Name "Bloom Final Upsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFinalUpsample
            ENDHLSL
        }

        // 6
        Pass {
            Name "Bloom OnePass Blur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOnePassBlur
            ENDHLSL
        }
    }
}
