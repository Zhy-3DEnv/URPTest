Shader "XRenderPipeline/Extension/VertexColorWithLight" {
    Properties {
        //_Color("BaseColor", COLOR) = (1, 1, 1, 1)
        [Toggle(_SHOW_VERTEX_COLOR)] _ShowVertexColor("Show Vertex Color", Float) = 0
        [Toggle(_WITH_LIGHT)] _WithLight("With Light", Float) = 0

    }

    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "XRP" }

        Pass {
            Tags { "LightMode" = "XRPForward" }

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _SHOW_VERTEX_COLOR

            #pragma shader_feature _WITH_LIGHT

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/LitInput.hlsl"
            #include "Extension.hlsl"

            

            struct a2v {
                float4 positionOS : POSITION;
                half4 vertexColor : COLOR;
                half3 normalOS : NORMAL;

            };

            struct v2f {
                float4 positionCS : SV_POSITION;
                half4 vertexColor : COLOR0;
                half3 normalWS : TEXCOORD2;
            };

            v2f vert(a2v v) {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                VertexNormalInputs vni = GetVertexNormalInputs(v.normalOS.xyz);
                o.normalWS = vni.normalWS;
                o.vertexColor = v.vertexColor;
                return o;
            }

            half4 frag(v2f i) : SV_TARGET {
                #if defined(_SHOW_VERTEX_COLOR)
                #if defined(_WITH_LIGHT)
                    Light light = GetMainLight();
                    half3 color = GetDiffuseWithSH(light.colorIntensity, i.vertexColor, 1, i.normalWS, light.direction);
                    return half4(color, 1);
                #else
                    return i.vertexColor;
                #endif
                #endif
                return half4(1, 1, 1, 1);
            }

            ENDHLSL
        }

    }

    //CustomEditor "LitExtensionGUI"
}
