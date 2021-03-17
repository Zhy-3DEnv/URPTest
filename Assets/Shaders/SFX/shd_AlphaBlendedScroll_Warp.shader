Shader "Framework/SFX/shd_AlphaBlendedScroll_Warp" {
    Properties {
        _TintColor ("Color", Color) = (1,0.6235296, 0.1470588,1)
        _ColorMultiplier ("Color Multiplier", Range(0, 10)) = 1
        _MainTextUSpeed ("MainText U Speed", Float ) = 0
        _MainTextVSpeed ("MainText V Speed", Float ) = 0
        _MainTex ("MainTex", 2D) = "white" {}

        _DissolveTex ("Dissolve", 2D) = "white" {}
        [HDR]_DissolveFirstColor("Dissolve First Color", Color) = (1,0,0,1)
        [HDR]_DissolveSecondColor("Dissolve Second Color", Color) = (1,0,0,1)
        _LieWidth("Dissolve Size", Range(0.0, 0.2)) = 0.1

        _NoiseAmount ("Noise Amount", Range(-1, 1)) = 0.1
        _DistortionUSpeed ("Distortion U Speed", Float ) = 0.1
        _DistortionVSpeed ("Distortion V Speed", Float ) = 0.1
        _Distortion ("Distortion", 2D) = "white" {}
        _MainTexMask ("MainTexMask", 2D) = "white" {}
        _DoubleSided ("DoubleSided", Float ) = 1
        _Mask ("Mask", Range(0, 1)) = 0

        _R ("Mask Excessive", Range(0 , 1)) = 0.1
        _MoveDir("MoveDir", vector) = (0, 3, 3, 1)
    }
    
    HLSLINCLUDE
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
    ENDHLSL

    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Pass {
            Tags { "LightMode"="XRPForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _DissolveTex_ST;
            float4 _Distortion_ST;
            float4 _MainTexMask_ST;

            half4 _TintColor;
            
            float _DissolveUSpeed;
            float _DissolveVSpeed;
            float _DistortionUSpeed;
            float _DistortionVSpeed;
            float _MainTextUSpeed;
            float _MainTextVSpeed;

            float _NoiseAmount;
            float _DissolvePower;
            float _ColorMultiplier;
            float _DoubleSided;
            float _Mask;
            float _R;
            float3 _MoveDir;
            half4 _DissolveFirstColor;
            half4 _DissolveSecondColor;
            float _LieWidth;

            CBUFFER_END

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_Distortion);    SAMPLER(sampler_Distortion);
            TEXTURE2D(_DissolveTex);     SAMPLER(sampler_DissolveTex);
            TEXTURE2D(_MainTexMask); SAMPLER(sampler_MainTexMask);

            struct appdate {
                float4 vertex :POSITION;
                float2 uv :TEXCOORD0;
                float4 uv1 :TEXCOORD1;
                float4 vertexColor :COLOR;
                float3 normal :NORMAL;
            };

            struct v2f {
                float4 pos :SV_POSITION;
                float4 uv :TEXCOORD0;
                float4 uv1 :TEXCOORD1;
                float4 vertexColor :COLOR;
                float dissolveAmount :TEXCOORD2;
            };

            v2f vert (appdate v) {
                v2f o;
                o.uv.xy = v.uv;
                o.uv.zw = TRANSFORM_TEX(v.uv, _Distortion) + _Time.g * float2(_DistortionUSpeed, _DistortionVSpeed);
                o.uv1.xy = TRANSFORM_TEX(v.uv, _MainTex) + _Time.g * float2(_MainTextUSpeed, _MainTextVSpeed);
                o.uv1.zw = TRANSFORM_TEX(v.uv, _DissolveTex);

                float mask = saturate((v.uv.x - (v.uv1.w))/ _R);
                half3 DirectionVar =  SAMPLE_TEXTURE2D_LOD(_Distortion, sampler_Distortion, o.uv.zw, 1).xyz * _MoveDir.xyz;
                DirectionVar = DirectionVar * 2 - 1;
                DirectionVar *= mask;
                
                float angle = v.uv1.x; 
                float xn = DirectionVar.y * cos(angle) - DirectionVar.z * sin(angle);
                float yn = DirectionVar.z * sin(angle) + DirectionVar.z *cos(angle);
                float3 rotationDir = float3(DirectionVar.x, xn, yn);

                float3 transformPosWS = TransformObjectToWorld(v.vertex.xyz + rotationDir);
               
                o.pos = TransformWorldToHClip(transformPosWS);
                o.dissolveAmount = v.uv1.z;
                
                o.vertexColor = v.vertexColor;
                return o;
            }

            half4 frag(v2f i) : COLOR {
                float2 distortionUV = i.uv.zw;
                half4 distortionVar = SAMPLE_TEXTURE2D(_Distortion, sampler_Distortion, distortionUV);

                float2 noiseUV = lerp(i.uv.xy, float2(distortionVar.r, distortionVar.r), _NoiseAmount);

                float2 mainTexUV = i.uv1.xy;
                half4 mainTexVar = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV);

                float2 DissolveUV = noiseUV + i.uv1.zw;
                half4 Dissolve = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, DissolveUV);
                float dissolveAmount = i.dissolveAmount;
                float clipValue = saturate(Dissolve - dissolveAmount);

                half lerpValue = 1 - smoothstep(0.0, _LieWidth, Dissolve.r - dissolveAmount);
                half3 DissolveColor = lerp(_DissolveFirstColor, _DissolveSecondColor, lerpValue);
                DissolveColor = pow(DissolveColor, 10);

                half4 mainTexMaskVar =  SAMPLE_TEXTURE2D(_MainTexMask,sampler_MainTexMask, TRANSFORM_TEX(i.uv.xy, _MainTexMask));

                half3 diffuse = mainTexVar.rgb * i.vertexColor.rgb * _TintColor.rgb * _ColorMultiplier
                * 2.0 * mainTexVar.a * mainTexMaskVar.r ;
                diffuse = lerp(diffuse * 2, DissolveColor,  lerpValue * step(0.0001, dissolveAmount));

                half4 finalCol;
                finalCol.rgb = diffuse;

                finalCol.a = i.vertexColor.a * _TintColor.a * mainTexVar.a * mainTexMaskVar.r * clipValue;

                //finalCol.rgb = i.vertexColor;
                //finalCol.a = 1;
                return finalCol;
            }
            ENDHLSL
        }
    }
}
