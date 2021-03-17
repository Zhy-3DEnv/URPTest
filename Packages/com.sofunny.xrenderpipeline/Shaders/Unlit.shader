Shader "XRenderPipeline/Unlit" {
    Properties {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "XRP" }

        Pass {

            Tags { "LightMode" = "XRPForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/CommonInput.hlsl"
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings UnlitVertex(Attributes input) {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 UnlitFragment(Varyings input) : SV_Target {
                half4 color = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                return color;
            }
            ENDHLSL
        }
    }
}
