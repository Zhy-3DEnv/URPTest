Shader "Framework/UI/shd_UI_gaussianBlur_v1" {
    Properties {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _Size ("Blur Size", Range(0, 0.5)) = 0.2
    }
    SubShader {
        Tags {
        "LightMode" = "XRPForward"
        "Queue"="Transparent"
        "IgnoreProjector"="True"
        "RenderType"="Transparent"
        "PreviewType"="Plane"
        }

        Stencil {
        Ref [_Stencil]
        Comp [_StencilComp]
        Pass [_StencilOp]
        ReadMask [_StencilReadMask]
        WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Size;
            half4 _Color;
            CBUFFER_END
            SAMPLER(_CameraOpaqueTexture);
            
            struct a2v
            {
                float4 vertex: POSITION;
                float2 texcoord: TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex: POSITION;
                float4 screenPos: TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            v2f vert(a2v v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = TransformObjectToHClip(v.vertex);
                #if UNITY_UV_STARTS_AT_TOP
                    float scale = 1.0;
                #else
                    float scale = -1.0;
                #endif
                o.screenPos.xy = (float2(o.vertex.x, o.vertex.y * scale) + o.vertex.w) * 0.5;
                o.screenPos.zw = o.vertex.zw;
                return o;
            }
            
            half4 frag(v2f i): SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 sum = half4(0, 0, 0, 0);
                #define GRABPIXEL(weight, kernelx) tex2D(_CameraOpaqueTexture, float4(i.screenPos.x + 0.01 * kernelx * _Size, i.screenPos.y, i.screenPos.z, i.screenPos.w)) * weight
                sum += GRABPIXEL(0.05, -4.0);
                sum += GRABPIXEL(0.09, -3.0);
                sum += GRABPIXEL(0.12, -2.0);
                sum += GRABPIXEL(0.15, -1.0);
                sum += GRABPIXEL(0.18, 0.0);
                sum += GRABPIXEL(0.15, +1.0);
                sum += GRABPIXEL(0.12, +2.0);
                sum += GRABPIXEL(0.09, +3.0);
                sum += GRABPIXEL(0.05, +4.0);

                #define GRABPIXEL01(weight, kernely) tex2D(_CameraOpaqueTexture, float4(i.screenPos.x, i.screenPos.y + 0.01 * kernely * _Size, i.screenPos.z, i.screenPos.w)) * weight
                // G(X) = (1/(sqrt(2*PI*deviation*deviation))) * exp(-(x*x / (2*deviation*deviation)))
                sum += GRABPIXEL01(0.05, -4.0);
                sum += GRABPIXEL01(0.09, -3.0);
                sum += GRABPIXEL01(0.12, -2.0);
                sum += GRABPIXEL01(0.15, -1.0);
                sum += GRABPIXEL01(0.18, 0.0);
                sum += GRABPIXEL01(0.15, +1.0);
                sum += GRABPIXEL01(0.12, +2.0);
                sum += GRABPIXEL01(0.09, +3.0);
                sum += GRABPIXEL01(0.05, +4.0);
                sum /= 2;
                _Color.rgb *= _Color.a;
                return sum * _Color;
            }
            ENDHLSL          
        }
    }
}
