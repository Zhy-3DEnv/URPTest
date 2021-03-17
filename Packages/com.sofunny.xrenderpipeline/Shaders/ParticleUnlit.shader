Shader "XRenderPipeline/ParticleUnlit" {
    Properties{
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _DistortionMap("Distortion Map (Normal Map)", 2D) = "bump" {}

        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0.0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 1.0
        _CameraNearFadeDistance("Camera Near Fade", Float) = 1.0
        _CameraFarFadeDistance("Camera Far Fade", Float) = 2.0
        _DistortionBlend("Distortion Blend", Float) = 0.5
        _DistortionStrength("Distortion Strength", Float) = 1.0

        [HideInInspector] _SurfaceType("__surfacetype", Float) = 1.0
        [HideInInspector] _BlendMode("__blendmode", Float) = 0.0
        [HideInInspector] _SrcBlend("__srcblend", Float) = 1.0
        [HideInInspector] _DstBlend("__dstblend", Float) = 10.0
        [HideInInspector] _ZWrite("__zwrite", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _QueueOffset("__queueoffset", Float) = 0.0

        [HideInInspector] _SoftParticlesEnabled("__softparticlesenabled", Float) = 0.0
        [HideInInspector] _SoftParticleFadeParams("__softparticlefadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _CameraFadingEnabled("__camerafadingenabled", Float) = 0.0
        [HideInInspector] _CameraFadeParams("__camerafadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _DistortionEnabled("__distortionenabled", Float) = 0.0
        [HideInInspector] _DistortionStrengthScaled("__distortionstrengthscaled", Float) = 0.1
    }

    SubShader {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "XRP" }

        Pass {
            Tags { "LightMode" = "XRPForward" }
            BlendOp[_BlendOp]
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]
            ColorMask RGB

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard SRP library
            // All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma shader_feature _SOFTPARTICLES_ON
            #pragma shader_feature _FADING_ON
            #pragma shader_feature _DISTORTION_ON

            #pragma multi_compile_fog

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/CommonInput.hlsl"

            #pragma vertex ParticleUnlitVertex
            #pragma fragment ParticleUnlitFragment

            CBUFFER_START(UnityPerMaterial)
            float4 _SoftParticleFadeParams;
            float4 _CameraFadeParams;
            half4 _BaseColor;
            half _DistortionStrengthScaled;
            half _DistortionBlend;
            CBUFFER_END

            #define SOFT_PARTICLE_NEAR_FADE _SoftParticleFadeParams.x
            #define SOFT_PARTICLE_INV_FADE_DISTANCE _SoftParticleFadeParams.y
            #define CAMERA_NEAR_FADE _CameraFadeParams.x
            #define CAMERA_INV_FADE_DISTANCE _CameraFadeParams.y

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            TEXTURE2D(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings {
                half4 color : COLOR;
                float3 uvFog : TEXCOORD0;
#if defined(_SOFTPARTICLES_ON) || defined(_FADING_ON) || defined(_DISTORTION_ON)
                float4 projectedPosition : TEXCOORD1;
#endif
                float4 positionCS : SV_POSITION;
            };

            // Soft particles - returns alpha value for fading particles based on the depth to the background pixel
            float SoftParticles(float near, float far, float4 projection) {
                float fade = 1;
                if (near > 0.0 || far > 0.0) {
                    float sceneZ = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, projection.xy / projection.w).r, _ZBufferParams);
                    float thisZ = LinearEyeDepth(projection.z / projection.w, _ZBufferParams);
                    fade = saturate (far * ((sceneZ - near) - thisZ));
                }
                return fade;
            }

            // Camera fade - returns alpha value for fading particles based on camera distance
            half CameraFade(float near, float far, float4 projection) {
                float thisZ = LinearEyeDepth(projection.z / projection.w, _ZBufferParams);
                return saturate((thisZ - near) * far);
            }

            half3 Distortion(float4 baseColor, float3 distortionVector, half strength, half blend, float4 projection) {
                float2 screenUV = (projection.xy / projection.w) + distortionVector.xy * strength * baseColor.a;
                float4 distortion = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
                return lerp(distortion.rgb, baseColor.rgb, saturate(baseColor.a - blend));
            }

            Varyings ParticleUnlitVertex(Attributes input) {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.color = input.color;
                output.uvFog.xy = input.texcoord;
                output.uvFog.z = ComputeFogFactor(vertexInput.positionCS.z);
#if defined(_SOFTPARTICLES_ON) || defined(_FADING_ON) || defined(_DISTORTION_ON)
                output.projectedPosition = ComputeScreenPosition(vertexInput.positionCS);
#endif
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 ParticleUnlitFragment(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uvFog.xy;
                half4 color = input.color * _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
#if defined(_SOFTPARTICLES_ON)
                color.a *= SoftParticles(SOFT_PARTICLE_NEAR_FADE, SOFT_PARTICLE_INV_FADE_DISTANCE, input.projectedPosition);
#endif
#if defined(_FADING_ON)
                color.a *= CameraFade(CAMERA_NEAR_FADE, CAMERA_INV_FADE_DISTANCE, input.projectedPosition);
#endif
#if defined(_DISTORTION_ON)
                half3 distortionVector = UnpackNormal(SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, uv));
                color.rgb = Distortion(color, distortionVector, _DistortionStrengthScaled, _DistortionBlend, input.projectedPosition);
#endif
                color.rgb = MixFog(color.rgb, input.uvFog.z);
                return color;
            }
            ENDHLSL
        }
    }
    CustomEditor "Framework.XRenderPipeline.ParticleUnlitShader"
}
