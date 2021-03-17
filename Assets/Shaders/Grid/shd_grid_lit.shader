// When creating shaders for Universal Render Pipeline you can you the ShaderGraph which is super AWESOME!
// However, if you want to author shaders in shading language you can use this teamplate as a base.
Shader "Framework/GridLit" {
  Properties {
    [Toggle(WPOS_ON)] _WPOS("Enable World Position UV", Int) = 0
    _TilingX ("Global Tiling X", Float) = 1
    _TilingY ("Global Tiling Y", Float) = 1

    _BaseColorWhite("Base Color White", Color) = (0.1, 0.1, 0.1, 1)
    [Gamma] _BaseMetallicWhite ("Base Metallic White", Range(0, 1)) = 0
    [Gamma] _BaseRoughnessWhite ("Base roughness White", Range(0, 1)) = 0
    _BaseColorBlack ("Base Color Black", Color) = (0.3, 0.3, 0.3, 1)
    [Gamma] _BaseMetallicBlack ("Base Metallic Black", Range(0, 1)) = 0
    [Gamma] _BaseRoughnessBlack ("Base roughness Black", Range(0, 1)) = 0
    _BaseMap("Base Map", 2D) = "white" {}

    _DetailColor ("Detail Color 01", Color) = (1, 1, 1, 1)
    [Toggle(DETAIL_ON)] _DETAIL_ON("Overwite Base Color, Metallic & roughness by Detail 01", Int) = 0
    [Gamma] _DetailMetallic ("Detail Metallic 01", Range(0, 1)) = 0
    [Gamma] _DetailRoughness("Detail roughness 01", Range(0, 1)) = 0
    _DetailMap ("Detail Map 01", 2D) = "black" {}

    [Toggle(DETAIL2_ON)] _DETAIL2_ON("Overwite Base Color by Detail 02", Int) = 0
    _DetailColor2 ("Detail Color 02", Color) = (1, 1, 1, 1)
    _DetailMap2 ("Detail Map 02", 2D) = "black" {}

    [Toggle(DETAIL3_ON)] _DETAIL3_ON("Overwite Base Color by Detail 03", Int) = 0
    _DetailColor3 ("Detail Color 03", Color) = (1, 1, 1, 1)
    _DetailMap3 ("Detail Map 03", 2D) = "black" {}

    // -----------------------------------------------

    _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    _Reflectance("Dielectric Reflectance", Range(0.0, 1.0)) = 0.5
    _EmissionColor("Color", Color) = (0,0,0)
    _EmissionMap("Emission", 2D) = "white" {}

    [Toggle(_USE_NORMALMAP)] _USE_NORMALMAP("Use NormalMap", Float) = 1
    [Normal]_NormalMap("NormalMap", 2D) = "bump" {}

    [Toggle(_USE_GLOSSYENVREFLECTION)] _UseGlossyEnvReflection("Use Glossy Environment Reflection", Float) = 1
    [Toggle(_USE_ENERGYCOMPENSATION)] _UseEnergyCompensation("Use Energy Compensation", Float) = 0
    [Toggle(_USE_SPECULARAO)] _UseSpecularAO("Use Specular AO", Float) = 0
    [Toggle(_USE_FAKEENVSPECULAR)] _UseFakeEnvSpecular("Use Fake Env Specular", Float) = 0
    [Toggle(_USE_CLUSTER_LIGHTING)] _UseClusterLighting("Use Cluster Lighting", Float) = 0

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
    _ReceiveShadows("Receive Shadows", Float) = 1.0
  }

  SubShader {
    Tags {
      "RenderType" = "Opaque"
      "RenderPipeline" = "XRP"
      "IgnoreProjector" = "True"
      "Queue" = "Geometry"
    }

    Pass {
      Name "ForwardLit"
      Tags { "LightMode" = "XRPForward" }

      Blend [_SrcBlend] [_DstBlend]
      ZWrite [_ZWrite]
      Cull [_Cull]

      HLSLPROGRAM
      #pragma prefer_hlslcc gles
      #pragma exclude_renderers d3d11_9x
      #pragma target 2.0

      #pragma shader_feature WPOS_ON
      #pragma shader_feature DETAIL_ON
      #pragma shader_feature DETAIL2_ON
      #pragma shader_feature DETAIL3_ON

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
      #pragma shader_feature _USE_CLUSTER_LIGHTING

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

      #include "GridLitInput.hlsl"
      #include "GridLitForwardPass.hlsl"
      ENDHLSL
    }
  }
  CustomEditor "GridLitShader"
}
