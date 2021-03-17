Shader "Hidden/XRenderPipeline/CopyDepth" {
    SubShader {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "XRP" }

        Pass {
            Name "CopyDepth"
            ZTest Always ZWrite On ColorMask 0

            HLSLPROGRAM

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _DEPTH_NO_MSAA _DEPTH_MSAA_2 _DEPTH_MSAA_4

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Common.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                output.uv = input.uv;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            #define DEPTH_TEXTURE_MS(name, samples) Texture2DMS<float, samples> name
            #define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)
            #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_MSAA(_CameraDepthRT, uv, sampleIndex)
            #define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_CameraDepthRT, sampler_CameraDepthRT, uv)

            #ifdef _DEPTH_MSAA_2
                #define MSAA_SAMPLES 2
            #elif _DEPTH_MSAA_4
                #define MSAA_SAMPLES 4
            #endif

            #ifdef _DEPTH_NO_MSAA
                DEPTH_TEXTURE(_CameraDepthRT);
                SAMPLER(sampler_CameraDepthRT);
            #else
                DEPTH_TEXTURE_MS(_CameraDepthRT, MSAA_SAMPLES);
                float4 _CameraDepthRT_TexelSize;
            #endif

            #if UNITY_REVERSED_Z
                #define DEPTH_DEFAULT_VALUE 1.0
                #define DEPTH_OP min
            #else
                #define DEPTH_DEFAULT_VALUE 0.0
                #define DEPTH_OP max
            #endif

            float SampleDepth(float2 uv) {
            #ifdef _DEPTH_NO_MSAA
                return SAMPLE(uv);
            #else
                int2 coord = int2(uv * _CameraDepthRT_TexelSize.zw);
                float outDepth = DEPTH_DEFAULT_VALUE;

                UNITY_UNROLL
                for (int i = 0; i < MSAA_SAMPLES; ++i) {
                    outDepth = DEPTH_OP(LOAD(coord, i), outDepth);
                }
                return outDepth;
            #endif
            }

            float Frag(Varyings input) : SV_Depth {
                return SampleDepth(input.uv);
            }

            ENDHLSL
        }
    }
}
