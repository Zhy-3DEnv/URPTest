Shader "XRenderPipeline/ClusteredForwardSimpleLit" {
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
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/ClusterCommon.hlsl"
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
                float3 normalWS : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float4 positionCS : SV_POSITION;
            };

            half3 LightingBlinnPhong(Light light, half3 normalWS, half3 viewDirectionWS, half3 baseColor, half3 specularColor) {
                half NoL = saturate(dot(normalWS, light.direction));
                half3 Fd = light.colorIntensity.rgb * (light.colorIntensity.w * light.distanceAttenuation * NoL);
                half3 h = normalize(light.direction + viewDirectionWS);
                half NoH = saturate(dot(normalWS, h));
                half3 Fr = light.colorIntensity.rgb * (light.colorIntensity.w * light.distanceAttenuation * pow(NoH, _SpecSmoothness));
                return Fd * baseColor + Fr * specularColor;
            }

            Varyings SimpleLitVertex(Attributes input) {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.posWS = vertexInput.positionWS;
                output.normalWS = normalize(normalInput.normalWS);
                output.viewDir = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 SimpleLitFragment(Varyings input) : SV_Target {
                half4 baseColor = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                float linearDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float linearDepthLog2 = log2(linearDepth);
                uint zSlice = uint(max(linearDepthLog2 * _SliceScale + _SliceBias, 0.0));
                // uint zSlice = uint(max(log2(LinearEyeDepth(input.projectedPosition.z / input.projectedPosition.w, _ZBufferParams)) * _SliceScale + _SliceBias, 0.0));

                float2 screenPos = float2(input.positionCS.x, _ScreenParams.y - input.positionCS.y);
                uint3 clusterId = uint3(uint2(screenPos / _ClusterDimXYAndTileSize.zw), zSlice);

                uint clusterIndex = clusterId.x + _ClusterDimXYAndTileSize.x * clusterId.y + (_ClusterDimXYAndTileSize.x * _ClusterDimXYAndTileSize.y) * clusterId.z;

                half3 finalRGB = half3(0, 0, 0);
                LightGrid lightGrid = LoadLightGrid(clusterIndex);
#if 0
                half3 cntDebugRGB;
                uint lcnt = (lightGrid.count & 0xFFFF) + (lightGrid.count >> 16) & 0xFFFF;
                if (lcnt <= 3) {
                    return half4(0, 0.3 * lcnt, 0, 1); // green
                } else if (lcnt <= 6) {
                    return half4(0, 0, 0.3 * (lcnt - 3), 1); // blue
                } else if (lcnt <= 10) {
                    return half4(0.3 * (lcnt - 6), 0.3 * (lcnt - 6), 0, 1); // yellow
                } else if (lcnt <= 16) {
                    return half4(0.3 * (lcnt - 10), 0, 0, 1); // red
                } else {
                    return half4(1, 0, 1, 1); // magenta
                }
#endif
                float3 positionWS = input.posWS;
                uint pointLightCount = lightGrid.count & 0xFFFF;
                for (uint i = 0; i < pointLightCount; ++i) {
                    uint lightIndex = LoadLightIndex(lightGrid.offset + i);
                    PointLight plight = LoadPointLight(lightIndex);

                    Light light = GetClusteredPointLight(plight, positionWS);
                    finalRGB += LightingBlinnPhong(light, normalize(input.normalWS), input.viewDir, baseColor.rgb, _SpecColor.rgb);
                }
                uint spotLightCount = (lightGrid.count >> 16) & 0xFFFF;
                for (uint i = 0; i < spotLightCount; ++i) {
                    uint lightIndex = LoadLightIndex(lightGrid.offset + pointLightCount + i);
                    SpotLight slight = LoadSpotLight(lightIndex);
                    Light light = GetClusteredSpotLight(slight, positionWS);
                    finalRGB += LightingBlinnPhong(light, normalize(input.normalWS), input.viewDir, baseColor.rgb, _SpecColor.rgb);
                }

                return half4(finalRGB, baseColor.a);
            }
            ENDHLSL

        }

    }
}