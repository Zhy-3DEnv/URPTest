Shader "Framework/shd_robotOutLine_v2"{
    Properties{
        _OutLineWidth ("Out Line Width", Range(0, 1)) = 0.35
        _OutLineColor ("Out Line Color", Color) = (1, 1, 1, 1)
        _ScaleSize("ScaleSize", Range(0, 1.0)) = 0
        _ScaleCenter("ScaleCenter", Range(0, 1.0)) = 0
        _Damping("Damping", Range(-0.2, -0.01)) = -0.04
        _ScaleWidth("ScaleWidth", Range(0, 0.3)) = 0.07
    }
    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "RenderPipeline" = "XRP"
            "IgnoreProjector" = "True"
            "Queue" = "Transparent+100"
        }
        LOD 300

        Pass {
            Name"OutLine"
            Tags {
                "LightMode" = "XRPUnlit"
            }
            ZWrite OFF
            Ztest Always
            Cull off

            stencil{
	            Ref 100
	            Comp Greater 
                Pass Keep
                Fail Keep
            }

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex VertexOutLine
            #pragma fragment FragmentOutLine
            #pragma 
         
            #include "RobotInput.hlsl"
            #include "RobotOutLinePass.hlsl"
            ENDHLSL
        } 
    }
}
