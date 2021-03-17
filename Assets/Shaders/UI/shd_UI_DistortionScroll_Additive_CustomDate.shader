Shader "Framework/UI/shd_UI_DistortionScroll_Additive_CustomDate" {
    Properties {
        _TintColor ("Color", Color) = (1,0.5342799,0.1764706,1)
        _ColorRamp ("Color Ramp", 2D) = "white" {}
        _ColorMultiplier ("Color Multiplier", Range(0, 10)) = 1.32872
        _MainTextureUSpeed ("Main Texture U Speed", Float ) = 0
        _MainTextureVSpeed ("Main Texture V Speed", Float ) = 0
        _MainTexutre ("Main Texutre", 2D) = "white" {}
        _GradientPower ("Gradient Power", Range(0, 50)) = 2.214298
        _GradientUSpeed ("Gradient U Speed", Float ) = -0.2
        _GradientVSpeed ("Gradient V Speed", Float ) = -0.2
        _GradientMap ("Gradient Map", 2D) = "white" {}
        _NoiseAmount ("Noise Amount", Range(-1, 1)) = 0.1144851
        _DistortionUSpeed ("Distortion U Speed", Float ) = 0.2
        _DistortionVSpeed ("Distortion V Speed", Float ) = 0
        _Distortion ("Distortion", 2D) = "white" {}
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
            Blend One One
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            CBUFFER_START(UnityPerMaterial)
            sampler2D _MainTexutre;
            float4 _MainTexutre_ST;
            float4 _TintColor;
            float _GradientUSpeed;
            float _GradientVSpeed;
            sampler2D _GradientMap;
            float4 _GradientMap_ST;
            float _NoiseAmount;
            sampler2D _Distortion;
            float4 _Distortion_ST;
            float _DistortionUSpeed;
            float _DistortionVSpeed;
            float _GradientPower;
            float _ColorMultiplier;
            sampler2D _ColorRamp;
            float4 _ColorRamp_ST;
            float _MainTextureVSpeed;
            float _MainTextureUSpeed;
            float _DoubleSided;
            CBUFFER_END

            struct appdate {
                float4 vertex :POSITION;
                float2 uv :TEXCOORD0;
                float4 uv_CustomData :TEXCOORD1;
                float4 vertexColor :COLOR;
            };

            struct v2f {
                float4 pos :SV_POSITION;
                float2 uv :TEXCOORD0;
                float4 uv_CustomData :TEXCOORD1;
                float4 vertexColor :COLOR;
            };

            v2f vert (appdate v) {
                v2f o;
                o.uv = v.uv;
                o.uv_CustomData = v.uv_CustomData;
                o.vertexColor = v.vertexColor;
                o.pos = UnityObjectToClipPos( v.vertex );
                return o;
            }

            float4 frag(v2f i) : COLOR {
                float2 distortionUV = (i.uv + (_Time.g * float2(_DistortionUSpeed, _DistortionVSpeed)));
                fixed4 distortionVar = tex2D(_Distortion, TRANSFORM_TEX(distortionUV, _Distortion));
                float2 noiseUV = lerp(i.uv, float2(distortionVar.r, distortionVar.r), _NoiseAmount);
                float2 mainTexUV = ((_Time.g * float2(_MainTextureUSpeed, _MainTextureVSpeed)) + i.uv);
                fixed4 mainTexVar = tex2D(_MainTexutre, TRANSFORM_TEX(mainTexUV, _MainTexutre));
                float4 colorRampVar = tex2D(_ColorRamp, TRANSFORM_TEX(i.uv, _ColorRamp));
                float2 gradientUV = (noiseUV + (_Time.g * float2(_GradientUSpeed,_GradientVSpeed)));
                fixed4 gradientVar = tex2D(_GradientMap,TRANSFORM_TEX(gradientUV, _GradientMap));
                fixed3 emissive = (mainTexVar.rgb * i.vertexColor.rgb*(_ColorMultiplier*_TintColor.rgb * colorRampVar.rgb)*2.0*(mainTexVar.a*pow(gradientVar.r,_GradientPower * i.uv_CustomData.x)));
                
                fixed4 finalCol;
                finalCol.rgb = emissive;
                finalCol.a = 1;
                return finalCol;
            }
            ENDHLSL
        }
    }
}
