Shader "Framework/SFX/shd_AlphaBlendedScroll_v1" {
    Properties {
        _TintColor ("Color", Color) = (1,0.6235296, 0.1470588,1)
        _ColorMultiplier ("Color Multiplier", Range(0, 10)) = 1
        _MainTextUSpeed ("MainText U Speed", Float ) = 0
        _MainTextVSpeed ("MainText V Speed", Float ) = 0
        _MainTex ("MainTex", 2D) = "white" {}
        _GradientPower ("Gradient Power", Range(0, 50)) = 0
        _GradientUSpeed ("Gradient U Speed", Float ) = 0.1
        _GradientVSpeed ("Gradient V Speed", Float ) = 0.1
        _Gradient ("Gradient", 2D) = "white" {}
        _NoiseAmount ("Noise Amount", Range(-1, 1)) = 0.1
        _DistortionUSpeed ("Distortion U Speed", Float ) = 0.1
        _DistortionVSpeed ("Distortion V Speed", Float ) = 0.1
        _Distortion ("Distortion", 2D) = "white" {}
        _MainTexMask ("MainTexMask", 2D) = "white" {}
        _DoubleSided ("DoubleSided", Float ) = 1
    }
    
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

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #pragma multi_compile_fog

            CBUFFER_START(UnityPerMaterial)
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TintColor;
            float _GradientUSpeed;
            float _GradientVSpeed;
            sampler2D _Gradient;
            float4 _Gradient_ST;
            float _NoiseAmount;
            sampler2D _Distortion;
            float4 _Distortion_ST;
            float _DistortionUSpeed;
            float _DistortionVSpeed;
            sampler2D _MainTexMask;
            float4 _MainTexMask_ST;
            float _GradientPower;
            float _ColorMultiplier;
            float _MainTextUSpeed;
            float _MainTextVSpeed;
            float _DoubleSided;
            CBUFFER_END

            struct appdate {
                float4 vertex :POSITION;
                float2 uv :TEXCOORD0;
                float4 vertexColor :COLOR;
            };

            struct v2f {
                float4 pos :SV_POSITION;
                float2 uv :TEXCOORD0;
                float4 vertexColor :COLOR;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdate v) {
                v2f o;
                o.uv = v.uv;
                o.vertexColor = v.vertexColor;
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : COLOR {
                float2 distortionUV = (i.uv + (_Time.g * float2(_DistortionUSpeed, _DistortionVSpeed)));
                fixed4 distortionVar = tex2D(_Distortion, TRANSFORM_TEX(distortionUV, _Distortion));
                float2 noiseUV = lerp(i.uv, float2(distortionVar.r, distortionVar.r), _NoiseAmount);
                float2 mainTexUV = ((_Time.g * float2(_MainTextUSpeed, _MainTextVSpeed)) + i.uv);
                fixed4 mainTexVar = tex2D(_MainTex, TRANSFORM_TEX(mainTexUV, _MainTex));
                float2 gradientUV = (noiseUV + (_Time.g * float2(_GradientUSpeed, _GradientVSpeed)));
                fixed4 gradientVar = tex2D(_Gradient, TRANSFORM_TEX(gradientUV, _Gradient));
                fixed4 mainTexMaskVar = tex2D(_MainTexMask, TRANSFORM_TEX(i.uv, _MainTexMask));
                fixed3 emissive = (mainTexVar.rgb * i.vertexColor.rgb * (_TintColor.rgb * _ColorMultiplier) * 2.0 * (mainTexVar.a * (gradientVar.rgb * pow(gradientVar.rgb,_GradientPower)) * mainTexMaskVar.a));

                fixed4 finalCol;
                finalCol.rgb = emissive * 2;
                finalCol.a = i.vertexColor.a * _TintColor.a * mainTexVar.a * mainTexMaskVar.a;
                UNITY_APPLY_FOG(i.fogCoord, finalCol);
                return finalCol;
            }
            ENDCG
        }
    }
}
