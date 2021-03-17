Shader "Framework/shd_robotArm_v1"{
    Properties{
        [Toggle] _OriginalColor("RangeOriginalColor", Int) = 0
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _Mask("Mask", 2D) = "white"{}
        _Color("Color", Color) = (1,1,1,1)
        _Brightness("Brightness", Range(1, 10)) = 1

        _MetallicAORoughnessMap("MetallicAORoughnessMap", 2D) = "white" {}
        _Reflectance("Dielectric Reflectance", Range(0.0, 1.0)) = 0.5
        [Normal]_NormalMap("NormalMap", 2D) = "bump" {}

        [NoScaleOffset] _EmissionMap("Emission", 2D) = "black" {}

        _FresnelExponent ("Fresnel Exponent", Range(0, 1)) = 0
        _FresnelColor ("Fresnel Color", Color) = (1, 0, 0, 1)
        _OutLineWidth ("Out Line Width", Range(0, 1)) = 0.35
        _OutLineColor ("Out Line Color", Color) = (1, 1, 1, 1)

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        //ArmScale
        _ScaleSize("ScaleSize", Range(0, 1.0)) = 0
        _ScaleCenter("ScaleCenter", Range(0, 1.0)) = 0
        _Damping("Damping", Range(-0.2, -0.01)) = -0.04
        _ScaleWidth("ScaleWidth", Range(0, 0.3)) = 0.07

        [Toggle(_USE_GLOSSYENVREFLECTION)] _UseGlossyEnvReflection("Use Glossy Environment Reflection", Float) = 1
        [Toggle(_USE_ENERGYCOMPENSATION)] _UseEnergyCompensation("Use Energy Compensation", Float) = 0
        [Toggle(_USE_SPECULARAO)] _UseSpecularAO("Use Specular AO", Float) = 0
        [Toggle(_USE_FAKEENVSPECULAR)] _UseFakeEnvSpecular("Use Fake Env Specular", Float) = 0
        [Toggle(_USE_CLUSTER_LIGHTING)] _UseClusterLighting("Use Cluster Lighting", Float) = 0
    }
    SubShader{
        Tags {
            "RenderType" = "Opaque"
            "RenderPipeline" = "XRP"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass{
            Name "ForwardLit"
            Tags{"LightMode" = "XRPForward"}

            stencil {
                Ref 120
                Comp Always
                Pass replace
                ZFail replace
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM

            #define _LINESCALE
            #define _USE_EMISSION
            #define MAIN_LIGHT_CALCULATE_SHADOWS
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR

            // XRPPipeline keywords
            #pragma shader_feature _USE_FAKEENVSPECULAR
            #pragma shader_feature _USE_GLOSSYENVREFLECTION
            #pragma shader_feature _USE_ENERGYCOMPENSATION
            #pragma shader_feature _USE_SPECULARAO
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _SHADINGQUALITY_HIGH _SHADINGQUALITY_MEDIUM _SHADINGQUALITY_LOW
            #pragma shader_feature _ENVBRDFAPPROX_V2
            #pragma shader_feature _USE_CLUSTER_LIGHTING

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SOFT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "RobotInput.hlsl"
            #include "RobotCommon.hlsl"

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

            #include "RobotInput.hlsl"
            #include "Packages/com.sofunny.xrenderpipeline/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Pass {
        //     Name "OutLine"
        //     Tags {"LightMode" = "PlayerOutLine"}
        //     ZWrite OFF
        //     ZTest Always

        //     HLSLPROGRAM
        //     #pragma multi_compile_instancing
        //     #pragma vertex VertexOutLine
        //     #pragma fragment FragmentOutLine

        //     #define _LINESCALE 
        //     #include "RobotInput.hlsl"
        //     #include "RobotOutLinePass.hlsl"
        //     ENDHLSL
        // }

        // Pass {
        //     Name"PlayerMask"
        //     Tags {"LightMode" = "PlayerMask"}

        //     ZWrite off
        //     ZTest Always
        //     HLSLPROGRAM
        //     #pragma prefer_hlslcc gles
        //     #pragma exclude_renderers d3d11_9x
        //     #pragma target 2.0

        //     #pragma shader_feature _ALPHATEST_ON

        //     #pragma multi_compile_instancing
        //     #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

        //     #pragma vertex PlayerMaskVertex
        //     #pragma fragment PlayerMaskFragment

        //     #define _LINESCALE
        //     #include "RobotInput.hlsl"
        //     #include "RobotOutLinePass.hlsl"
        //     ENDHLSL
        // }   
    }
}

