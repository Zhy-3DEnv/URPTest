Shader "Framework/SFX/shd_GrabWarp_v1" {
    Properties {
        _WarpTex ("Warp Texture", 2D) = "black" {}
        _Mask ("Mask", 2D) = "white" {}
        _WarpInstensity ("Warp Instensity", float) = 1
        [MaterialToggle(_USE_FINALCOL)] _USE_FIANLCOL ("USE FIANLCOL", int) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
    ENDHLSL


    SubShader {
        
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

        Cull off
        ZWrite off
        Blend SrcAlpha OneMinusSrcAlpha , One OneMinusSrcAlpha
        ZTest LEqual

        Pass {
            Tags { "LightMode"="XRPForward" }
            
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile __ _USE_FINALCOL
            struct Attributes {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings {
                float4  positionCS  : SV_POSITION;
                float4  color       : COLOR;
                float4	uv          : TEXCOORD0;
                float4  screenPos   : TEXCOORD1;
                float fogCoord      : TEXCOORD2;
            };

            TEXTURE2D(_WarpTex);
            SAMPLER(sampler_WarpTex);

            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            #if _USE_FINALCOL
            TEXTURE2D(_CameraFinalColor);
            SAMPLER(sampler_CameraFinalColor);
            #else
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            #endif
            

            CBUFFER_START(UnityPerMaterial)
            float4 _WarpTex_ST;
            float4 _Mask_ST;
            half _WarpInstensity;
            CBUFFER_END

            Varyings Vertex(Attributes attributes) {
                Varyings o = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(attributes.positionOS.xyz);
                o.positionCS = vertexInput.positionCS;
                o.uv.xy = TRANSFORM_TEX(attributes.uv, _WarpTex);
                o.uv.zw = TRANSFORM_TEX(attributes.uv, _Mask);
                o.color = attributes.color;
                o.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                o.screenPos = ComputeScreenPosition(o.positionCS);
                return o;
            }

            half4 Fragment(Varyings i) : SV_Target {
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv.zw).r;

                half4 warpTex = i.color * SAMPLE_TEXTURE2D(_WarpTex, sampler_WarpTex, i.uv.xy);
                float2 warpUV = i.screenPos.xy / i.screenPos.w + warpTex.r * _WarpInstensity * mask;

                #if _USE_FINALCOL
                half4 finalCol = SAMPLE_TEXTURE2D(_CameraFinalColor, sampler_CameraFinalColor, warpUV);
                #else
                half4 finalCol = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, warpUV);
                #endif
            
                // finalCol.rgb = MixFog(finalCol.rgb, i.fogCoord);
                return finalCol;
            }
            ENDHLSL
        }
    }
}
