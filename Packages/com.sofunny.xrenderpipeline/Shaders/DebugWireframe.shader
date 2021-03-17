Shader "XRenderPipeline/DebugWireframe" {
    SubShader {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "XRP" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha

        // wireframe shader pass
        Pass {
            Tags { "LightMode" = "XRPDebug" }

            HLSLPROGRAM
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            half4 _WireframeColor;

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                return _WireframeColor;
            }
            ENDHLSL
        }

        // override shader pass
        Pass {
            Tags { "LightMode" = "XRPForward" }

            HLSLPROGRAM
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            half4 _DebugOverrideColor;

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                return _DebugOverrideColor;
            }
            ENDHLSL
        }

    }
}
