Shader "XRenderPipeline/SimpleLit" {
    Properties {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap("Base Map", 2D) = "white" {}
        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _SpecSmoothness("Specular Smoothness", float) = 8
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
    }

    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "XRP" }

        Pass {

            Tags { "LightMode" = "XRPForward" }

            HLSLPROGRAM
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Light.hlsl"
            #pragma multi_compile_fog
            #pragma vertex SimpleLitVertex
            #pragma fragment SimpleLitFragment

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half _SpecSmoothness;
            half3 _EmissionColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SpecMap); SAMPLER(sampler_SpecMap);

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings {
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float4 positionCS : SV_POSITION;
            };

            Varyings SimpleLitVertex(Attributes input) {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.posWS = vertexInput.positionWS;
                output.normal = normalize(normalInput.normalWS);
                output.viewDir = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 SimpleLitFragment(Varyings input) : SV_Target {
                half4 baseColor = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normal);
                half NoL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuseLight = mainLight.colorIntensity.rgb * NoL;
                float3 halfVec = SafeNormalize(float3(mainLight.direction) + float3(input.viewDir));
                half NoH = saturate(dot(normalWS, halfVec));
                half3 specularLight = mainLight.colorIntensity.rgb * pow(NoH, _SpecSmoothness);
                half3 finalColor = diffuseLight * baseColor.rgb + specularLight * _SpecColor.rgb + _EmissionColor.rgb;
                return half4(finalColor, 1);
            }
            ENDHLSL


        }

    }
}