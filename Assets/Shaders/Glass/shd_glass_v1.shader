Shader "Framework/shd_glass_v1" {
    Properties {
        _BaseMap("Albedo", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)

        _Reflectance("Dielectric Reflectance", Range(0.0, 1.0)) = 0.5
        _MetallicAORoughnessMap("MetallicAORoughnessMap", 2D) = "white" {}

        _Fresnel ("Fresnel", float) = 1

        [Normal]_NormalMap("NormalMap", 2D) = "bump" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _MatCap("MatCap", 2D) = "black" {}
        _Thickness ("Thickness", float) = 0.4

        _FresnelExponent ("Fresnel Exponent", Range(0, 1)) = 0
        _FresnelColor ("Fresnel Color", Color) = (1, 0, 0, 1)

        [Toggle(_USE_GLOSSYENVREFLECTION)] _UseGlossyEnvReflection("Use Glossy Environment Reflection", Float) = 1
        [Toggle(_USE_ENERGYCOMPENSATION)] _UseEnergyCompensation("Use Energy Compensation", Float) = 0
        [Toggle(_USE_SPECULARAO)] _UseSpecularAO("Use Specular AO", Float) = 1
        [Toggle(_USE_CLUSTER_LIGHTING)] _UseClusterLighting("Use Cluster Lighting", Float) = 0
        [Toggle(_USE_FAKEENVSPECULAR)] _UseFakeEnvSpecular("Use Fake Env Specular", Float) = 0
    }

    SubShader {
        Tags{
            "RenderType" = "Transparent"
            "Queue" = "Transparent-200"
            "RenderPipeline" = "XRP"
            "IgnoreProjector" = "True" }
     
        Pass {
            Tags {"LightMode" = "XRPForward"}

            stencil {
                Ref 120
                Comp Always
                Pass replace
                ZFail replace
            }

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #define _USE_EMISSION
            #define MAIN_LIGHT_CALCULATE_SHADOWS
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x gles
            #pragma target 4.5

            // XRP Pipeline keywords
            #pragma shader_feature _USE_GLOSSYENVREFLECTION
            #pragma shader_feature _USE_ENERGYCOMPENSATION
            #pragma shader_feature _USE_SPECULARAO
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _SHADINGQUALITY_HIGH _SHADINGQUALITY_MEDIUM _SHADINGQUALITY_LOW
            #pragma shader_feature _ENVBRDFAPPROX_V2
            #pragma shader_feature _USE_CLUSTER_LIGHTING
            #pragma shader_feature _USE_FAKEENVSPECULAR

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SOFT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
          //  #define _USE_REFRACTION 1
            #define _USE_MATCAP 1

            #include "GlassInput.hlsl"
            #include "GlassForwardPass.hlsl"
            ENDHLSL
        }
    }
}
