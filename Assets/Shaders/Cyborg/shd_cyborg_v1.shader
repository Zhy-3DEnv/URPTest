Shader "Framework/shd_cyvorg_v1" {
    Properties {
        _BaseColor("BaseColor", Color) = (1.0, 1.0, 1.0, 1.0)
        _BaseMap("BaseMap", 2D) = "white" {}
        _MetallicAORoughnessTextureMode("MetallicAORoughnessTextureMode", Float) = 0
        _UseSeparateMap("Use Separate Metallic AO Roughness Map", Float) = 0
        _MetallicMap("MetallicMap", 2D) = "white" {}
        _AmbientOcclusionMap("AmbientOcclusionMap", 2D) = "white" {}
        _RoughnessMap("RoughnessMap", 2D) = "white" {}
        _AmbientOcclusionOffset("AmbientOcclusion Offset", Range(-1.0, 1.0)) = 0.0
        _RoughnessOffset("Roughness Offset", Range(-1.0, 1.0)) = 0.0
        _MetallicOffset("Metallic Offset", Range(-1.0, 1.0)) = 0.0
        _MetallicAORoughnessMap("MetallicAORoughnessMap", 2D) = "white" {}
        _Reflectance("Dielectric Reflectance", Range(0.0, 1.0)) = 0.5
        [Normal]_NormalMap("NormalMap", 2D) = "bump" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_USE_EMISSION)] _UseEmission("Use Emission", Float) = 0
        _EmissionColor("EmissionColor", Color) = (0.0, 0.0, 0.0)
        _EmissionMap("EmissionMap", 2D) = "white" {}
        [Toggle(_USE_GLOSSYENVREFLECTION)] _UseGlossyEnvReflection("Use Glossy Environment Reflection", Float) = 1
        [Toggle(_USE_ENERGYCOMPENSATION)] _UseEnergyCompensation("Use Energy Compensation", Float) = 0
        [Toggle(_USE_SPECULARAO)] _UseSpecularAO("Use Specular AO", Float) = 0
        [Toggle(_USE_FAKEENVSPECULAR)] _UseFakeEnvSpecular("Use Fake Env Specular", Float) = 0
        // [Toggle(_TEST)] _Test("Test", Float) = 1

        [HideInInspector] _SurfaceType("__surfacetype", Float) = 0.0
        [HideInInspector] _AlphaTest("__alphatest", Float) = 0.0
        [HideInInspector] _BlendMode("__blendmode", Float) = 0.0
        [HideInInspector] _SrcBlend("__srcblend", Float) = 1.0
        [HideInInspector] _DstBlend("__dstblend", Float) = 0.0
        [HideInInspector] _ZWrite("__zwrite", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _QueueOffset("__queueoffset", Float) = 0.0
        [HideInInspector] _ShadingQuality("__shadingquality", Float) = 0.0
        [HideInInspector] _EnvBRDFApprox("__envbrdfapprox", Float) = 1.0
        [HideInInspector] _DebugMode("__debugmode", Float) = 0.0
        _ReceiveShadows("Receive Shadows", Float) = 1.0
    }

    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "XRP" }

        Pass {
            Tags { "LightMode" = "XRPForward" }

            stencil {
                Ref 120
                Comp Always
                Pass replace
                ZFail replace
            }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma shader_feature _USE_SEPARATEMAP
            #pragma shader_feature _USE_METALLICAOROUGHNESSMAP
            #pragma shader_feature _USE_METALLICAOROUGHNESSOFFSET
            #pragma shader_feature _USE_NORMALMAP
            #pragma shader_feature _USE_ALPHATEST
            #pragma shader_feature _USE_EMISSION
            #pragma shader_feature _USE_GLOSSYENVREFLECTION
            #pragma shader_feature _USE_ENERGYCOMPENSATION
            #pragma shader_feature _USE_SPECULARAO
            #pragma shader_feature _USE_FAKEENVSPECULAR
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _SHADINGQUALITY_HIGH _SHADINGQUALITY_MEDIUM _SHADINGQUALITY_LOW
            #pragma shader_feature _ENVBRDFAPPROX_V2

            // #pragma shader_feature _TEST

            #pragma shader_feature _DEBUG_MATERIAL
            #pragma shader_feature _DEBUG_PIPELINE

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SOFT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _USE_CLUSTER_LIGHTING

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags{ "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma shader_feature _USE_ALPHATEST

            #pragma multi_compile_instancing

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass {
            Name "Meta"
            Tags{ "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex VertexMeta
            #pragma fragment FragmentMeta

            #pragma shader_feature _USE_EMISSION
            #pragma shader_feature _USE_SEPARATEMAP
            #pragma shader_feature _USE_METALLICAOROUGHNESSMAP
            #pragma shader_feature _USE_METALLICAOROUGHNESSSCALE
            #pragma shader_feature _USE_ALPHATEST


            #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/LitInput.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }

        Pass {
            Name "XRPOverrideDepthFarest"
            Tags { "LightMode" = "XRPOverrideDepthFarest" }

            Cull[_Cull]
            ColorMask 0
            ZTest Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex OverrideDepthVertex
            #pragma fragment FillFarestDepthFragment

            #include "Packages/com.sofunny.xrenderpipeline/Shaders/OverrideDepthPass.hlsl"
            ENDHLSL
        }

        Pass {
            Name "XRPOverrideDepthNearest"
            Tags { "LightMode" = "XRPOverrideDepthNearest" }

            Cull[_Cull]
            ColorMask 0
            ZTest Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex OverrideDepthVertex
            #pragma fragment FillNearestDepthFragment

            #include "Packages/com.sofunny.xrenderpipeline/Shaders/OverrideDepthPass.hlsl"
            ENDHLSL
        }

        Pass {
            Name "XRPRestoreDepth"
            Tags { "LightMode" = "XRPRestoreDepth" }

            Cull[_Cull]
            ColorMask 0

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex OverrideDepthVertex
            #pragma fragment FillOriginDepthFragment

            #include "Packages/com.sofunny.xrenderpipeline/Shaders/OverrideDepthPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Framework.XRenderPipeline.LitShader"
}
